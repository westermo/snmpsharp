// This file is part of SNMP#NET.
// 
// SNMP#NET is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// SNMP#NET is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with SNMP#NET.  If not, see <http://www.gnu.org/licenses/>.
// 
// First introduced: 0.5.2
//

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SnmpSharpNet;

/// <summary>
///     Send SNMP Trap notifications
/// </summary>
/// <remarks>
///     TrapAgent class is used to hide Socket operations from users and provide an easy method to send
///     Trap notifications.
///     To use the class, you can use the TrapAgent class protocol specific members, recommended when you
///     expect to send a lot of notifications, or a static helper TrapAgent.SendTrap method which will
///     construct a new socket for each call.
/// </remarks>
public class TrapAgent : IDisposable, IAsyncDisposable
{
    /// <summary>
    ///     Internal Socket class
    /// </summary>
    protected readonly Socket _sock;
    private bool _disposed;
    private readonly bool _supportsIpv6;

    /// <summary>
    ///     Constructor.
    /// </summary>
    /// <remarks>
    ///     Constructor initializes an internal Socket used to send traps. Socket is initialized by selecting a
    ///     random UDP port number.
    /// </remarks>
    public TrapAgent()
    {
        try
        {
            var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
            try
            {
                socket.DualMode = true;
                socket.Bind(new IPEndPoint(IPAddress.IPv6Any, 0));
            }
            catch
            {
                socket.Dispose();
                throw;
            }

            _sock = socket;
            _supportsIpv6 = true;
        }
        catch (Exception ex) when (ex is SocketException or PlatformNotSupportedException)
        {
            _sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _sock.Bind(new IPEndPoint(IPAddress.Any, 0));
            _supportsIpv6 = false;
        }
    }

