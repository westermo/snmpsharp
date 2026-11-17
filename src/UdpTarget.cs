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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SnmpSharpNet;

/// <summary>UDP target for SNMP versions 1, 2c and 3.</summary>
public class UdpTarget : UdpTransport
{
    /// <summary>Agent IP address.</summary>
    protected IPAddress _address;
    /// <summary>Agent UDP port.</summary>
    protected int _port;
    /// <summary>Number of additional attempts.</summary>
    protected int _retry;
    /// <summary>Per-attempt timeout in milliseconds.</summary>
    protected int _timeout;

    /// <summary>Creates a target with the specified timeout and retry count.</summary>
    public UdpTarget(IPAddress peer, int port, int timeout, int retry)
        : base(peer.AddressFamily == AddressFamily.InterNetworkV6)
    {
        _address = peer;
        _port = port;
        _timeout = timeout;
        _retry = retry;
    }

    /// <summary>Creates a target on port 161 with a 2000ms timeout and two retries.</summary>
    public UdpTarget(IPAddress peer) : this(peer, 161, 2000, 2)
    {
    }

    /// <summary>Agent address. Changing IP address family replaces the socket.</summary>
    public IPAddress Address
    {
        get => _address;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if ((value.AddressFamily == AddressFamily.InterNetworkV6) != IsIPv6)
                initSocket(value.AddressFamily == AddressFamily.InterNetworkV6);
            _address = value;
        }
    }

    /// <summary>Agent UDP port.</summary>
    public int Port
    {
        get => _port;
        set => _port = value;
    }

    /// <summary>Per-attempt timeout in milliseconds. Zero or -1 means no timeout.</summary>
    public int Timeout
    {
        get => _timeout;
        set => _timeout = value;
    }

    /// <summary>Number of additional attempts after the initial request.</summary>
    public int Retry
    {
        get => _retry;
        set => _retry = value;
    }

    /// <summary>Synchronously waits for <see cref="RequestAsync(Pdu, IAgentParameters, CancellationToken)"/>.</summary>
    public SnmpPacket Request(Pdu pdu, IAgentParameters agentParameters) =>
        RequestAsync(pdu, agentParameters).GetAwaiter().GetResult();

    /// <summary>Encodes, sends, receives and validates an SNMP request asynchronously.</summary>
    /// <remarks>
    ///     Errors are raised as exceptions rather than callback result codes. Caller cancellation does not retry.
    ///     Do not mutate the PDU, agent parameters or target settings until the request completes.
    /// </remarks>
    /// <param name="pdu">Request PDU (not a ScopedPdu).</param>
    /// <param name="agentParameters">Community or SNMPv3 security parameters.</param>
    /// <param name="cancellationToken">Cancels the entire request.</param>
    /// <returns>The decoded and validated response packet.</returns>
    public async Task<SnmpPacket> RequestAsync(Pdu pdu, IAgentParameters agentParameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdu);
        ArgumentNullException.ThrowIfNull(agentParameters);
        cancellationToken.ThrowIfCancellationRequested();
        byte[] outPacket;
        var disableSourceCheck = false;
        switch (agentParameters.Version)
        {
            case SnmpVersion.Ver3:
            {
                var secparams = (SecureAgentParameters)agentParameters;
                if (secparams.Authentication != AuthenticationDigests.None &&
                    secparams.AuthenticationSecret.Length <= 0)
                    throw new SnmpAuthenticationException("Authentication password not specified.");
                if (secparams.Privacy != PrivacyProtocols.None && secparams.PrivacySecret.Length <= 0)
                    throw new SnmpPrivacyException("Privacy password not specified.");
                var packet = new SnmpV3Packet(new ScopedPdu(pdu));
                secparams.InitializePacket(packet);
                outPacket = secparams.HasCachedKeys
                    ? packet.Encode(secparams.AuthenticationKey, secparams.PrivacyKey)
                    : packet.Encode();
                break;
            }
            case SnmpVersion.Ver1:
            case SnmpVersion.Ver2:
            {
                var param = (AgentParameters)agentParameters;
                if (!param.Valid())
                    throw new SnmpException(SnmpException.InvalidIAgentParameters,
                        "Invalid AgentParameters. Unable to process request.");
                disableSourceCheck = param.DisableReplySourceCheck;
                if (agentParameters.Version == SnmpVersion.Ver1)
                {
                    var packet = new SnmpV1Packet();
                    packet.Pdu.Set(pdu);
                    packet.Community.Set(param.Community);
                    outPacket = packet.Encode();
                }
                else
                {
                    var packet = new SnmpV2Packet();
                    packet.Pdu.Set(pdu);
                    packet.Community.Set(param.Community);
                    outPacket = packet.Encode();
                }
                break;
            }
            default:
                throw new SnmpInvalidVersionException("Unsupported SNMP version.");
        }

        var inBuffer = await RequestCoreAsync(_address, _port, outPacket, _timeout, _retry, disableSourceCheck,
            cancellationToken).ConfigureAwait(false);
        switch (agentParameters.Version)
        {
            case SnmpVersion.Ver1:
            {
                var packet = new SnmpV1Packet();
                packet.Decode(inBuffer);
                ValidateReply(packet.Community, packet.Pdu, (AgentParameters)agentParameters, pdu.RequestId);
                return packet;
            }
            case SnmpVersion.Ver2:
            {
                var packet = new SnmpV2Packet();
                packet.Decode(inBuffer);
                ValidateReply(packet.Community, packet.Pdu, (AgentParameters)agentParameters, pdu.RequestId);
                return packet;
            }
            case SnmpVersion.Ver3:
            {
                var packet = new SnmpV3Packet();
                var secparams = (SecureAgentParameters)agentParameters;
                secparams.InitializePacket(packet);
                if (secparams.HasCachedKeys)
                    packet.Decode(inBuffer, secparams.AuthenticationKey, secparams.PrivacyKey);
                else
                    packet.Decode(inBuffer);

                if (packet.Pdu.Type == PduType.Report && packet.Pdu.VbCount > 0 &&
                    packet.Pdu.VbList[0].Oid?.Equals(SnmpConstants.usmStatsUnknownEngineIDs) == true)
                {
                    secparams.UpdateDiscoveryValues(packet);
                    return packet;
                }

                if (!secparams.ValidateIncomingPacket(packet))
                    throw new SnmpAuthenticationException("Incoming SNMPv3 packet validation failed.");
                secparams.UpdateDiscoveryValues(packet);
                return packet;
            }
            default:
                throw new SnmpInvalidVersionException("Unsupported SNMP version.");
        }
    }

    private static void ValidateReply(OctetString community, Pdu reply, AgentParameters parameters, int requestId)
    {
        if (community != parameters.Community)
            throw new SnmpAuthenticationException("Invalid community name in reply.");
        if (reply.RequestId != requestId)
            throw new SnmpException(SnmpException.InvalidRequestId, "Invalid request id in reply.");
    }

    /// <summary>Synchronously waits for <see cref="DiscoveryAsync"/>.</summary>
    public bool Discovery(SecureAgentParameters param) => DiscoveryAsync(param).GetAwaiter().GetResult();

    /// <summary>Discovers and updates the agent's SNMPv3 engine parameters.</summary>
    /// <remarks>Resets the supplied parameters before discovery. Transport and decoding errors propagate.</remarks>
    public async Task<bool> DiscoveryAsync(SecureAgentParameters param, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(param);
        cancellationToken.ThrowIfCancellationRequested();
        param.Reset();
        param.SecurityName.Set("");
        param.Reportable = true;
        var pdu = new Pdu();
        var packet = (SnmpV3Packet)await RequestAsync(pdu, param, cancellationToken).ConfigureAwait(false);
        if (packet.USM is { EngineBoots: 0, EngineTime: 0 })
            await RequestAsync(pdu, param, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
