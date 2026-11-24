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
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SnmpSharpNet;

/// <summary>
///     Utility class to simplify SNMP version 1, 2 and 3 requests.
/// </summary>
/// <remarks>
///     The legacy synchronous operations can suppress errors and return null. Async SNMP operations report failures
///     as exceptions. Use <see cref="ResolveAsync"/> to check peer resolution separately.
/// </remarks>
public class SimpleSnmp
{
    /// <summary>
    ///     the authentication protocol used for SNMPV3
    /// </summary>
    protected readonly AuthenticationDigests _authDigests = AuthenticationDigests.None;

    /// <summary>
    ///     the authentication password
    /// </summary>
    protected readonly string _authSecret = "";

    /// <summary>
    ///     SNMP community name
    /// </summary>
    protected string _community;

    /// <summary>
    ///     the SNMPV3 context name
    /// </summary>
    protected readonly string _contextName = "";

    /// <summary>
    ///     Maximum repetitions value used in SNMP GET-BULK requests
    /// </summary>
    protected int _maxRepetitions = 50;

    /// <summary>
    ///     Non repeaters value used in SNMP GET-BULK requests
    /// </summary>
    protected int _nonRepeaters;

    /// <summary>
    ///     SNMP Agents IP address
    /// </summary>
    protected IPAddress _peerIP;

    /// <summary>
    ///     SNMP Agents name
    /// </summary>
    protected string _peerName;

    /// <summary>
    ///     SNMP Agent UDP port number
    /// </summary>
    protected int _peerPort;

    /// <summary>
    ///     the privacy protocol used for SNMPV3
    /// </summary>
    protected readonly PrivacyProtocols _privProtocols = PrivacyProtocols.None;

    /// <summary>
    ///     the privacy password
    /// </summary>
    protected readonly string _privSecret = "";

    /// <summary>
    ///     Maximum retry count excluding the first request
    /// </summary>
    protected int _retry;

    /// <summary>
    ///     the SNMPV3 secure parameters
    /// </summary>
    private SecureAgentParameters? _secparam;

    /// <summary>
    ///     security name for SNMPV3 requests
    /// </summary>
    protected readonly string _securityName = "";

    /// <summary>
    ///     Flag determines if exceptions are suppressed or thrown. When exceptions are suppressed, methods
    ///     return null on errors regardless of what the error is.
    /// </summary>
    protected bool _suppressExceptions = true;

    /// <summary>
    ///     Target class
    /// </summary>
    protected UdpTarget? _target;

    /// <summary>
    ///     Timeout value in milliseconds
    /// </summary>
    protected int _timeout;

    /// <summary>Constructor.</summary>
    /// <remarks>
    ///     Class is initialized to default values. Peer IP address is set to loopback, peer port number
    ///     to 161, timeout to 2000 ms (2 seconds), retry count to 2 and community name to public.
    /// </remarks>
    public SimpleSnmp()
    {
        _peerIP = IPAddress.Loopback;
        _peerPort = 161;
        _timeout = 2000;
        _retry = 2;
        _community = "public";
        _target = null;
        _peerName = "localhost";
    }

    /// <summary>
    ///     Constructor.
    /// </summary>
    /// <remarks>
    ///     Class is initialized with default values with the agent name set to the supplied DNS name (or
    ///     IP address). DNS resolution is deferred until <see cref="ResolveAsync"/> or the first request.
    /// </remarks>
    /// <param name="peerName">Peer name or IP address</param>
    public SimpleSnmp(string peerName)
        : this()
    {
        _peerName = peerName;
        _peerIP = IPAddress.None;
    }

    /// <summary>
    ///     Constructor.
    /// </summary>
    /// <param name="peerName">Peer name or IP address</param>
    /// <param name="peerPort">Peer UDP port number</param>
    public SimpleSnmp(string peerName, int peerPort)
        : this(peerName)
    {
        _peerPort = peerPort;
    }

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="peerName">Peer name or IP address</param>
    /// <param name="community">SNMP community name</param>
    public SimpleSnmp(string peerName, string community)
        : this(peerName)
    {
        _community = community;
    }

    /// <summary>
    ///     Constructor.
    /// </summary>
    /// <param name="peerName">Peer name or IP address</param>
    /// <param name="peerPort">Peer UDP port number</param>
    /// <param name="community">SNMP community name</param>
    public SimpleSnmp(string peerName, int peerPort, string community)
        : this(peerName, community)
    {
        _peerPort = peerPort;
    }

    /// <summary>
    ///     Constructor.
    /// </summary>
    /// <param name="peerName">Peer name or IP address</param>
    /// <param name="peerPort">Peer UDP port number</param>
    /// <param name="community">SNMP community name</param>
    /// <param name="timeout">SNMP request timeout</param>
    /// <param name="retry">Maximum number of retries excluding the first request (0 = 1 request is sent)</param>
    public SimpleSnmp(string peerName, int peerPort, string community, int timeout, int retry)
        : this(peerName, peerPort, community)
    {
        _timeout = timeout;
        _retry = retry;
    }

    /// <summary>
    ///     Constructor.
    /// </summary>
    /// <param name="peerName">Peer name or IP address</param>
    /// <param name="securityName">security name for SNMPV3 requests</param>
    /// <param name="authDigests">the authentication protocol used for SNMPV3</param>
    /// <param name="authSecret">the authentication password</param>
    /// <param name="privProtocols">the privacy protocol used for SNMPV3</param>
    /// <param name="privSecret">the privacy password</param>
    /// <param name="contextName">the SNMPV3 context name</param>
    public SimpleSnmp(string peerName, string securityName, AuthenticationDigests authDigests,
        string authSecret, PrivacyProtocols privProtocols, string privSecret, string contextName)
        : this(peerName)
    {
        _securityName = securityName;
        _authDigests = authDigests;
        _authSecret = authSecret;
        _privProtocols = privProtocols;
        _privSecret = privSecret;
        _contextName = contextName;
    }

    /// <summary>
    ///     Constructor.
    /// </summary>
    /// <param name="peerName">Peer name or IP address</param>
    /// <param name="peerPort">Peer UDP port number</param>
    /// <param name="securityName">security name for SNMPV3 requests</param>
    /// <param name="authDigests">the authentication protocol used for SNMPV3</param>
    /// <param name="authSecret">the authentication password</param>
    /// <param name="privProtocols">the privacy protocol used for SNMPV3</param>
    /// <param name="privSecret">the privacy password</param>
    /// <param name="contextName">the SNMPV3 context name</param>
    public SimpleSnmp(string peerName, int peerPort, string securityName, AuthenticationDigests authDigests,
        string authSecret, PrivacyProtocols privProtocols, string privSecret, string contextName)
        : this(peerName, peerPort)
    {
        _securityName = securityName;
        _authDigests = authDigests;
        _authSecret = authSecret;
        _privProtocols = privProtocols;
        _privSecret = privSecret;
        _contextName = contextName;
    }

