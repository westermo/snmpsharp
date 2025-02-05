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

using System;
using System.Net;
using System.Net.Sockets;

namespace SnmpSharpNet;

/// <summary>
///     Callback used to pass result of an async SNMP operation back to the caller.
/// </summary>
/// <param name="result">Result code of the operation.</param>
/// <param name="packet">SNMP packet received</param>
public delegate void SnmpAsyncResponse(AsyncRequestResult result, SnmpPacket packet);

/// <summary>Transport class for IPv4 using UDP</summary>
/// <remarks>
///     InternetProtocol version 4 User Datagram Protocol (IP/UDP) transport protocol
///     implementation for use with SNMP versions 1, 2 and 3.
/// </remarks>
public class UdpTarget : UdpTransport
{
    /// <summary>
    ///     SNMP request target host IP address
    /// </summary>
    protected IPAddress _address;

    /// <summary>
    ///     Internal storage of the agent parameters information passed to the async request member function.
    /// </summary>
    protected IAgentParameters? _agentParameters;

    /// <summary>
    ///     SNMP target UDP port number
    /// </summary>
    protected int _port;

    /// <summary>
    ///     Maximum number of retries. Value of 0 (zero) will result in a single request without
    ///     retries.
    /// </summary>
    protected int _retry;

    /// <summary>
    ///     SNMP request timeout value in milliseconds
    /// </summary>
    protected int _timeout;

    /// <summary>
    ///     Constructor.
    /// </summary>
    /// <param name="peer">SNMP peer IP address</param>
    /// <param name="port">SNMP peer UDP port number</param>
    /// <param name="timeout">SNMP peer timeout in milliseconds</param>
    /// <param name="retry">SNMP peer maximum retires setting. Value of 0 will result in a single request with no retries.</param>
    public UdpTarget(IPAddress peer, int port, int timeout, int retry)
        : base(peer.AddressFamily == AddressFamily.InterNetworkV6)
    {
        _address = peer;
        _port = port;
        _timeout = timeout;
        _retry = retry;
    }

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <remarks>
    ///     Initializes the class with defaults for timeout (2000ms = 2 seconds), retry (two) and agent UDP port
    ///     number (161).
    /// </remarks>
    /// <param name="peer">Agent IP address</param>
    public UdpTarget(IPAddress peer)
        : base(peer.AddressFamily == AddressFamily.InterNetworkV6)
    {
        _address = peer;
        _port = 161;
        _timeout = 2000;
        _retry = 2;
    }

    /// <summary>
    ///     Get/Set Udp agent IP address
    /// </summary>
    public IPAddress Address
    {
        get => _address;
        set
        {
            _address = value;
            switch (_address.AddressFamily)
            {
                case AddressFamily.InterNetworkV6 when !IsIPv6:
                    initSocket(true);
                    break;
                case AddressFamily.InterNetwork when IsIPv6:
                    initSocket(false);
                    break;
            }
        }
    }

    /// <summary>
    ///     Get/Set Udp agent port number
    /// </summary>
    public int Port
    {
        get => _port;
        set => _port = value;
    }

    /// <summary>
    ///     Get/Set Udp agent timeout value in milliseconds
    /// </summary>
    public int Timeout
    {
        get => _timeout;
        set => _timeout = value;
    }

    /// <summary>
    ///     Get/Set Udp agent maximum retry value. Value of 0 (zero) will result in a single request
    ///     being sent without further retry attempts.
    /// </summary>
    public int Retry
    {
        get => _retry;
        set => _retry = value;
    }