    /// <summary>Closes the trap sender socket.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _sock.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task SendAsync(byte[] buffer, IPAddress peer, int port, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_supportsIpv6 && peer.AddressFamily == AddressFamily.InterNetworkV6)
            throw new SocketException((int)SocketError.AddressFamilyNotSupported);
        var address = peer.AddressFamily == AddressFamily.InterNetwork ? peer.MapToIPv6() : peer;
        var endpoint = new IPEndPoint(address, port);
        var sent = await _sock.SendToAsync(buffer.AsMemory(), SocketFlags.None, endpoint, cancellationToken)
            .ConfigureAwait(false);
        if (sent != buffer.Length)
            throw new SocketException((int)SocketError.MessageSize);
    }

    /// <summary>
    ///     Send SNMP version 1 Trap notification
    /// </summary>
    /// <param name="packet">SNMP v1 Trap packet class</param>
    /// <param name="peer">Manager (receiver) IP address</param>
    /// <param name="port">Manager (receiver) UDP port number</param>
    public void SendV1Trap(SnmpV1TrapPacket packet, IpAddress peer, int port)
    {
        SendV1TrapAsync(packet, peer, port).GetAwaiter().GetResult();
    }

    /// <summary>Sends an SNMP version 1 trap asynchronously.</summary>
    public Task SendV1TrapAsync(SnmpV1TrapPacket packet, IpAddress peer, int port,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        return SendAsync(packet.Encode(), (IPAddress)peer, port, cancellationToken);
    }

    /// <summary>Sends an SNMP version 1 trap asynchronously to an IPv4 or IPv6 manager.</summary>
    public Task SendV1TrapAsync(SnmpV1TrapPacket packet, IPAddress peer, int port,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(peer);
        return SendAsync(packet.Encode(), peer, port, cancellationToken);
    }

    /// <summary>
    ///     Construct and send SNMP v1 Trap
    /// </summary>
    /// <param name="receiver">Receiver IP address</param>
    /// <param name="receiverPort">Receiver UDP port number</param>
    /// <param name="community">SNMP community name</param>
    /// <param name="senderSysObjectID">Senders sysObjectID</param>
    /// <param name="senderIpAdress">Sender IP address</param>
    /// <param name="genericTrap">Generic trap code</param>
    /// <param name="specificTrap">Specific trap code</param>
    /// <param name="senderUpTime">Senders sysUpTime</param>
    /// <param name="varList">Variable binding list</param>
    public void SendV1Trap(IpAddress receiver, int receiverPort, string community, Oid senderSysObjectID,
        IpAddress senderIpAdress, int genericTrap, int specificTrap, uint senderUpTime,
        VbCollection varList)
    {
        SendV1TrapAsync(receiver, receiverPort, community, senderSysObjectID, senderIpAdress, genericTrap,
            specificTrap, senderUpTime, varList).GetAwaiter().GetResult();
    }

    /// <summary>Constructs and sends an SNMP version 1 trap asynchronously.</summary>
    public Task SendV1TrapAsync(IpAddress receiver, int receiverPort, string community, Oid senderSysObjectID,
        IpAddress senderIpAdress, int genericTrap, int specificTrap, uint senderUpTime, VbCollection varList,
        CancellationToken cancellationToken = default)
    {
        var packet = new SnmpV1TrapPacket(community)
        {
            Pdu =
            {
                Generic = genericTrap,
                Specific = specificTrap
            }
        };
        packet.Pdu.AgentAddress.Set(senderIpAdress);
        packet.Pdu.TimeStamp = senderUpTime;
        packet.Pdu.VbList.Add(varList);
        packet.Pdu.Enterprise.Set(senderSysObjectID);
        return SendV1TrapAsync(packet, receiver, receiverPort, cancellationToken);
    }

    /// <summary>
    ///     Send SNMP version 2 Trap notification
    /// </summary>
    /// <param name="packet">SNMP v2 Trap packet class</param>
    /// <param name="peer">Manager (receiver) IP address</param>
    /// <param name="port">Manager (receiver) UDP port number</param>
    public void SendV2Trap(SnmpV2Packet packet, IpAddress peer, int port)
    {
        SendV2TrapAsync(packet, peer, port).GetAwaiter().GetResult();
    }

    /// <summary>Sends an SNMP version 2 trap asynchronously.</summary>
    public Task SendV2TrapAsync(SnmpV2Packet packet, IpAddress peer, int port,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (packet.Pdu.Type != PduType.V2Trap)
            throw new SnmpInvalidPduTypeException("Invalid Pdu type.");
        return SendAsync(packet.Encode(), (IPAddress)peer, port, cancellationToken);
    }

    /// <summary>Sends an SNMP version 2 trap asynchronously to an IPv4 or IPv6 manager.</summary>
    public Task SendV2TrapAsync(SnmpV2Packet packet, IPAddress peer, int port,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(peer);
        if (packet.Pdu.Type != PduType.V2Trap)
            throw new SnmpInvalidPduTypeException("Invalid Pdu type.");
        return SendAsync(packet.Encode(), peer, port, cancellationToken);
    }

    /// <summary>
    ///     Construct and send SNMP v2 Trap
    /// </summary>
    /// <param name="receiver">Trap receiver IP address</param>
    /// <param name="receiverPort">Trap receiver UDP port number</param>
    /// <param name="community">SNMP community name</param>
    /// <param name="senderUpTime">Sender sysUpTime</param>
    /// <param name="trapObjectID">Trap ObjectID</param>
    /// <param name="varList">Variable binding list</param>
    public void SendV2Trap(IpAddress receiver, int receiverPort, string community, uint senderUpTime,
        Oid trapObjectID, VbCollection varList)
    {
        SendV2TrapAsync(receiver, receiverPort, community, senderUpTime, trapObjectID, varList)
            .GetAwaiter().GetResult();
    }

    /// <summary>Constructs and sends an SNMP version 2 trap asynchronously.</summary>
    public Task SendV2TrapAsync(IpAddress receiver, int receiverPort, string community, uint senderUpTime,
        Oid trapObjectID, VbCollection varList, CancellationToken cancellationToken = default)
    {
        var packet = new SnmpV2Packet(community)
        {
            Pdu =
            {
                Type = PduType.V2Trap,
                TrapObjectID = trapObjectID,
                TrapSysUpTime =
                {
                    Value = senderUpTime
                }
            }
        };
        packet.Pdu.SetVbList(varList);
        return SendV2TrapAsync(packet, receiver, receiverPort, cancellationToken);
    }

    /// <summary>
    ///     Send SNMP version 3 Trap notification
    /// </summary>
    /// <param name="packet">SNMP v3 Trap packet class</param>
    /// <param name="peer">Manager (receiver) IP address</param>
    /// <param name="port">Manager (receiver) UDP port number</param>
    public void SendV3Trap(SnmpV3Packet packet, IpAddress peer, int port)
    {
        SendV3TrapAsync(packet, peer, port).GetAwaiter().GetResult();
    }

    /// <summary>Sends an SNMP version 3 trap asynchronously.</summary>
    public Task SendV3TrapAsync(SnmpV3Packet packet, IpAddress peer, int port,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (packet.Pdu.Type != PduType.V2Trap)
            throw new SnmpInvalidPduTypeException("Invalid Pdu type.");
        return SendAsync(packet.Encode(), (IPAddress)peer, port, cancellationToken);
    }

    /// <summary>Sends an SNMP version 3 trap asynchronously to an IPv4 or IPv6 manager.</summary>
    public Task SendV3TrapAsync(SnmpV3Packet packet, IPAddress peer, int port,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(peer);
        if (packet.Pdu.Type != PduType.V2Trap)
            throw new SnmpInvalidPduTypeException("Invalid Pdu type.");
        return SendAsync(packet.Encode(), peer, port, cancellationToken);
    }

    /// <summary>
    ///     Construct and send SNMP v3 noAuthNoPriv Trap
    /// </summary>
    /// <param name="receiver">Trap receiver IP address</param>
    /// <param name="receiverPort">Trap receiver UDP port number</param>
    /// <param name="engineId">Sender SNMP engineId</param>
    /// <param name="senderEngineBoots">Sender SNMP engine boots</param>
    /// <param name="senderEngineTime">Sender SNMP engine time</param>
    /// <param name="senderUserName">Security (user) name</param>
    /// <param name="senderUpTime">Sender upTime</param>
    /// <param name="trapObjectID">Trap object ID</param>
    /// <param name="varList">Variable binding list</param>
    public void SendV3Trap(IpAddress receiver, int receiverPort, byte[] engineId, int senderEngineBoots,
        int senderEngineTime, string senderUserName, uint senderUpTime, Oid trapObjectID, VbCollection varList)
    {
        SendV3TrapAsync(receiver, receiverPort, engineId, senderEngineBoots, senderEngineTime, senderUserName,
            senderUpTime, trapObjectID, varList).GetAwaiter().GetResult();
    }

    /// <summary>Constructs and sends an SNMP version 3 noAuthNoPriv trap asynchronously.</summary>
    public Task SendV3TrapAsync(IpAddress receiver, int receiverPort, byte[] engineId, int senderEngineBoots,
        int senderEngineTime, string senderUserName, uint senderUpTime, Oid trapObjectID, VbCollection varList,
        CancellationToken cancellationToken = default)
    {
        var packet = new SnmpV3Packet
        {
            Pdu =
            {
                Type = PduType.V2Trap
            }
        };
        packet.NoAuthNoPriv(Encoding.UTF8.GetBytes(senderUserName));
        packet.SetEngineId(engineId);
        packet.SetEngineTime(senderEngineBoots, senderEngineTime);
        packet.ScopedPdu.TrapObjectID.Set(trapObjectID);
        packet.ScopedPdu.TrapSysUpTime.Value = senderUpTime;
        packet.ScopedPdu.VbList.Add(varList);
        packet.MsgFlags.Reportable = false;
        return SendV3TrapAsync(packet, receiver, receiverPort, cancellationToken);
    }

    /// <summary>
    ///     Construct and send SNMP v3 authNoPriv Trap
    /// </summary>
    /// <param name="receiver">Trap receiver IP address</param>
    /// <param name="receiverPort">Trap receiver UDP port number</param>
    /// <param name="engineId">Sender SNMP engineId</param>
    /// <param name="senderEngineBoots">Sender SNMP engine boots</param>
    /// <param name="senderEngineTime">Sender SNMP engine time</param>
    /// <param name="senderUserName">Security (user) name</param>
    /// <param name="senderUpTime">Sender upTime</param>
    /// <param name="trapObjectID">Trap object ID</param>
    /// <param name="varList">Variable binding list</param>
    /// <param name="authDigest">
    ///     Authentication digest. <see cref="AuthenticationDigests" /> enumeration for
    ///     available digests
    /// </param>
    /// <param name="authSecret">Authentication secret</param>
    public void SendV3Trap(IpAddress receiver, int receiverPort, byte[] engineId, int senderEngineBoots,
        int senderEngineTime, string senderUserName, uint senderUpTime, Oid trapObjectID, VbCollection varList,
        AuthenticationDigests authDigest, byte[] authSecret)
    {
        SendV3TrapAsync(receiver, receiverPort, engineId, senderEngineBoots, senderEngineTime, senderUserName,
            senderUpTime, trapObjectID, varList, authDigest, authSecret).GetAwaiter().GetResult();
    }

    /// <summary>Constructs and sends an SNMP version 3 authNoPriv trap asynchronously.</summary>
    public Task SendV3TrapAsync(IpAddress receiver, int receiverPort, byte[] engineId, int senderEngineBoots,
        int senderEngineTime, string senderUserName, uint senderUpTime, Oid trapObjectID, VbCollection varList,
        AuthenticationDigests authDigest, byte[] authSecret, CancellationToken cancellationToken = default)
    {
        var packet = new SnmpV3Packet
        {
            Pdu =
            {
                Type = PduType.V2Trap
            }
        };
        packet.AuthNoPriv(Encoding.UTF8.GetBytes(senderUserName), authSecret, authDigest);
        packet.SetEngineId(engineId);
        packet.SetEngineTime(senderEngineBoots, senderEngineTime);
        packet.ScopedPdu.TrapObjectID.Set(trapObjectID);
        packet.ScopedPdu.TrapSysUpTime.Value = senderUpTime;
        packet.ScopedPdu.VbList.Add(varList);
        packet.MsgFlags.Reportable = false;
        return SendV3TrapAsync(packet, receiver, receiverPort, cancellationToken);
    }

    /// <summary>
    ///     Construct and send SNMP v3 authPriv Trap
    /// </summary>
    /// <param name="receiver">Trap receiver IP address</param>
    /// <param name="receiverPort">Trap receiver UDP port number</param>
    /// <param name="engineId">Sender SNMP engineId</param>
    /// <param name="senderEngineBoots">Sender SNMP engine boots</param>
    /// <param name="senderEngineTime">Sender SNMP engine time</param>
    /// <param name="senderUserName">Security (user) name</param>
    /// <param name="senderUpTime">Sender upTime</param>
    /// <param name="trapObjectID">Trap object ID</param>
    /// <param name="varList">Variable binding list</param>
    /// <param name="authDigest">
    ///     Authentication digest. See <see cref="AuthenticationDigests" /> enumeration for
    ///     available digests
    /// </param>
    /// <param name="authSecret">Authentication secret</param>
    /// <param name="privProtocol">
    ///     Privacy protocol. See <see cref="PrivacyProtocols" /> enumeration for
    ///     available privacy protocols.
    /// </param>
    /// <param name="privSecret">Privacy secret</param>
    public void SendV3Trap(IpAddress receiver, int receiverPort, byte[] engineId, int senderEngineBoots,
        int senderEngineTime, string senderUserName, uint senderUpTime, Oid trapObjectID, VbCollection varList,
        AuthenticationDigests authDigest, byte[] authSecret, PrivacyProtocols privProtocol, byte[] privSecret)
    {
        SendV3TrapAsync(receiver, receiverPort, engineId, senderEngineBoots, senderEngineTime, senderUserName,
            senderUpTime, trapObjectID, varList, authDigest, authSecret, privProtocol, privSecret)
            .GetAwaiter().GetResult();
    }

    /// <summary>Constructs and sends an SNMP version 3 authPriv trap asynchronously.</summary>
    public Task SendV3TrapAsync(IpAddress receiver, int receiverPort, byte[] engineId, int senderEngineBoots,
        int senderEngineTime, string senderUserName, uint senderUpTime, Oid trapObjectID, VbCollection varList,
        AuthenticationDigests authDigest, byte[] authSecret, PrivacyProtocols privProtocol, byte[] privSecret,
        CancellationToken cancellationToken = default)
    {
        var packet = new SnmpV3Packet
        {
            Pdu =
            {
                Type = PduType.V2Trap
            }
        };
        packet.AuthPriv(Encoding.UTF8.GetBytes(senderUserName), authSecret, authDigest, privSecret, privProtocol);
        packet.SetEngineId(engineId);
        packet.SetEngineTime(senderEngineBoots, senderEngineTime);
        packet.ScopedPdu.TrapObjectID.Set(trapObjectID);
        packet.ScopedPdu.TrapSysUpTime.Value = senderUpTime;
        packet.ScopedPdu.VbList.Add(varList);
        packet.MsgFlags.Reportable = false;
        return SendV3TrapAsync(packet, receiver, receiverPort, cancellationToken);
    }

    /// <summary>
    ///     Send SNMP Trap notification
    /// </summary>
    /// <remarks>
    ///     Helper function to allow for seamless sending of SNMP notifications for all protocol versions.
    ///     packet parameter should be appropriately formatted SNMP notification in SnmpV1TrapPacket,
    ///     SnmpV2Packet or SnmpV3Packet class cast as SnmpPacket class.
    ///     Function will determine which version of the notification is to be used by checking the type
    ///     of the packet parameter and call appropriate TrapAgent member function to send it.
    /// </remarks>
    /// <param name="packet">SNMP trap packet</param>
    /// <param name="peer">Manager (receiver) IP address</param>
    /// <param name="port">Manager (receiver) UDP port number</param>
    public static void SendTrap(SnmpPacket packet, IpAddress peer, int port)
    {
        SendTrapAsync(packet, peer, port).GetAwaiter().GetResult();
    }

    /// <summary>Sends an SNMP trap asynchronously.</summary>
    public static async Task SendTrapAsync(SnmpPacket packet, IpAddress peer, int port,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(peer);
        await SendTrapAsync(packet, (IPAddress)peer, port, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends an SNMP trap asynchronously to an IPv4 or IPv6 manager.</summary>
    public static async Task SendTrapAsync(SnmpPacket packet, IPAddress peer, int port,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(peer);
        await using var agent = new TrapAgent();
        switch (packet)
        {
            case SnmpV1TrapPacket trapPacket:
                await agent.SendV1TrapAsync(trapPacket, peer, port, cancellationToken).ConfigureAwait(false);
                break;
            case SnmpV2Packet v2Packet:
                await agent.SendV2TrapAsync(v2Packet, peer, port, cancellationToken).ConfigureAwait(false);
                break;
            case SnmpV3Packet v3Packet:
                await agent.SendV3TrapAsync(v3Packet, peer, port, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new SnmpException("Invalid SNMP packet type.");
        }
    }
}