    /// <summary>Class validity flag</summary>
    /// <remarks>
    ///     Return whether the peer name and community are configured. DNS resolution is deferred until
    ///     <see cref="ResolveAsync"/> or the first request.
    /// </remarks>
    public bool Valid
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_peerName)) return false;

            switch (_community.Length)
            {
                case < 1 or > 50:
                    return false;
                default:
                    return true;
            }
        }
    }

    private SecureAgentParameters? DiscoverSecureAgentParameters() =>
        _target is { } target
            ? DiscoverSecureAgentParametersAsync(target, CancellationToken.None).GetAwaiter().GetResult()
            : null;

    private async Task<SecureAgentParameters> DiscoverSecureAgentParametersAsync(UdpTarget target,
        CancellationToken cancellationToken)
    {
        if (_secparam is { EngineId.Length: > 0 } cachedParameters)
            return cachedParameters;

        var parameters = new SecureAgentParameters();
        if (!await target.DiscoveryAsync(parameters, cancellationToken).ConfigureAwait(false) ||
            parameters.EngineId.Length == 0)
            throw new SnmpException("SNMP version 3 discovery failed.");

        if (_authDigests == AuthenticationDigests.None && _privProtocols == PrivacyProtocols.None)
            parameters.noAuthNoPriv(_securityName);
        else if (_privProtocols == PrivacyProtocols.None)
            parameters.authNoPriv(_securityName, _authDigests, _authSecret);
        else
            parameters.authPriv(_securityName, _authDigests, _authSecret, _privProtocols, _privSecret);

        parameters.ContextName.Set(_contextName);
        _secparam = parameters;
        return parameters;
    }

    /// <summary>
    ///     SNMP GET request
    /// </summary>
    /// <example>
    ///     SNMP GET request:
    ///     <code>
    /// String snmpAgent = "10.10.10.1";
    /// String snmpCommunity = "public";
    /// SimpleSnmp snmp = new SimpleSnmp(snmpAgent, snmpCommunity);
    /// // Create a request Pdu
    /// Pdu pdu = new Pdu();
    /// pdu.Type = SnmpConstants.GET; // type GET
    /// pdu.VbList.Add("1.3.6.1.2.1.1.1.0");
    /// Dictionary&lt;Oid, AsnType&gt; result = snmp.GetNext(SnmpVersion.Ver1, pdu);
    /// if( result == null ) {
    ///   Console.WriteLine("Request failed.");
    /// } else {
    ///   foreach (KeyValuePair&lt;Oid, AsnType&gt; entry in result)
    ///   {
    ///     Console.WriteLine("{0} = {1}: {2}", entry.Key.ToString(), SnmpConstants.GetTypeName(entry.Value.Type),
    ///       entry.Value.ToString());
    ///   }
    /// }
    /// </code>
    ///     Will return:
    ///     <code>
    /// 1.3.6.1.2.1.1.1.0 = OctetString: "Dual core Intel notebook"
    /// </code>
    /// </example>
    /// <param name="version">SNMP protocol version.</param>
    /// <param name="pdu">Request Protocol Data Unit</param>
    /// <returns>Result of the SNMP request in a dictionary format with Oid => AsnType values</returns>
    [Obsolete("Use GetAsync; this synchronous API blocks the calling thread.")]
    public Dictionary<Oid, AsnType>? Get(SnmpVersion version, Pdu pdu)
    {
        if (!ResolveOrThrow()) return null;

        switch (Valid)
        {
            case false:
            {
                return _suppressExceptions switch
                {
                    false => throw new SnmpException("SimpleSnmp class is not valid."),
                    _ => null
                };
            }
        }

        try
        {
            _target = new UdpTarget(_peerIP, _peerPort, _timeout, _retry);
        }
        catch (Exception)
        {
            _target = null;
            switch (_suppressExceptions)
            {
                case false:
                    throw;
            }
        }

        switch (_target)
        {
            case null:
                return null;
        }

        try
        {
            IAgentParameters? param;
            if (version == SnmpVersion.Ver3)
            {
                param = DiscoverSecureAgentParameters();
                if (param == null)
                {
                    if (!_suppressExceptions)
                        throw new SnmpException("SNMP version 3 discovery failed.");
                    return null;
                }
            }
            else
            {
                param = new AgentParameters(version, new OctetString(_community));
            }

            var result = _target.Request(pdu, param);
            if (version == SnmpVersion.Ver3 && result is not null && result.Pdu.Type == PduType.Report)
            {
                var oid = result.Pdu.VbList[0].Oid;
                if (oid is not null && oid.Equals(SnmpConstants.usmStatsNotInTimeWindows))
                {
                    result = _target.Request(pdu, _secparam!);
                }
            }

            if (result?.Pdu.ErrorStatus == 0)
            {
                var res = new Dictionary<Oid, AsnType>();
                foreach (var v in result.Pdu.VbList)
                    switch (version)
                    {
                        case SnmpVersion.Ver2 or SnmpVersion.Ver3 when
                            v.Value is { Type: SnmpConstants.SMI_NOSUCHINSTANCE or SnmpConstants.SMI_NOSUCHOBJECT }:
                        {
                            switch (v.Oid is not null && res.ContainsKey(v.Oid))
                            {
                                case false:
                                    if (v.Oid is not null) res.Add(v.Oid, new Null());
                                    break;
                                default:
                                    res.Add(Oid.NullOid(), v.Value);
                                    break;
                            }

                            break;
                        }
                        default:
                        {
                            switch (v.Oid is not null && res.ContainsKey(v.Oid))
                            {
                                case false:
                                    if (v.Oid is not null && v.Value is not null) res.Add(v.Oid, v.Value);
                                    break;
                                default:
                                {
                                    if (v.Oid is not null && v.Value is not null && res[v.Oid].Type == v.Value.Type)
                                        res[v.Oid] = v.Value; // update value of the existing Oid entry
                                    else if (v.Oid is not null && v.Value != null)
                                        throw new SnmpException(SnmpException.OidValueTypeChanged,
                                            $"Value type changed from {res[v.Oid].Type} to {v.Value.Type}");
                                    break;
                                }
                            }

                            break;
                        }
                    }

                _target.Close();
                _target = null;
                return res;
            }

            switch (_suppressExceptions)
            {
                case false:
                    throw new SnmpErrorStatusException("Agent responded with an error", result?.Pdu.ErrorStatus ?? 0,
                        result?.Pdu.ErrorIndex ?? 0);
            }
        }
        catch (Exception)
        {
            switch (_suppressExceptions)
            {
                case false:
                    _target?.Close();
                    _target = null;
                    throw;
            }
        }

        _target?.Close();
        _target = null;
        return null;
    }

    /// <summary>
    ///     SNMP GET request
    /// </summary>
    /// <example>
    ///     SNMP GET request:
    ///     <code>
    /// String snmpAgent = "10.10.10.1";
    /// String snmpCommunity = "public";
    /// SimpleSnmp snmp = new SimpleSnmp(snmpAgent, snmpCommunity);
    /// Dictionary&lt;Oid, AsnType&gt; result = snmp.GetNext(SnmpVersion.Ver1, new string[] { "1.3.6.1.2.1.1.1.0" });
    /// if( result == null ) {
    ///   Console.WriteLine("Request failed.");
    /// } else {
    ///   foreach (KeyValuePair&lt;Oid, AsnType&gt; entry in result)
    ///   {
    ///     Console.WriteLine("{0} = {1}: {2}", entry.Key.ToString(), SnmpConstants.GetTypeName(entry.Value.Type),
    ///       entry.Value.ToString());
    ///   }
    /// }
    /// </code>
    ///     Will return:
    ///     <code>
    /// 1.3.6.1.2.1.1.1.0 = OctetString: "Dual core Intel notebook"
    /// </code>
    /// </example>
    /// <param name="version">SNMP protocol version number.</param>
    /// <param name="oidList">List of request OIDs in string dotted decimal format.</param>
    /// <returns>Result of the SNMP request in a dictionary format with Oid => AsnType values</returns>
    [Obsolete("Use GetAsync; this synchronous API blocks the calling thread.")]
    public Dictionary<Oid, AsnType>? Get(SnmpVersion version, string[] oidList)
    {
        switch (Valid)
        {
            case false:
            {
                return _suppressExceptions switch
                {
                    false => throw new SnmpException("SimpleSnmp class is not valid."),
                    _ => null
                };
            }
        }

        var pdu = new Pdu(PduType.Get);
        foreach (var s in oidList) pdu.VbList.Add(s);

        return Get(version, pdu);
    }

    /// <summary>
    ///     SNMP GET-NEXT request
    /// </summary>
    /// <example>
    ///     SNMP GET-NEXT request:
    ///     <code>
    /// String snmpAgent = "10.10.10.1";
    /// String snmpCommunity = "private";
    /// SimpleSnmp snmp = new SimpleSnmp(snmpAgent, snmpCommunity);
    /// // Create a request Pdu
    /// Pdu pdu = new Pdu();
    /// pdu.Type = SnmpConstants.GETNEXT; // type GETNEXT
    /// pdu.VbList.Add("1.3.6.1.2.1.1");
    /// Dictionary&lt;Oid, AsnType&gt; result = snmp.GetNext(pdu);
    /// if( result == null ) {
    ///   Console.WriteLine("Request failed.");
    /// } else {
    ///   foreach (KeyValuePair&lt;Oid, AsnType&gt; entry in result)
    ///   {
    ///     Console.WriteLine("{0} = {1}: {2}", entry.Key.ToString(), SnmpConstants.GetTypeName(entry.Value.Type),
    ///       entry.Value.ToString());
    ///   }
    /// }
    /// </code>
    ///     Will return:
    ///     <code>
    /// 1.3.6.1.2.1.1.1.0 = OctetString: "Dual core Intel notebook"
    /// </code>
    /// </example>
    /// <param name="version">SNMP protocol version.</param>
    /// <param name="pdu">Request Protocol Data Unit</param>
    /// <returns>Result of the SNMP request in a dictionary format with Oid => AsnType values</returns>
    [Obsolete("Use GetNextAsync; this synchronous API blocks the calling thread.")]
    public Dictionary<Oid, AsnType>? GetNext(SnmpVersion version, Pdu pdu)
    {
        if (!ResolveOrThrow()) return null;

        switch (Valid)
        {
            case false:
            {
                return _suppressExceptions switch
                {
                    false => throw new SnmpException("SimpleSnmp class is not valid."),
                    _ => null
                };
            }
        }

        try
        {
            _target = new UdpTarget(_peerIP, _peerPort, _timeout, _retry);
        }
        catch
        {
            _target = null;
        }

        switch (_target)
        {
            case null:
                return null;
        }

        try
        {
            IAgentParameters? param;
            if (version == SnmpVersion.Ver3)
            {
                param = DiscoverSecureAgentParameters();
                switch (param)
                {
                    case null:
                    {
                        if (!_suppressExceptions)
                            throw new SnmpException("SNMP version 3 discovery failed.");
                        return null;
                    }
                }
            }
            else
            {
                param = new AgentParameters(version, new OctetString(_community));
            }

            var result = _target.Request(pdu, param);
            switch (version)
            {
                case SnmpVersion.Ver3 when result is { Pdu.Type: PduType.Report }:
                {
                    var oid = result.Pdu.VbList[0].Oid;
                    if (oid is not null && oid.Equals(SnmpConstants.usmStatsNotInTimeWindows))
                        result = _target.Request(pdu, _secparam!);
                    break;
                }
            }

            if (result != null)
            {
                switch (result.Pdu.ErrorStatus)
                {
                    case 0:
                    {
                        var res = new Dictionary<Oid, AsnType>();
                        foreach (var v in result.Pdu.VbList)
                        {
                            if (v.Value is not null && ((version is SnmpVersion.Ver2 or SnmpVersion.Ver3 &&
                                                         v.Value.Type == SnmpConstants.SMI_ENDOFMIBVIEW) ||
                                                        v.Value.Type == SnmpConstants.SMI_NOSUCHINSTANCE ||
                                                        v.Value.Type == SnmpConstants.SMI_NOSUCHOBJECT))
                                break;

                            if (v.Oid is not null && res.ContainsKey(v.Oid))
                            {
                                if (v.Value is not null && res[v.Oid].Type != v.Value.Type)
                                    throw new SnmpException(SnmpException.OidValueTypeChanged,
                                        "OID value type changed for OID: " + v.Oid);

                                if (v.Value is not null) res[v.Oid] = v.Value;
                            }
                            else
                            {
                                if (v.Oid is not null && v.Value is not null) res.Add(v.Oid, v.Value);
                            }
                        }

                        _target.Close();
                        _target = null;
                        return res;
                    }
                }

                switch (_suppressExceptions)
                {
                    case false:
                        throw new SnmpErrorStatusException("Agent responded with an error", result.Pdu.ErrorStatus,
                            result.Pdu.ErrorIndex);
                }
            }
        }
        catch (Exception)
        {
            switch (_suppressExceptions)
            {
                case false:
                    _target?.Close();
                    _target = null;
                    throw;
            }
        }

        _target?.Close();
        _target = null;
        return null;
    }

    /// <summary>
    ///     SNMP GET-NEXT request
    /// </summary>
    /// <example>
    ///     SNMP GET-NEXT request:
    ///     <code>
    /// String snmpAgent = "10.10.10.1";
    /// String snmpCommunity = "private";
    /// SimpleSnmp snmp = new SimpleSnmp(snmpAgent, snmpCommunity);
    /// Dictionary&lt;Oid, AsnType&gt; result = snmp.GetNext(SnmpVersion.Ver1, new string[] { "1.3.6.1.2.1.1" });
    /// if( result == null ) {
    ///   Console.WriteLine("Request failed.");
    /// } else {
    ///   foreach (KeyValuePair&lt;Oid, AsnType&gt; entry in result)
    ///   {
    ///     Console.WriteLine("{0} = {1}: {2}", entry.Key.ToString(), SnmpConstants.GetTypeName(entry.Value.Type),
    ///       entry.Value.ToString());
    ///   }
    /// }
    /// </code>
    ///     Will return:
    ///     <code>
    /// 1.3.6.1.2.1.1.1.0 = OctetString: "Dual core Intel notebook"
    /// </code>
    /// </example>
    /// <param name="version">SNMP protocol version number.</param>
    /// <param name="oidList">List of request OIDs in string dotted decimal format.</param>
    /// <returns>Result of the SNMP request in a dictionary format with Oid => AsnType values</returns>
    [Obsolete("Use GetNextAsync; this synchronous API blocks the calling thread.")]
    public Dictionary<Oid, AsnType>? GetNext(SnmpVersion version, string[] oidList)
    {
        switch (Valid)
        {
            case false:
            {
                switch (_suppressExceptions)
                {
                    case false:
                        throw new SnmpException("SimpleSnmp class is not valid.");
                    default:
                        return null;
                }
            }
        }

        var pdu = new Pdu(PduType.GetNext);
        foreach (var s in oidList) pdu.VbList.Add(s);

        return GetNext(version, pdu);
    }

    /// <summary>
    ///     SNMP GET-BULK request
    /// </summary>
    /// <remarks>
    ///     GetBulk request type is only available with SNMP v2c and SNMP v3 agents.
    ///     GetBulk method will return a dictionary of Oid to value mapped values as returned form a
    ///     single GetBulk request to the agent. You can change how the request itself is made by changing the
    ///     SimpleSnmp.NonRepeaters and SimpleSnmp.MaxRepetitions values. SimpleSnmp properties are only used
    ///     when values in the parameter Pdu are set to 0.
    /// </remarks>
    /// <example>
    ///     SNMP GET-BULK request:
    ///     <code>
    /// String snmpAgent = "10.10.10.1";
    /// String snmpCommunity = "private";
    /// SimpleSnmp snmp = new SimpleSnmp(snmpAgent, snmpCommunity);
    /// // Create a request Pdu
    /// Pdu pdu = new Pdu();
    /// pdu.Type = SnmpConstants.GETBULK; // type GETBULK
    /// pdu.VbList.Add("1.3.6.1.2.1.1");
    /// pdu.NonRepeaters = 0;
    /// pdu.MaxRepetitions = 10;
    /// Dictionary&lt;Oid, AsnType&gt; result = snmp.GetBulk(pdu);
    /// if( result == null ) {
    ///   Console.WriteLine("Request failed.");
    /// } else {
    ///   foreach (KeyValuePair&lt;Oid, AsnType&gt; entry in result)
    ///   {
    ///     Console.WriteLine("{0} = {1}: {2}", entry.Key.ToString(), SnmpConstants.GetTypeName(entry.Value.Type),
    ///       entry.Value.ToString());
    ///   }
    /// }
    /// </code>
    ///     Will return:
    ///     <code>
    /// 1.3.6.1.2.1.1.1.0 = OctetString: "Dual core Intel notebook"
    /// 1.3.6.1.2.1.1.2.0 = ObjectId: 1.3.6.1.9.233233.1.1
    /// 1.3.6.1.2.1.1.3.0 = TimeTicks: 0d 0h 0m 1s 420ms
    /// 1.3.6.1.2.1.1.4.0 = OctetString: "msinadinovic@users.sourceforge.net"
    /// 1.3.6.1.2.1.1.5.0 = OctetString: "milans-nbook"
    /// 1.3.6.1.2.1.1.6.0 = OctetString: "Developer home"
    /// 1.3.6.1.2.1.1.8.0 = TimeTicks: 0d 0h 0m 0s 10ms
    /// </code>
    /// </example>
    /// <param name="pdu">Request Protocol Data Unit</param>
    /// <param name="version">SNMP protocol version number.</param>
    /// <returns>Result of the SNMP request in a dictionary format with Oid => AsnType values</returns>
    [Obsolete("Use GetBulkAsync; this synchronous API blocks the calling thread.")]
    public Dictionary<Oid, AsnType>? GetBulk(Pdu pdu, SnmpVersion version = SnmpVersion.Ver2)
    {
        if (!ResolveOrThrow()) return null;

        switch (Valid)
        {
            case false:
            {
                switch (_suppressExceptions)
                {
                    case false:
                        throw new SnmpException("SimpleSnmp class is not valid.");
                    default:
                        return null; // class is not fully initialized.
                }
            }
        }

        switch (version)
        {
            // function only works on SNMP version 2 and SNMP version 3 requests
            case SnmpVersion.Ver1:
            {
                switch (_suppressExceptions)
                {
                    case false:
                        throw new SnmpInvalidVersionException("GetBulk request not support SNMP version 1.");
                    default:
                        return null;
                }
            }
        }

        try
        {
            pdu.NonRepeaters = _nonRepeaters;
            pdu.MaxRepetitions = _maxRepetitions;
            _target = new UdpTarget(_peerIP, _peerPort, _timeout, _retry);
        }
        catch (Exception)
        {
            _target = null;
            switch (_suppressExceptions)
            {
                case false:
                    throw;
            }
        }

        switch (_target)
        {
            case null:
                return null;
        }

        try
        {
            IAgentParameters? param;
            if (version == SnmpVersion.Ver3)
            {
                param = DiscoverSecureAgentParameters();
                switch (param)
                {
                    case null:
                    {
                        return _suppressExceptions switch
                        {
                            false => throw new SnmpException("SNMP version 3 discovery failed."),
                            _ => null
                        };
                    }
                }
            }
            else
            {
                param = new AgentParameters(version, new OctetString(_community));
            }

            var result = _target.Request(pdu, param);
            if (version == SnmpVersion.Ver3 && result is { Pdu.Type: PduType.Report })
            {
                var oid = result.Pdu.VbList[0].Oid;
                if (oid is not null && oid.Equals(SnmpConstants.usmStatsNotInTimeWindows))
                    result = _target.Request(pdu, _secparam!);
            }

            if (result != null)
            {
                switch (result.Pdu.ErrorStatus)
                {
                    case 0:
                    {
                        var res = new Dictionary<Oid, AsnType>();
                        foreach (var v in result.Pdu.VbList)
                        {
                            if (
                                v.Value is
                                {
                                    Type: SnmpConstants.SMI_ENDOFMIBVIEW
                                    or SnmpConstants.SMI_NOSUCHINSTANCE
                                    or SnmpConstants.SMI_NOSUCHOBJECT
                                }
                            )
                                break;

                            if (v.Oid is not null && res.ContainsKey(v.Oid))
                            {
                                if (v.Value is not null && res[v.Oid].Type != v.Value.Type)
                                    throw new SnmpException(SnmpException.OidValueTypeChanged,
                                        "OID value type changed for OID: " + v.Oid);

                                if (v.Value is not null) res[v.Oid] = v.Value;
                            }
                            else
                            {
                                if (v.Oid is not null && v.Value != null) res.Add(v.Oid, v.Value);
                            }
                        }

                        _target.Close();
                        _target = null;
                        return res;
                    }
                }

                switch (_suppressExceptions)
                {
                    case false:
                        throw new SnmpErrorStatusException("Agent responded with an error", result.Pdu.ErrorStatus,
                            result.Pdu.ErrorIndex);
                }
            }
        }
        catch (Exception)
        {
            switch (_suppressExceptions)
            {
                case false:
                    _target?.Close();
                    _target = null;
                    throw;
            }
        }

        _target?.Close();
        _target = null;
        return null;
    }

    /// <summary>
    ///     SNMP GET-BULK request
    /// </summary>
    /// <remarks>
    ///     Performs a GetBulk SNMP v2 operation on a list of OIDs. This is a convenience function that
    ///     calls GetBulk(Pdu) method.
    /// </remarks>
    /// <example>
    ///     SNMP GET-BULK request:
    ///     <code>
    /// String snmpAgent = "10.10.10.1";
    /// String snmpCommunity = "private";
    /// SimpleSnmp snmp = new SimpleSnmp(snmpAgent, snmpCommunity);
    /// Dictionary&lt;Oid, AsnType&gt; result = snmp.GetBulk(new string[] { "1.3.6.1.2.1.1" });
    /// if( result == null ) {
    ///   Console.WriteLine("Request failed.");
    /// } else {
    ///   foreach (KeyValuePair&lt;Oid, AsnType&gt; entry in result)
    ///   {
    ///     Console.WriteLine("{0} = {1}: {2}", entry.Key.ToString(), SnmpConstants.GetTypeName(entry.Value.Type),
    ///       entry.Value.ToString());
    ///   }
    /// }
    /// </code>
    /// </example>
    /// <param name="oidList">List of request OIDs in string dotted decimal format.</param>
    /// <param name="version">SNMP protocol version number.</param>
    /// <returns>Result of the SNMP request in a dictionary format with Oid => AsnType values</returns>
    [Obsolete("Use GetBulkAsync; this synchronous API blocks the calling thread.")]
    public Dictionary<Oid, AsnType>? GetBulk(string[] oidList, SnmpVersion version = SnmpVersion.Ver2)
    {
        switch (Valid)
        {
            case false:
            {
                return _suppressExceptions switch
                {
                    false => throw new SnmpException("SimpleSnmp class is not valid."),
                    _ => null
                };
            }
        }

        var pdu = new Pdu(PduType.GetBulk)
        {
            MaxRepetitions = _maxRepetitions,
            NonRepeaters = _nonRepeaters
        };
        foreach (var s in oidList) pdu.VbList.Add(s);

        return GetBulk(pdu, version);
    }

    /// <summary>
    ///     SNMP SET request
    /// </summary>
    /// <example>
    ///     Set operation in SNMP version 1:
    ///     <code>
    /// String snmpAgent = "10.10.10.1";
    /// String snmpCommunity = "private";
    /// SimpleSnmp snmp = new SimpleSnmp(snmpAgent, snmpCommunity);
    /// // Create a request Pdu
    /// Pdu pdu = new Pdu();
    /// pdu.Type = SnmpConstants.SET; // type SET
    /// Oid setOid = new Oid("1.3.6.1.2.1.1.1.0"); // sysDescr.0
    /// OctetString setValue = new OctetString("My personal toy");
    /// pdu.VbList.Add(setOid, setValue);
    /// Dictionary&lt;Oid, AsnType&gt; result = snmp.Set(SnmpVersion.Ver1, pdu);
    /// if( result == null ) {
    ///   Console.WriteLine("Request failed.");
    /// } else {
    ///   Console.WriteLine("Success!");
    /// }
    /// </code>
    ///     To use SNMP version 2, change snmp.Set() method call first parameter to SnmpVersion.Ver2.
    /// </example>
    /// <param name="version">SNMP protocol version number.</param>
    /// <param name="pdu">Request Protocol Data Unit</param>
    /// <returns>Result of the SNMP request in a dictionary format with Oid => AsnType values</returns>
    [Obsolete("Use SetAsync; this synchronous API blocks the calling thread.")]
    public Dictionary<Oid, AsnType>? Set(SnmpVersion version, Pdu pdu)
    {
        if (!ResolveOrThrow()) return null;

        switch (Valid)
        {
            case false:
            {
                return _suppressExceptions switch
                {
                    false => throw new SnmpException("SimpleSnmp class is not valid."),
                    _ => null
                };
            }
        }

        try
        {
            _target = new UdpTarget(_peerIP, _peerPort, _timeout, _retry);
        }
        catch (Exception)
        {
            _target = null;
            switch (_suppressExceptions)
            {
                case false:
                    throw;
            }
        }

        switch (_target)
        {
            case null:
                return null;
        }

        try
        {
            IAgentParameters? param;
            switch (version)
            {
                case SnmpVersion.Ver3:
                {
                    param = DiscoverSecureAgentParameters();
                    switch (param)
                    {
                        case null:
                        {
                            switch (_suppressExceptions)
                            {
                                case false:
                                    throw new SnmpException("SNMP version 3 discovery failed.");
                                default:
                                    return null;
                            }
                        }
                    }

                    break;
                }
                default:
                    param = new AgentParameters(version, new OctetString(_community));
                    break;
            }

            var result = _target.Request(pdu, param);
            if (version == SnmpVersion.Ver3 && result is { Pdu.Type: PduType.Report })
            {
                var oid = result.Pdu.VbList[0].Oid;
                if (oid is not null && oid.Equals(SnmpConstants.usmStatsNotInTimeWindows))
                    result = _target.Request(pdu, _secparam!);
            }

            if (result != null)
            {
                switch (result.Pdu.ErrorStatus)
                {
                    case 0:
                    {
                        var res = new Dictionary<Oid, AsnType>();
                        foreach (var v in result.Pdu.VbList)
                            if (v.Oid is not null && res.ContainsKey(v.Oid))
                            {
                                if (v.Value is not null && res[v.Oid].Type != v.Value.Type)
                                    throw new SnmpException(SnmpException.OidValueTypeChanged,
                                        "OID value type changed for OID: " + v.Oid);

                                if (v.Value is not null) res[v.Oid] = v.Value;
                            }
                            else
                            {
                                if (v.Oid is null) continue;
                                if (v.Value is null) continue;
                                res.Add(v.Oid, v.Value);
                            }

                        _target.Close();
                        _target = null;
                        return res;
                    }
                }

                switch (_suppressExceptions)
                {
                    case false:
                        throw new SnmpErrorStatusException("Agent responded with an error", result.Pdu.ErrorStatus,
                            result.Pdu.ErrorIndex);
                }
            }
        }
        catch (Exception)
        {
            switch (_suppressExceptions)
            {
                case false:
                    _target?.Close();
                    _target = null;
                    throw;
            }
        }

        _target?.Close();
        _target = null;
        return null;
    }

    /// <summary>
    ///     SNMP SET request
    /// </summary>
    /// <example>
    ///     Set operation in SNMP version 1:
    ///     <code>
    /// String snmpAgent = "10.10.10.1";
    /// String snmpCommunity = "private";
    /// SimpleSnmp snmp = new SimpleSnmp(snmpAgent, snmpCommunity);
    /// // Create a request Pdu
    /// List&lt;Vb&gt; vbList = new List&lt;Vb&gt;();
    /// Oid setOid = new Oid("1.3.6.1.2.1.1.1.0"); // sysDescr.0
    /// OctetString setValue = new OctetString("My personal toy");
    /// vbList.Add(new Vb(setOid, setValue));
    /// Dictionary&lt;Oid, AsnType&gt; result = snmp.Set(SnmpVersion.Ver1, list.ToArray());
    /// if( result == null ) {
    ///   Console.WriteLine("Request failed.");
    /// } else {
    ///   Console.WriteLine("Success!");
    /// }
    /// </code>
    ///     To use SNMP version 2, change snmp.Set() method call first parameter to SnmpVersion.Ver2.
    /// </example>
    /// <param name="version">SNMP protocol version number.</param>
    /// <param name="vbs">Vb array containing Oid/AsnValue pairs for the SET operation</param>
    /// <returns>Result of the SNMP request in a dictionary format with Oid => AsnType values</returns>
    [Obsolete("Use SetAsync; this synchronous API blocks the calling thread.")]
    public Dictionary<Oid, AsnType>? Set(SnmpVersion version, Vb[] vbs)
    {
        if (!Valid)
        {
            if (!_suppressExceptions)
                throw new SnmpException("SimpleSnmp class is not valid.");
            return null;
        }

        var pdu = new Pdu(PduType.Set);
        foreach (var vb in vbs) pdu.VbList.Add(vb);

        return Set(version, pdu);
    }

    /// <summary>SNMP WALK operation</summary>
    /// <remarks>
    ///     When using SNMP version 1, walk is performed using GET-NEXT calls. When using SNMP version 2,
    ///     walk is performed using GET-BULK calls.
    /// </remarks>
    /// <example>
    ///     Example SNMP walk operation using SNMP version 1:
    ///     <code>
    /// String snmpAgent = "10.10.10.1";
    /// String snmpCommunity = "private";
    /// SimpleSnmp snmp = new SimpleSnmp(snmpAgent, snmpCommunity);
    /// Dictionary&lt;Oid, AsnType&gt; result = snmp.Walk(SnmpVersion.Ver1, "1.3.6.1.2.1.1");
    /// if( result == null ) {
    ///   Console.WriteLine("Request failed.");
    /// } else {
    /// foreach (KeyValuePair&lt;Oid, AsnType&gt; entry in result)
    /// {
    ///   Console.WriteLine("{0} = {1}: {2}", entry.Key.ToString(), SnmpConstants.GetTypeName(entry.Value.Type),
    ///     entry.Value.ToString());
    /// }
    /// </code>
    ///     Will return:
    ///     <code>
    /// 1.3.6.1.2.1.1.1.0 = OctetString: "Dual core Intel notebook"
    /// 1.3.6.1.2.1.1.2.0 = ObjectId: 1.3.6.1.9.233233.1.1
    /// 1.3.6.1.2.1.1.3.0 = TimeTicks: 0d 0h 0m 1s 420ms
    /// 1.3.6.1.2.1.1.4.0 = OctetString: "msinadinovic@users.sourceforge.net"
    /// 1.3.6.1.2.1.1.5.0 = OctetString: "milans-nbook"
    /// 1.3.6.1.2.1.1.6.0 = OctetString: "Developer home"
    /// 1.3.6.1.2.1.1.8.0 = TimeTicks: 0d 0h 0m 0s 10ms
    /// </code>
    ///     To use SNMP version 2, change snmp.Set() method call first parameter to SnmpVersion.Ver2.
    /// </example>
    /// <param name="version">SNMP protocol version.</param>
    /// <param name="rootOid">
    ///     OID to start WALK operation from. Only child OIDs of the rootOid will be
    ///     retrieved and returned
    /// </param>
    /// <returns>
    ///     Oid => AsnType value mappings on success, empty dictionary if no data was found or
    ///     null on error
    /// </returns>
    [Obsolete("Use WalkAsync; this synchronous API blocks the calling thread.")]
    public Dictionary<Oid, AsnType>? Walk(SnmpVersion version, string rootOid)
    {
        if (!Valid)
        {
            return _suppressExceptions switch
            {
                false => throw new SnmpException("SimpleSnmp class is not valid."),
                _ => null
            };
        }

        if (rootOid.Length < 2)
        {
            return _suppressExceptions switch
            {
                false => throw new SnmpException(SnmpException.InvalidOid, "RootOid is not a valid Oid"),
                _ => null
            };
        }

        var root = new Oid(rootOid);
        switch (root.Length)
        {
            case <= 0:
                return null; // unable to parse root oid
        }

        var lastOid = root.Clone() as Oid;

        var result = new Dictionary<Oid, AsnType>();
        while (lastOid is not null && root.IsRootOf(lastOid))
        {
            Dictionary<Oid, AsnType>? val;
            if (version == SnmpVersion.Ver1)
                val = GetNext(version, [lastOid.ToString()]);
            else
                val = GetBulk([lastOid.ToString()], version);

            // check that we have a result
            if (val == null || val.Count == 0)
                // error of some sort happened. abort...
                break;

            foreach (var entry in val)
                if (root.IsRootOf(entry.Key))
                {
                    if (result.ContainsKey(entry.Key))
                    {
                        if (result[entry.Key].Type != entry.Value.Type)
                            throw new SnmpException(SnmpException.OidValueTypeChanged,
                                "OID value type changed for OID: " + entry.Key);

                        result[entry.Key] = entry.Value;
                    }
                    else
                    {
                        result.Add(entry.Key, entry.Value);
                    }

                    lastOid = (Oid)entry.Key.Clone();
                }
                else
                {
                    // it's faster to check if variable is null then checking IsRootOf
                    lastOid = null;
                    break;
                }
        }

        return result;
    }

    /// <summary>Performs an SNMP GET request asynchronously.</summary>
    public Task<Dictionary<Oid, AsnType>> GetAsync(SnmpVersion version, Pdu pdu,
        CancellationToken cancellationToken = default) =>
        RequestDictionaryAsync(version, pdu, cancellationToken);

    /// <summary>Performs an SNMP GET request asynchronously for the specified OIDs.</summary>
    public Task<Dictionary<Oid, AsnType>> GetAsync(SnmpVersion version, string[] oidList,
        CancellationToken cancellationToken = default) =>
        RequestDictionaryAsync(version, CreatePdu(PduType.Get, oidList), cancellationToken);

    /// <summary>Performs an SNMP GET-NEXT request asynchronously.</summary>
    public Task<Dictionary<Oid, AsnType>> GetNextAsync(SnmpVersion version, Pdu pdu,
        CancellationToken cancellationToken = default) =>
        RequestDictionaryAsync(version, pdu, cancellationToken);

    /// <summary>Performs an SNMP GET-NEXT request asynchronously for the specified OIDs.</summary>
    public Task<Dictionary<Oid, AsnType>> GetNextAsync(SnmpVersion version, string[] oidList,
        CancellationToken cancellationToken = default) =>
        RequestDictionaryAsync(version, CreatePdu(PduType.GetNext, oidList), cancellationToken);

    /// <summary>Performs an SNMP GET-BULK request asynchronously.</summary>
    public async Task<Dictionary<Oid, AsnType>> GetBulkAsync(Pdu pdu, SnmpVersion version = SnmpVersion.Ver2,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdu);
        if (version == SnmpVersion.Ver1)
            throw new SnmpInvalidVersionException("GetBulk request not support SNMP version 1.");

        ApplyBulkOptions(pdu);
        return await RequestDictionaryAsync(version, pdu, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Performs an SNMP GET-BULK request asynchronously for the specified OIDs.</summary>
    public Task<Dictionary<Oid, AsnType>> GetBulkAsync(string[] oidList, SnmpVersion version = SnmpVersion.Ver2,
        CancellationToken cancellationToken = default) =>
        GetBulkAsync(CreatePdu(PduType.GetBulk, oidList), version, cancellationToken);

    /// <summary>Performs an SNMP SET request asynchronously.</summary>
    public Task<Dictionary<Oid, AsnType>> SetAsync(SnmpVersion version, Pdu pdu,
        CancellationToken cancellationToken = default) =>
        RequestDictionaryAsync(version, pdu, cancellationToken);

    /// <summary>Performs an SNMP SET request asynchronously for the specified variable bindings.</summary>
    public Task<Dictionary<Oid, AsnType>> SetAsync(SnmpVersion version, Vb[] variableBindings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(variableBindings);
        var pdu = new Pdu(PduType.Set);
        foreach (var binding in variableBindings)
            pdu.VbList.Add(binding);
        return SetAsync(version, pdu, cancellationToken);
    }

    /// <summary>Walks an OID subtree asynchronously, yielding each returned binding as it arrives.</summary>
    /// <remarks>The walk ends normally at the subtree boundary or the end of the MIB.</remarks>
    /// <exception cref="SnmpException">The agent returned a non-increasing OID.</exception>
    public async IAsyncEnumerable<KeyValuePair<Oid, AsnType>> WalkAsync(SnmpVersion version, string rootOid,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rootOid);
        if (!Valid)
            throw new SnmpException("SimpleSnmp class is not valid.");
        if (rootOid.Length < 2)
            throw new SnmpException(SnmpException.InvalidOid, "RootOid is not a valid Oid");

        var root = new Oid(rootOid);

        if (root.Length <= 0)
            throw new SnmpException(SnmpException.InvalidOid, "RootOid is not a valid Oid");

        var lastOid = root;
        var requestType = version == SnmpVersion.Ver1 ? PduType.GetNext : PduType.GetBulk;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = new Pdu(requestType);
            request.VbList.Add(lastOid);
            if (requestType == PduType.GetBulk)
                ApplyBulkOptions(request);

            var packet = await SendRequestAsync(version, request, cancellationToken).ConfigureAwait(false);
            var response = packet.Pdu;
            // SNMPv1 GETNEXT signals the end of the MIB with noSuchName for the requested binding.
            if (version == SnmpVersion.Ver1 && response.ErrorStatus == SnmpConstants.ErrNoSuchName &&
                response.ErrorIndex == 1)
                yield break;
            ThrowIfAgentError(response);

            var advanced = false;
            foreach (var binding in response.VbList)
            {
                if (binding.Oid is not { } oid || binding.Value is not { } value)
                    continue;
                if (IsTraversalEnd(value, version, requestType) || !root.IsRootOf(oid))
                    yield break;
                if (oid.CompareTo(lastOid) <= 0)
                    throw new SnmpException("Agent returned a non-increasing OID during a walk.");

                lastOid = new Oid(oid);
                advanced = true;
                yield return new KeyValuePair<Oid, AsnType>(oid, value);
            }

            if (!advanced)
                yield break;
        }
    }

    private void ApplyBulkOptions(Pdu pdu)
    {
        pdu.NonRepeaters = _nonRepeaters;
        pdu.MaxRepetitions = _maxRepetitions;
    }

    private static bool IsTraversalEnd(AsnType value, SnmpVersion version, PduType requestType) =>
        (requestType is PduType.GetNext or PduType.GetBulk) &&
        (value.Type is SnmpConstants.SMI_NOSUCHINSTANCE or SnmpConstants.SMI_NOSUCHOBJECT ||
         value.Type == SnmpConstants.SMI_ENDOFMIBVIEW &&
         (requestType == PduType.GetBulk || version is SnmpVersion.Ver2 or SnmpVersion.Ver3));

    private static void ThrowIfAgentError(Pdu response)
    {
        if (response.ErrorStatus != 0)
            throw new SnmpErrorStatusException("Agent responded with an error", response.ErrorStatus,
                response.ErrorIndex);
    }

    private static Pdu CreatePdu(PduType type, string[] oidList)
    {
        ArgumentNullException.ThrowIfNull(oidList);
        var pdu = new Pdu(type);
        foreach (var oid in oidList)
            pdu.VbList.Add(oid);
        return pdu;
    }

    private async Task<Dictionary<Oid, AsnType>> RequestDictionaryAsync(SnmpVersion version, Pdu pdu,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pdu);
        var packet = await SendRequestAsync(version, pdu, cancellationToken).ConfigureAwait(false);
        ThrowIfAgentError(packet.Pdu);

        var result = new Dictionary<Oid, AsnType>();
        foreach (var vb in packet.Pdu.VbList)
        {
            if (vb.Oid is null || vb.Value is null)
                continue;

            if (pdu.Type == PduType.Get &&
                (version is SnmpVersion.Ver2 or SnmpVersion.Ver3) &&
                (vb.Value.Type is SnmpConstants.SMI_NOSUCHINSTANCE or SnmpConstants.SMI_NOSUCHOBJECT))
            {
                if (!result.ContainsKey(vb.Oid))
                    result.Add(vb.Oid, new Null());
                else
                    result.Add(Oid.NullOid(), vb.Value);
                continue;
            }

            if (IsTraversalEnd(vb.Value, version, pdu.Type))
                break;

            if (result.TryGetValue(vb.Oid, out var previous))
            {
                if (previous.Type != vb.Value.Type)
                    throw new SnmpException(SnmpException.OidValueTypeChanged,
                        $"Value type changed from {previous.Type} to {vb.Value.Type}");
                result[vb.Oid] = vb.Value;
            }
            else
            {
                result.Add(vb.Oid, vb.Value);
            }
        }

        return result;
    }

    private async Task<SnmpPacket> SendRequestAsync(SnmpVersion version, Pdu pdu,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Valid)
            throw new SnmpException("SimpleSnmp class is not valid.");

        if (!await ResolvePeerAddressAsync(cancellationToken).ConfigureAwait(false))
            throw new SnmpException("Unable to resolve the SNMP agent address.");

        using var target = new UdpTarget(_peerIP, _peerPort, _timeout, _retry);
        IAgentParameters parameters = version == SnmpVersion.Ver3
            ? await DiscoverSecureAgentParametersAsync(target, cancellationToken).ConfigureAwait(false)
            : new AgentParameters(version, new OctetString(_community));

        var result = await target.RequestAsync(pdu, parameters, cancellationToken).ConfigureAwait(false);
        if (result is SnmpV3Packet { Pdu.Type: PduType.Report, Pdu.VbCount: > 0 } &&
            parameters is SecureAgentParameters { EngineId.Length: > 0 } &&
            result.Pdu.VbList[0].Oid?.Equals(SnmpConstants.usmStatsNotInTimeWindows) == true)
            result = await target.RequestAsync(pdu, parameters, cancellationToken).ConfigureAwait(false);

        if (result is SnmpV3Packet { Pdu.Type: PduType.Report } report)
        {
            var reason = SNMPV3ReportError.TranslateError(report);
            throw new SnmpException(reason.Length == 0
                ? "Agent returned an SNMPv3 report instead of a response."
                : $"Agent returned an SNMPv3 report: {reason}");
        }

        return result;
    }

    #region Properties

    /// <summary>
    ///     Get/Set peer IP address
    /// </summary>
    public IPAddress PeerIP
    {
        get => _peerIP;
        set => _peerIP = value;
    }

    /// <summary>
    ///     Get/Set peer name
    /// </summary>
    public string PeerName
    {
        get => _peerName;
        set
        {
            _peerName = value;
            _peerIP = IPAddress.TryParse(value, out var address) ? address : IPAddress.None;
        }
    }

    /// <summary>
    ///     Get/Set peer port number
    /// </summary>
    public int PeerPort
    {
        get => _peerPort;
        set => _peerPort = value;
    }

    /// <summary>
    ///     Get set timeout value in milliseconds
    /// </summary>
    public int Timeout
    {
        get => _timeout;
        set => _timeout = value;
    }

    /// <summary>
    ///     Get/Set maximum retry count
    /// </summary>
    public int Retry
    {
        get => _retry;
        set => _retry = value;
    }

    /// <summary>
    ///     Get/Set SNMP community name
    /// </summary>
    public string Community
    {
        get => _community;
        set => _community = value;
    }

    /// <summary>
    ///     Get/Set NonRepeaters value
    /// </summary>
    /// <remarks>
    ///     NonRepeaters value will only be used by SNMPv2 GET-BULK requests. Any other
    ///     request type will ignore this value.
    /// </remarks>
    public int NonRepeaters
    {
        get => _nonRepeaters;
        set => _nonRepeaters = value;
    }

    /// <summary>
    ///     Get/Set MaxRepetitions value
    /// </summary>
    /// <remarks>
    ///     MaxRepetitions value will only be used by SNMPv2 GET-BULK requests. Any other
    ///     request type will ignore this value.
    /// </remarks>
    public int MaxRepetitions
    {
        get => _maxRepetitions;
        set => _maxRepetitions = value;
    }

    #endregion

    #region Utility functions

    /// <summary>Resolves the configured peer name asynchronously.</summary>
    /// <param name="cancellationToken">Cancels DNS resolution.</param>
    /// <returns>True when the peer name resolves to a usable address; false for invalid or unresolved names.</returns>
    /// <exception cref="OperationCanceledException">The caller canceled resolution.</exception>
    public async Task<bool> ResolveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await ResolvePeerAddressAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SocketException or ArgumentException)
        {
            return false;
        }
    }

    private async Task<bool> ResolvePeerAddressAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_peerIP is not null && !_peerIP.Equals(IPAddress.None) && !_peerIP.Equals(IPAddress.Any))
            return true;
        if (string.IsNullOrWhiteSpace(_peerName))
            return false;

        if (IPAddress.TryParse(_peerName, out var parsed))
        {
            _peerIP = parsed;
            return !_peerIP.Equals(IPAddress.None) && !_peerIP.Equals(IPAddress.Any);
        }

        _peerIP = IPAddress.None;
        var addresses = await Dns.GetHostAddressesAsync(_peerName, cancellationToken).ConfigureAwait(false);
        foreach (var address in addresses)
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                _peerIP = address;
                return true;
            }

        if (addresses.Length > 0)
        {
            _peerIP = addresses[0];
            return true;
        }

        return false;
    }

    private bool ResolveOrThrow()
    {
        if (_suppressExceptions)
            return ResolveAsync().GetAwaiter().GetResult();
        if (ResolvePeerAddressAsync(CancellationToken.None).GetAwaiter().GetResult())
            return true;
        throw new SnmpException("Unable to resolve the SNMP agent address.");
    }

    /// <summary>
    ///     Legacy exception suppression flag for synchronous operations. Async SNMP operations report failures
    ///     as exceptions regardless of this setting.
    /// </summary>
    [Obsolete("Async SNMP operations report failures as exceptions; use try/catch instead.")]
    public bool SuppressExceptions
    {
        get => _suppressExceptions;
        set => _suppressExceptions = value;
    }

    #endregion
}