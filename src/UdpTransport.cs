// This file is part of SNMP#NET.
//
// SNMP#NET is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// SNMP#NET is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with SNMP#NET. If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SnmpSharpNet;

/// <summary>IP/UDP transport with cancellable asynchronous requests.</summary>
public class UdpTransport : IDisposable
{
    private const int MaxDatagramSize = 64 * 1024;
    private readonly object _socketLock = new();
    private int _busy;
    private bool _isIPv6;

    /// <summary>Disables validation of the reply's source IP address and port.</summary>
    protected bool _noSourceCheck;

    /// <summary>The transport socket.</summary>
    protected Socket? _socket;

    /// <summary>Initializes and binds an IPv4 or IPv6 UDP socket.</summary>
    public UdpTransport(bool useV6)
    {
        initSocket(useV6);
    }

    /// <summary>Whether the transport uses IPv6.</summary>
    public bool IsIPv6 => _isIPv6;

    /// <summary>Whether a transport request is in progress.</summary>
    public bool IsBusy => Volatile.Read(ref _busy) != 0;

    /// <summary>Closes the socket and aborts any pending request.</summary>
    public void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }

    /// <summary>Replaces the socket. Not permitted while a request is in progress.</summary>
    protected void initSocket(bool useV6)
    {
        lock (_socketLock)
        {
            if (IsBusy) throw new InvalidOperationException("A request is in progress.");
            var socket = new Socket(useV6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork,
                SocketType.Dgram, ProtocolType.Udp);
            try
            {
                socket.Bind(new IPEndPoint(useV6 ? IPAddress.IPv6Any : IPAddress.Any, 0));
            }
            catch
            {
                socket.Dispose();
                throw;
            }

            _socket?.Dispose();
            _socket = socket;
            _isIPv6 = useV6;
        }
    }

    /// <summary>Synchronously waits for <see cref="RequestAsync"/>.</summary>
    [Obsolete("Use RequestAsync; this synchronous wrapper blocks the calling thread.")]
    public byte[] Request(IPAddress peer, int port, byte[] buffer, int bufferLength, int timeout, int retries)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return RequestAsync(peer, port, buffer.AsMemory(0, bufferLength), timeout, retries)
            .GetAwaiter().GetResult();
    }

    /// <summary>Sends a UDP request and awaits its response.</summary>
    /// <param name="peer">Agent IP address.</param>
    /// <param name="port">Agent UDP port.</param>
    /// <param name="buffer">Datagram to send; must remain unchanged until completion.</param>
    /// <param name="timeout">Per-attempt timeout in milliseconds. Zero or -1 means no timeout.</param>
    /// <param name="retries">Number of additional attempts after the initial request.</param>
    /// <param name="cancellationToken">Cancels the entire request without retrying.</param>
    /// <exception cref="InvalidOperationException">Another request is in progress.</exception>
    /// <exception cref="ObjectDisposedException">The transport is closed.</exception>
    /// <exception cref="OperationCanceledException">The caller canceled the request.</exception>
    /// <exception cref="SnmpException">All attempts timed out.</exception>
    /// <exception cref="SnmpNetworkException">A socket operation failed.</exception>
    public Task<byte[]> RequestAsync(IPAddress peer, int port, ReadOnlyMemory<byte> buffer, int timeout, int retries,
        CancellationToken cancellationToken = default) =>
        RequestCoreAsync(peer, port, buffer, timeout, retries, _noSourceCheck, cancellationToken);

    /// <summary>Performs a request using a request-local reply-source policy.</summary>
    protected async Task<byte[]> RequestCoreAsync(IPAddress peer, int port, ReadOnlyMemory<byte> buffer, int timeout,
        int retries, bool disableSourceCheck, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(peer);
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, -1);
        ArgumentOutOfRangeException.ThrowIfNegative(retries);
        cancellationToken.ThrowIfCancellationRequested();
        var netPeer = new IPEndPoint(peer, port);
        Socket socket;
        lock (_socketLock)
        {
            socket = _socket ?? throw new ObjectDisposedException(nameof(UdpTransport));
            if (socket.AddressFamily != peer.AddressFamily)
                throw new SnmpException("Invalid address protocol version.");
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
                throw new InvalidOperationException("A request is in progress.");
        }

        using var activity = SnmpDiagnostics.ActivitySource.StartActivity("udp.request", ActivityKind.Client);
        activity?.SetTag("network.transport", "udp");
        activity?.SetTag("network.address.family", peer.AddressFamily.ToString());
        byte[]? receiveBuffer = null;
        try
        {
            receiveBuffer = ArrayPool<byte>.Shared.Rent(MaxDatagramSize);
            EndPoint anyPeer = new IPEndPoint(IsIPv6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
            for (var attempt = 0;; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var attemptCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (timeout > 0) attemptCancellation.CancelAfter(timeout);
                try
                {
                    await socket.SendToAsync(buffer, SocketFlags.None, netPeer, attemptCancellation.Token)
                        .ConfigureAwait(false);
                    while (true)
                    {
                        var result = await socket.ReceiveFromAsync(receiveBuffer.AsMemory(0, MaxDatagramSize),
                            SocketFlags.None, anyPeer, attemptCancellation.Token).ConfigureAwait(false);
                        if (!disableSourceCheck && !result.RemoteEndPoint.Equals(netPeer)) continue;
                        if (result.ReceivedBytes == 0) break;
                        activity?.SetStatus(ActivityStatusCode.Ok);
                        return receiveBuffer.AsSpan(0, result.ReceivedBytes).ToArray();
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
                catch (OperationCanceledException) when (attemptCancellation.IsCancellationRequested)
                {
                    // Awaiting cancellation removes the pending receive before the next attempt starts.
                }
                catch (SocketException ex) when (ex.SocketErrorCode is SocketError.TimedOut or SocketError.MessageSize)
                {
                    // Retry transient timeout/oversized-datagram failures.
                }
                catch (SocketException) when (!ReferenceEquals(socket, _socket))
                {
                    throw new ObjectDisposedException(nameof(UdpTransport));
                }
                catch (SocketException ex)
                {
                    throw new SnmpNetworkException(ex, $"Network error: {ex.SocketErrorCode}.");
                }

                if (attempt >= retries)
                {
                    SnmpDiagnostics.Timeouts.Add(1, SnmpDiagnostics.TransportTags("timeout"));
                    activity?.SetStatus(ActivityStatusCode.Error, "request timed out");
                    throw new SnmpException(SnmpException.RequestTimedOut, "Request has reached maximum retries.");
                }

                SnmpDiagnostics.Retries.Add(1, SnmpDiagnostics.TransportTags("retry"));
            }
        }
        catch (OperationCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "request canceled");
            throw;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            throw;
        }
        finally
        {
            if (receiveBuffer != null) ArrayPool<byte>.Shared.Return(receiveBuffer);
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    /// <summary>Closes the socket and aborts any pending request.</summary>
    public void Close()
    {
        lock (_socketLock)
        {
            var socket = _socket;
            _socket = null;
            socket?.Dispose();
        }
    }
}