    /// <summary>Make SNMP Request</summary>
    /// <remarks>
    ///     Make SNMP request. With this method you can make blocked SNMP version 1, 2 and 3 requests of type GET,
    ///     GET-NEXT, GET-BULK, SET and REPORT (request types have to compatible with the SNMP protocol version you
    ///     are using).
    ///     This method will pass through any exceptions thrown by parsing classes/methods so see individual packet
    ///     classes, ASN.1 type classes, authentication, privacy, etc. classes for exceptions thrown.
    /// </remarks>
    /// <param name="pdu">Pdu class (do not pass ScopedPdu)</param>
    /// <param name="agentParameters">
    ///     Security information for the request. Use <see cref="AgentParameters" />
    ///     for SNMP versions 1 and 2 requests. Use <see cref="SecureAgentParameters" /> for SNMP version 3
    ///     requests.
    /// </param>
    /// <returns>
    ///     Appropriate SNMP packet class for the reply received (<see cref="SnmpV1Packet" />,
    ///     <see cref="SnmpV2Packet" />, or <see cref="SnmpV3Packet" />. Null value if there was an error
    ///     with the request.
    /// </returns>
    /// <exception cref="SnmpAuthenticationException">
    ///     Thrown on SNMPv3 requests when authentication password
    ///     is not specified on authNoPriv or authPriv requests in SecureAgentParameters or if incoming packet
    ///     authentication check failed.
    ///     With SNMP ver1 and ver2c, authentication check fails when invalid community name is parsed in the reply.
    /// </exception>
    /// <exception cref="SnmpPrivacyException">
    ///     Thrown on SNMPv3 requests when privacy password is not
    ///     specified in SecureAgentParameters on authPriv requests.
    /// </exception>
    /// <exception cref="SnmpException">
    ///     Thrown in following cases:
    ///     * IAgentParameters.Valid() returned false. SnmpException.ErrorCode is set to SnmpException.InvalidIAgentParameters
    ///     * No data received on request. SnmpException.ErrorCode is set to SnmpException.NoDataReceived
    ///     * Invalid RequestId in reply. SnmpException.ErrorCode is set to SnmpException.InvalidRequestId
    /// </exception>
    public SnmpPacket? Request(Pdu pdu, IAgentParameters agentParameters)
    {
        byte[] outPacket;
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
                _noSourceCheck = false; // this option is not valid for SNMP v3 requests
                var outPdu = new ScopedPdu(pdu);
                var packet = new SnmpV3Packet(outPdu);
                secparams.InitializePacket(packet);
                outPacket = secparams.HasCachedKeys switch
                {
                    true => packet.encode(secparams.AuthenticationKey, secparams.PrivacyKey),
                    _ => packet.encode()
                };
                break;
            }
            case SnmpVersion.Ver1:
            {
                var param = (AgentParameters)agentParameters;
                if (!param.Valid())
                    throw new SnmpException(SnmpException.InvalidIAgentParameters,
                        "Invalid AgentParameters. Unable to process request.");
                var packet = new SnmpV1Packet();
                packet.Pdu.Set(pdu);
                packet.Community.Set(param.Community);
                outPacket = packet.encode();
                _noSourceCheck = param.DisableReplySourceCheck;
                break;
            }
            case SnmpVersion.Ver2:
            {
                var param = (AgentParameters)agentParameters;
                if (!param.Valid())
                    throw new SnmpException(SnmpException.InvalidIAgentParameters,
                        "Invalid AgentParameters. Unable to process request.");
                var packet = new SnmpV2Packet();
                packet.Pdu.Set(pdu);
                packet.Community.Set(param.Community);
                _noSourceCheck = param.DisableReplySourceCheck;
                outPacket = packet.encode();
                break;
            }
            default:
                throw new SnmpInvalidVersionException("Unsupported SNMP version.");
        }

        var inBuffer = base.Request(_address, _port, outPacket, outPacket.Length, _timeout, _retry);

        if (inBuffer is not { Length: > 0 })
            throw new SnmpException(SnmpException.NoDataReceived, "No data received on request.");
        switch (agentParameters.Version)
        {
            // verify packet
            case SnmpVersion.Ver1:
            {
                var packet = new SnmpV1Packet();
                var param = (AgentParameters)agentParameters;
                packet.decode(inBuffer, inBuffer.Length);
                if (packet.Community != param.Community)
                    // invalid community name received. Ignore the rest of the packet
                    throw new SnmpAuthenticationException("Invalid community name in reply.");
                if (packet.Pdu.RequestId != pdu.RequestId)
                    // invalid request id. unmatched response ignored
                    throw new SnmpException(SnmpException.InvalidRequestId, "Invalid request id in reply.");
                return packet;
            }
            case SnmpVersion.Ver2:
            {
                var packet = new SnmpV2Packet();
                var param = (AgentParameters)agentParameters;
                packet.decode(inBuffer, inBuffer.Length);
                if (packet.Community != param.Community)
                    // invalid community name received. Ignore the rest of the packet
                    throw new SnmpAuthenticationException("Invalid community name in reply.");
                if (packet.Pdu.RequestId != pdu.RequestId)
                    // invalid request id. unmatched response ignored
                    throw new SnmpException(SnmpException.InvalidRequestId, "Invalid request id in reply.");
                return packet;
            }
            case SnmpVersion.Ver3:
            {
                var packet = new SnmpV3Packet();
                var secparams = (SecureAgentParameters)agentParameters;
                secparams.InitializePacket(packet);
                switch (secparams.HasCachedKeys)
                {
                    case true:
                        packet.decode(inBuffer, inBuffer.Length, secparams.AuthenticationKey, secparams.PrivacyKey);
                        break;
                    default:
                        packet.decode(inBuffer, inBuffer.Length);
                        break;
                }

                switch (packet.Pdu.Type)
                {
                    // first check if packet is a discovery response and process it
                    case PduType.Report when packet.Pdu.VbCount > 0 &&
                                             packet.Pdu.VbList[0].Oid?.Equals(SnmpConstants.usmStatsUnknownEngineIDs) ==
                                             true:
                        secparams.UpdateDiscoveryValues(packet);
                        return packet;
                }

                if (!secparams.ValidateIncomingPacket(packet)) return null;

                secparams.UpdateDiscoveryValues(packet); // update time, etc. values
                return packet;
            }
            default:
                return null;
        }
    }

    /// <summary>
    ///     Internal event to send result of the async request to.
    /// </summary>
    protected event SnmpAsyncResponse? _response;

    /// <summary>
    ///     Make SNMP request. With this method you can make blocked SNMP version 1, 2 and 3 requests of type GET,
    ///     GET-NEXT, GET-BULK, SET and REPORT (request types have to compatible with the SNMP protocol version you
    ///     are using).
    ///     This method will pass through any exceptions thrown by parsing classes/methods so see individual packet
    ///     classes, ASN.1 type classes, authentication, privacy, etc. classes for exceptions thrown.
    /// </summary>
    /// <param name="pdu">Pdu class (do not pass ScopedPdu)</param>
    /// <param name="agentParameters">
    ///     Security information for the request. Use <see cref="AgentParameters" />
    ///     for SNMP versions 1 and 2 requests. Use <see cref="SecureAgentParameters" /> for SNMP version 3
    ///     requests.
    /// </param>
    /// <param name="responseCallback">Callback that receives the result of the async operation.</param>
    /// <returns>True if async request was successfully initiated, otherwise false.</returns>
    public bool RequestAsync(Pdu pdu, IAgentParameters agentParameters, SnmpAsyncResponse? responseCallback)
    {
        if (IsBusy) return false; // class is busy

        _response = null;
        _response += responseCallback;
        _agentParameters = agentParameters;
        byte[] outPacket;
        switch (agentParameters.Version)
        {
            case SnmpVersion.Ver3:
            {
                var secparams = (SecureAgentParameters)agentParameters;
                if (secparams.Authentication != AuthenticationDigests.None &&
                    secparams.AuthenticationSecret.Length <= 0)
                    // _response(AsyncRequestResult.AuthenticationError, null);
                    return false;
                if (secparams.Privacy != PrivacyProtocols.None && secparams.PrivacySecret.Length <= 0)
                    // _response(AsyncRequestResult.PrivacyError, null);
                    return false;
                _noSourceCheck = false; // this option is not valid for SNMP v3 requests
                var outPdu = new ScopedPdu(pdu);
                outPdu.ContextEngineId.Set(secparams.EngineId);
                outPdu.ContextName.Set(secparams.ContextName);
                var packet = new SnmpV3Packet(outPdu);
                secparams.InitializePacket(packet);
                try
                {
                    outPacket = secparams.HasCachedKeys switch
                    {
                        true => packet.encode(secparams.AuthenticationKey, secparams.PrivacyKey),
                        _ => packet.encode()
                    };
                }
                catch (Exception)
                {
                    _response?.Invoke(AsyncRequestResult.EncodeError, packet);
                    return false;
                }

                break;
            }
            case (int)SnmpVersion.Ver1:
            {
                var param = (AgentParameters)agentParameters;
                _noSourceCheck = param.DisableReplySourceCheck;
                var packet = new SnmpV1Packet();
                packet.Pdu.Set(pdu);
                packet.Community.Set(param.Community);
                try
                {
                    outPacket = packet.encode();
                }
                catch (Exception)
                {
                    _response?.Invoke(AsyncRequestResult.EncodeError, packet);
                    return false;
                }

                break;
            }
            case SnmpVersion.Ver2:
            {
                var param = (AgentParameters)agentParameters;
                _noSourceCheck = param.DisableReplySourceCheck;
                var packet = new SnmpV2Packet();
                packet.Pdu.Set(pdu);
                packet.Community.Set(param.Community);
                try
                {
                    outPacket = packet.encode();
                }
                catch (Exception)
                {
                    _response?.Invoke(AsyncRequestResult.EncodeError, packet);
                    return false;
                }

                break;
            }
            default:
                throw new SnmpInvalidVersionException("Unsupported SNMP version.");
        }

        return base.RequestAsync(_address, _port, outPacket, outPacket.Length, _timeout, _retry, AsyncResponse);
    }

    /// <summary>
    ///     Perform SNMP version 3 discovery operation. This is the first operation that needs to be
    ///     performed on a newly accessed agent to retrieve agentId, agentBoots and agentTime values, critical
    ///     for further authentication and privacy operations.
    /// </summary>
    /// <param name="param">
    ///     <see cref="SecureAgentParameters" /> class instance that will be updated
    ///     with discovered agent values. This class with be reset to its defaults prior to agent
    ///     discovered values so do not store any critical information in it prior to calling the
    ///     discovery method
    /// </param>
    /// <returns>True if discovery operation was a success, otherwise false</returns>
    public bool Discovery(SecureAgentParameters param)
    {
        param.Reset();
        param.SecurityName.Set("");
        param.Reportable = true;
        var pdu = new Pdu(); // just leave everything at default.
        if (Request(pdu, param) is not SnmpV3Packet inpkt) return false;
        return inpkt.USM is not { EngineBoots: 0, EngineTime: 0 } || Request(pdu, param) is SnmpV3Packet;
    }

    /// <summary>
    ///     Make an async discovery request for protocol version 3.
    /// </summary>
    /// <param name="param">Agent parameters</param>
    /// <param name="callback">Callback method</param>
    /// <returns>True if operation was correctly initiated, otherwise false.</returns>
    public bool DiscoveryAsync(SecureAgentParameters param, SnmpAsyncResponse callback)
    {
        var p = new Pdu();
        return RequestAsync(p, param, callback);
    }

    internal void AsyncResponse(AsyncRequestResult result, IPEndPoint? peer, byte[]? buffer, int buflen)
    {
        if (result != AsyncRequestResult.NoError)
        {
            _response?.Invoke(result, null!);
        }
        else
        {
            if (buffer is not { Length: > 0 } || buflen <= 0)
            {
                _response?.Invoke(AsyncRequestResult.NoDataReceived, null!);
                return;
            }

            switch (_agentParameters?.Version)
            {
                // verify packet
                case (int)SnmpVersion.Ver1:
                {
                    var packet = new SnmpV1Packet();
                    try
                    {
                        packet.decode(buffer, buflen);
                    }
                    catch (Exception)
                    {
                        _response?.Invoke(AsyncRequestResult.DecodeError, packet);
                        return;
                    }

                    _response?.Invoke(AsyncRequestResult.NoError, packet);
                    break;
                }
                case SnmpVersion.Ver2:
                {
                    var packet = new SnmpV2Packet();
                    try
                    {
                        packet.decode(buffer, buflen);
                    }
                    catch (Exception)
                    {
                        _response?.Invoke(AsyncRequestResult.DecodeError, packet);
                        return;
                    }

                    _response?.Invoke(AsyncRequestResult.NoError, packet);
                    break;
                }
                case SnmpVersion.Ver3:
                {
                    var packet = new SnmpV3Packet();
                    var secparams = (SecureAgentParameters)_agentParameters;
                    secparams.InitializePacket(packet);
                    try
                    {
                        switch (secparams.HasCachedKeys)
                        {
                            case true:
                                packet.decode(buffer, buflen, secparams.AuthenticationKey, secparams.PrivacyKey);
                                break;
                            default:
                                packet.decode(buffer, buflen);
                                break;
                        }
                    }
                    catch
                    {
                        _response?.Invoke(AsyncRequestResult.DecodeError, packet);
                        return;
                    }

                    if (!secparams.ValidateIncomingPacket(packet))
                    {
                        _response?.Invoke(AsyncRequestResult.AuthenticationError, packet);
                    }
                    else
                    {
                        secparams.UpdateDiscoveryValues(packet); // update time, etc. values
                        switch (packet.USM.EngineId.Length)
                        {
                            case > 0 when packet.USM is { EngineBoots: 0, EngineTime: 0 }:
                            {
                                var p = new Pdu();
                                RequestAsync(p, _agentParameters, _response);
                                break;
                            }
                            default:
                                _response?.Invoke(AsyncRequestResult.NoError, packet);
                                break;
                        }
                    }

                    break;
                }
            }
        }
    }
}