// This file is part of SNMP#NET.
// 
// SNMP#NET is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser Public License as published by
// the Free Software Foundation, either version 3 of the License or
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
using System.Buffers;
using System.Text;

namespace SnmpSharpNet;

/// <summary>
///     SNMP version 3 packet implementation class.
/// </summary>
/// <remarks>
///     Available packet classes are:
///     <ul>
///         <li>
///             <see cref="SnmpV1Packet" />
///         </li>
///         <li>
///             <see cref="SnmpV1TrapPacket" />
///         </li>
///         <li>
///             <see cref="SnmpV2Packet" />
///         </li>
///         <li>
///             <see cref="SnmpV3Packet" />
///         </li>
///     </ul>
///     This class is provided to simplify encoding and decoding of packets and to provide consistent interface
///     for users who wish to handle transport part of protocol on their own without using the <see cref="UdpTarget" />
///     class.
///     <see cref="SnmpPacket" /> and derived classes have been developed to implement SNMP version 1, 2 and 3 packet
///     support.
///     For SNMP version 1 and 2 packet, <see cref="SnmpV1Packet" /> and <see cref="SnmpV2Packet" /> classes
///     provide sufficient support for encoding and decoding data to/from BER buffers to satisfy requirements
///     of most applications.
///     SNMP version 3 on the other hand requires a lot more information to be passed to the encoder method and
///     returned by the decode method. While using SnmpV3Packet class for full packet handling is possible, transport
///     specific class <see cref="UdpTarget" /> uses <see cref="SecureAgentParameters" /> class to store protocol
///     version 3 specific information that carries over from request to request when used on the same SNMP agent
///     and therefore simplifies both initial definition of agents configuration (mostly security) and
///     removes the need for repeated initialization of the packet class for subsequent requests.
///     If you decide not to use transport helper class(es) like <see cref="UdpTarget" />, BER encoding and
///     decoding and packets is easily done with SnmpPacket derived classes.
///     Example, SNMP version 1 packet encoding:
///     <code>
///  SnmpV1Packet packetv1 = new SnmpV1Packet();
///  packetv1.Community.Set("public");
///  packetv1.Pdu.Set(mypdu);
///  byte[] berpacket = packetv1.encode();
///  </code>
///     Example, SNMP version 3 noAuthNoPriv encoding:
///     <code>
///  SnmpV3Packet packetv3 = new SnmpV3Packet();
///  packetv3.noAuthNoPriv("myusername");
///  packetv3.SetEngineTime(engineTime, engineBoots); // See SNMPv3 discovery process for details
///  packetv3.SetEngineId(engineId); // See SNMPv3 discovery process for details
///  packetv3.IsReportable = true;
///  packetv3.Pdu.Set(mypdu);
///  byte[] berpacket = packetv3.encode();
///  </code>
///     Example, SNMP version 3 authNoPriv using MD5 authentication packet encoding:
///     <code>
///  SnmpV3Packet packetv3 = new SnmpV3Packet();
///  packetv3.authNoPriv("myusername", "myAuthenticationPassword", AuthenticationDigests.MD5);
///  packetv3.SetEngineTime(engineTime, engineBoots); // See SNMPv3 discovery process for details
///  packetv3.SetEngineId(engineId); // See SNMPv3 discovery process for details
///  packetv3.IsReportable = true;
///  packetv3.Pdu.Set(mypdu);
///  byte[] berpacket = packetv3.encode();
///  </code>
///     Example, SNMP version 3 authPriv using MD5 authentication and DES encryption packet encoding:
///     <code>
///  SnmpV3Packet packetv3 = new SnmpV3Packet();
///  packetv3.authPriv("myusername", "myAuthenticationPassword", AuthenticationDigests.MD5, "myPrivacyPassword", PrivacyProtocols.DES);
///  packetv3.SetEngineTime(engineTime, engineBoots); // See SNMPv3 discovery process for details
///  packetv3.SetEngineId(engineId); // See SNMPv3 discovery process for details
///  packetv3.IsReportable = true;
///  packetv3.Pdu.Set(mypdu);
///  byte[] berpacket = packetv3.encode();
///  </code>
///     When decoding SNMP version 3 packets, SnmpV3Packet class needs to be initialized with the same values
///     security values as a request does. This includes authoritative engine id, engine boots and engine time,
///     if authentication is used, authentication digest and password and for encryption, password and privacy
///     protocol used. Without these parameters packet class will not be able to verify the incoming packet and
///     responses will be discarded even if they are valid.
/// </remarks>
public class SnmpV3Packet : SnmpPacket, IEquatable<SnmpV3Packet>
{
    private const int StackAllocThreshold = 1024;

    /// <summary>
    ///     Maximum message size. In the discovery packet, set it to the maximum acceptable size = 64KB. The Agent will
    ///     return the maximum value it is ready to handle, so you should stick with that value in all following
    ///     requests.
    /// </summary>
    private readonly Integer32 _maxMessageSize;

    /// <summary>
    ///     SNMP version 3 message id. Uniquly identifies the message.
    /// </summary>
    private readonly Integer32 _messageId;

    /// <summary>
    ///     Security model code. The only supported security model is UserSecurityModel (integer value 3)
    /// </summary>
    private readonly Integer32 _securityModel;

    /// <summary>
    ///     Standard constructor.
    /// </summary>
    public SnmpV3Packet()
        : base(SnmpVersion.Ver3)
    {
        _messageId = new Integer32(Random.Shared.Next(1, int.MaxValue));
        _maxMessageSize = new Integer32(64 * 1024);
        MsgFlags = new MsgFlags
        {
            Reportable = true // Make sure reportable is set to true by default
        };
        _securityModel = new Integer32();
        USM = new UserSecurityModel();
        ScopedPdu = new ScopedPdu();
    }

    /// <summary>
    ///     Constructor.
    /// </summary>
    /// <remarks>
    ///     Sets internal ScopedPdu class to the argument supplied instance of the
    ///     class. This is a good cheat that will allow you direct access to the internal ScopedPdu class
    ///     since it is not cloned but assigned to the internal variable.
    /// </remarks>
    /// <param name="pdu"><see cref="ScopedPdu" /> class assigned to the class</param>
    /// <param name="parameters"></param>
    public SnmpV3Packet(ScopedPdu? pdu, SecureAgentParameters? parameters = null)
        : this()
    {
        if (pdu != null)
            ScopedPdu = pdu;
        parameters?.InitializePacket(this);
    }

    /// <summary>
    ///     Constructor.
    /// </summary>
    /// <remarks>
    ///     Create new SNMPv3 packet class and initialize security parameters
    /// </remarks>
    /// <param name="param">Initialization SNMPv3 security parameters</param>
    public SnmpV3Packet(SecureAgentParameters? param)
        : this()
    {
        param?.InitializePacket(this);
    }

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <remarks>
    ///     Create new SNMPv3 packet class and initialize security parameters and ScopedPdu.
    /// </remarks>
    /// <param name="param">SNMPv3 security parameters</param>
    /// <param name="pdu">ScopedPdu assigned to the class</param>
    public SnmpV3Packet(SecureAgentParameters param, ScopedPdu? pdu)
        : this(param)
    {
        if (pdu != null)
            ScopedPdu = pdu;
    }

    public SnmpV3Packet(ReadOnlySpan<byte> encodedForm, ReadOnlySpan<byte> authKey, ReadOnlySpan<byte> privKey) : this()
    {
        Decode(encodedForm, authKey, privKey);
    }

    public SnmpV3Packet(ReadOnlySpan<byte> encodedForm) : this(encodedForm, ReadOnlySpan<byte>.Empty,
        ReadOnlySpan<byte>.Empty)
    {
    }

    public SnmpV3Packet(ReadOnlySpan<byte> encodedForm, SecureAgentParameters parameters) : this()
    {
        parameters.InitializePacket(this);
        if (parameters.HasCachedKeys)
        {
            Decode(encodedForm, parameters.AuthenticationKey, parameters.PrivacyKey);
        }
        else
        {
            Decode(encodedForm);
        }
    }

    /// <summary>
    ///     Get SNMP version 3 message id object.
    /// </summary>
    public int MessageId
    {
        get => _messageId.Value;
        set => _messageId.Value = value;
    }

    /// <summary>
    ///     Get the maximum message size to be sent to the agent in the request.
    /// </summary>
    public int MaxMessageSize
    {
        get => _maxMessageSize.Value;
        set => _maxMessageSize.Value = value;
    }

    public override string ToString()
    {
        var str = new StringBuilder();
        str.AppendLine("SNMPv3 packet:");
        str.AppendLine($"Version: {_protocolVersion}");
        str.AppendLine($"MsgId: {_messageId}");
        str.AppendLine($"MaxMsgSize: {MaxMessageSize}");
        str.AppendLine($"MsgFlags: {MsgFlags}");
        str.AppendLine($"SecurityModel: {_securityModel}");
        str.AppendLine($"USM: {USM}");
        str.AppendLine($"ScopedPdu: {ScopedPdu}");
        return str.ToString();
    }

    /// <summary>
    ///     Message flags interface. Allows you to directly set or clear SNMP version 3 header flags field.
    ///     Available flags are MsgFlags.Authentication, MsgFlags.Privacy and MsgFlags.Reportable.
    ///     Please be careful how you use this property. After setting authentication or privacy parameters to true,
    ///     you will need to update <see cref="UserSecurityModel" /> authentication and privacy types to the correct
    ///     values otherwise encoding/decoding will not work.
    /// </summary>
    public MsgFlags MsgFlags { get; }

    /// <summary>
    ///     Get <see cref="UserSecurityModel" /> class reference.
    /// </summary>
    public UserSecurityModel USM { get; }

    /// <summary>
    ///     Override base class implementation. Returns class ScopedPdu cast as Pdu
    /// </summary>
    public override Pdu Pdu => ScopedPdu;

    /// <summary>
    ///     Access packet ScopedPdu class.
    /// </summary>
    public ScopedPdu ScopedPdu { get; }

    /// <summary>
    ///     Get or set SNMP version 3 packet-Reportable flag in the message flags section. By default, this value is set to
    ///     true.
    /// </summary>
    public bool IsReportable
    {
        get => MsgFlags.Reportable;
        set => MsgFlags.Reportable = value;
    }

    /// <summary>
    ///     Packet is a discovery request
    /// </summary>
    /// <remarks>
    ///     Class checks if Engine id, engine boots, and engine time values are set to default values (null, 0 and 0). If they are
    ///     then the packet is probably a discovery packet, otherwise it is not and false is returned
    /// </remarks>
    public bool IsDiscoveryPacket =>
        USM.EngineId.Length switch
        {
            0 when USM is { EngineTime: 0, EngineBoots: 0 } => true,
            _ => false
        };

    /// <summary>
    ///     Set class security to no authentication and no privacy. The Username is set to "initial" (suitable for
    ///     SNMP version 3 discovery process). Change username before using if discovery is not being performed.
    /// </summary>
    public void NoAuthNoPriv()
    {
        MsgFlags.Authentication = false;
        MsgFlags.Privacy = false;
        USM.SecurityName.Set("initial");
    }

    /// <summary>
    ///     Set class security to no authentication and no privacy with the specific username.
    /// </summary>
    /// <param name="userName">User name</param>
    public void NoAuthNoPriv(byte[] userName)
    {
        MsgFlags.Authentication = false;
        MsgFlags.Privacy = false;
        USM.SecurityName.Set(userName);
    }

    /// <summary>
    ///     Set class security to enable authentication and no privacy. To perform authentication,
    ///     authentication password needs to be supplied and authentication protocol to be used
    ///     to perform authentication.
    ///     This method does not initialize the packet username. Use SNMPV3Packet.SecurityName
    ///     method to set the security name (also called username) for this request.
    /// </summary>
    /// <param name="userName">User name</param>
    /// <param name="authenticationPassword">
    ///     Authentication password to use in authenticating the message. This
    ///     value has to match the password configured on the agent.
    /// </param>
    /// <param name="authenticationProtocol">
    ///     Authentication protocol to use. Available authentication protocols are:
    ///     <see cref="AuthenticationDigests.MD5" /> for HMAC-MD5 authentication, and <see cref="AuthenticationDigests.SHA1" />
    ///     for HMAC-SHA1 message authentication.
    /// </param>
    public void AuthNoPriv(byte[] userName, byte[] authenticationPassword, AuthenticationDigests authenticationProtocol)
    {
        NoAuthNoPriv(userName); // reset authentication and privacy values and set username
        MsgFlags.Authentication = true;
        USM.Authentication = authenticationProtocol;
        USM.AuthenticationSecret = authenticationPassword;
        MsgFlags.Privacy = false;
    }

    /// <summary>
    ///     Set packet security to authentication enabled and privacy protection enabled (SNMP v3 mode authPriv)
    /// </summary>
    /// <param name="userName">User name</param>
    /// <param name="authenticationPassword">Authentication password</param>
    /// <param name="authenticationProtocol">
    ///     Authentication protocol. See definitions in <see cref="AuthenticationDigests" />
    ///     enumeration.
    /// </param>
    /// <param name="privacyPassword">Privacy protection password.</param>
    /// <param name="privacyProtocol">Privacy protocol. See definitions in <see cref="PrivacyProtocols" /> enumeration.</param>
    public void AuthPriv(byte[] userName, byte[] authenticationPassword, AuthenticationDigests authenticationProtocol,
        byte[] privacyPassword, PrivacyProtocols privacyProtocol)
    {
        NoAuthNoPriv(userName); // reset authentication and privacy values and set username
        MsgFlags.Authentication = true;
        USM.AuthenticationSecret = authenticationPassword;
        USM.Authentication = authenticationProtocol;
        MsgFlags.Privacy = true;
        USM.PrivacySecret = privacyPassword;
        USM.Privacy = privacyProtocol;
    }

    /// <summary>
    ///     Set engine time and boots values
    /// </summary>
    /// <param name="engineBoots">Authoritative engine boots value retrived from the agent during discovery procedure.</param>
    /// <param name="engineTime">Engine time value.</param>
    public void SetEngineTime(int engineBoots, int engineTime)
    {
        USM.EngineBoots = engineBoots;
        USM.EngineTime = engineTime;
    }

    /// <summary>
    ///     Set authoritative engine id
    /// </summary>
    /// <param name="engineId">Authoritative engine id</param>
    public void SetEngineId(byte[] engineId)
    {
        USM.EngineId.Set(engineId);
    }

    #region Encode & decode

    /// <summary>
    ///     "Look-ahead" decode of SNMP packet header including USM information
    /// </summary>
    /// <remarks>
    ///     Decode the first component of the SNMP version 3 packet allowing the caller to retrieve USM SecureName needed to
    ///     retrieve
    ///     client security parameters that will allow authentication and privacy decryption to take place.
    ///     This method is used to support Agent like behavior or to handle unsolicited packets like TRAP and INFORMs. In all
    ///     of
    ///     these cases, sender of packets will forward a packet without a request being sent by you. In turn, you will need
    ///     to parse enough of the packet to retrieve SecureName which you can use to retrieve security parameters associated
    ///     with
    ///     that user and attempt to authorize and privacy decrypt the received packet.
    ///     Only use this method when your application is acting as an Agent or if you need to process TRAP and INFORM packets.
    /// </remarks>
    /// <param name="berBuffer">Raw SNMP version 3 packet</param>
    /// <param name="length">SNMP version 3 packet length</param>
    /// <returns>UserSecurityModel class parsed from the parameter SNMP version 3 packet</returns>
    /// <exception cref="SnmpInvalidVersionException">Thrown when attempting to parse an SNMP packet that is not version 3</exception>
    /// <exception cref="OverflowException">
    ///     Thrown when header specifies packet length that is longer then the amount of data
    ///     received.
    /// </exception>
    /// <exception cref="SnmpDecodingException">
    ///     Thrown when an invalid sequence is encountered while decoding global message data
    ///     sequence
    /// </exception>
    /// <exception cref="SnmpException">
    ///     Thrown with SnmpException.UnsupportedNoAuthPriv when a packet is using privacy without
    ///     authentication (not allowed)
    /// </exception>
    /// <exception cref="SnmpException">
    ///     Thrown with SnmpException.UnsupportedSecurityModel when a packet is sent with security
    ///     model other than USM (only USM is defined in SNMPv3 standard)
    /// </exception>
    public UserSecurityModel GetUSM(byte[] berBuffer, int length)
    {
        var buffer = berBuffer.AsSpan(0, length);

        var offset =
            // let base class parse the first sequence and SNMP version number
            base.Decode(buffer);

        // check for a correct SNMP protocol version
        if (_protocolVersion != (int)SnmpVersion.Ver3)
            throw new SnmpInvalidVersionException("Expecting SNMP version 3.");

        // now grab the global message data sequence header information
        var asnType = AsnType.ParseHeader(buffer, ref offset, out var len);

        if (asnType != SnmpConstants.SMI_SEQUENCE)
            throw new SnmpDecodingException("Invalid sequence type when decoding global message data sequence.");

        // check that packet size can accommodate the length specified in the header
        if (len > buffer.Length - offset)
            throw new OverflowException("Packet is too small to contain the data described in the header.");

        // retrieve message id
        offset = _messageId.Decode(buffer, offset);

        // max message size
        offset = _maxMessageSize.Decode(buffer, offset);

        // message flags
        offset = MsgFlags.Decode(buffer, offset);

        switch (MsgFlags.Authentication)
        {
            // verify that a valid authentication/privacy configuration is present in the packet
            case false when MsgFlags.Privacy:
                throw new SnmpException(SnmpException.UnsupportedNoAuthPriv,
                    "SNMP version 3 noAuthPriv security combination is not supported.");
        }

        // security model code
        offset = _securityModel.Decode(buffer, offset);

        // we only support USM. code = 0x03
        if (_securityModel.Value != USM.Type)
            throw new SnmpException(SnmpException.UnsupportedSecurityModel,
                "Class only support SNMP Version 3 User Security Model.");

        // parse user security model
        USM.Decode(buffer, offset);

        return USM;
    }

    /// <summary>
    ///     Decode SNMP version 3 packet. This method will perform authentication check and decode privacy protected
    ///     <see cref="ScopedPdu" />. This method will
    ///     not check for the timeliness of the packet, correct engine boot value or engine id because it does not have a
    ///     reference to the engine time prior to this call.
    /// </summary>
    /// <param name="berBuffer">BER encoded SNMP version 3 packet buffer</param>
    public sealed override int Decode(ReadOnlySpan<byte> berBuffer)
    {
        var pkey = GetPrivateAndAuthenticationKeys(out var akey);

        return Decode(berBuffer, akey, pkey);
    }

    private byte[]? GetPrivateAndAuthenticationKeys(out byte[]? authenticationKey)
    {
        byte[]? privacyKey = null;
        authenticationKey = null;
        if (!ShouldAuthenticate)
            return privacyKey;

        var auth = Authentication.GetInstance(USM.Authentication);

        if (auth == null)
            throw new SnmpException(SnmpException.UnsupportedNoAuthPriv, "Invalid authentication protocol.");

        authenticationKey = auth.PasswordToKey(USM.AuthenticationSecret, USM.EngineId.GetData());

        if (!ShouldEncrypt) return privacyKey;

        var privacyProtocol = PrivacyProtocol.GetInstance(USM.Privacy);

        if (privacyProtocol == null)
            throw new SnmpException(SnmpException.UnsupportedPrivacyProtocol,
                "Specified privacy protocol is not supported.");

        privacyKey = privacyProtocol.PasswordToKey(USM.PrivacySecret,
            USM.EngineId.GetData(),
            auth);

        return privacyKey;
    }

    /// <summary>
    ///     Decode SNMP version 3 packet. This method will perform authentication check and decode privacy protected
    ///     <see cref="ScopedPdu" />. This method will
    ///     not check for the timeliness of the packet, correct engine boot value or engine id because it does not have a
    ///     reference to the engine time prior to this call.
    /// </summary>
    /// <param name="buffer">BER encoded SNMP version 3 packet buffer</param>
    /// <param name="authenticationKey">Authentication key (not password)</param>
    /// <param name="privacyKey">Privacy key (not password)</param>
    public int Decode(ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> authenticationKey, ReadOnlySpan<byte> privacyKey)
    {
        // let base class parse the first sequence and SNMP version number
        var offset = base.Decode(buffer);

        // check for a correct SNMP protocol version
        if (_protocolVersion != (int)SnmpVersion.Ver3)
            throw new SnmpInvalidVersionException("Expecting SNMP version 3.");

        // now grab the global message data sequence header information
        var asnType = AsnType.ParseHeader(buffer, ref offset, out var len);
        if (asnType != SnmpConstants.SMI_SEQUENCE)
            throw new SnmpDecodingException("Invalid sequence type in global message data sequence.");

        // check that packet size can accommodate the length specified in the header
        if (len > buffer.Length - offset)
            throw new OverflowException("Packet is too small to contain the data described in the header.");

        // retrieve message id
        offset = _messageId.Decode(buffer, offset);

        // max message size
        offset = _maxMessageSize.Decode(buffer, offset);

        // message flags
        offset = MsgFlags.Decode(buffer, offset);

        // verify that a valid authentication/privacy configuration is present in the packet
        if (MsgFlags is { Authentication: false, Privacy: true })
            throw new SnmpException(SnmpException.UnsupportedNoAuthPriv,
                "SNMP version 3 noAuthPriv security combination is not supported.");

        // security model code
        offset = _securityModel.Decode(buffer, offset);

        // we only support USM. code = 0x03
        if (_securityModel.Value != USM.Type)
            throw new SnmpException(SnmpException.UnsupportedSecurityModel,
                "Class only support SNMP Version 3 User Security Model.");

        // parse user security model
        offset = USM.Decode(buffer, offset);

        // Authenticate message if the authentication flag is set and packet is not a discovery packet
        if (MsgFlags.Authentication && USM.EngineId.Length > 0)
        {
            // Authenticate packet
            var expectedLength = 12;

            var authProto = Authentication.GetInstance(USM.Authentication);
            if (authProto != null) expectedLength = authProto.AuthentificationHeaderLength;
            if (USM.AuthenticationParameters.Length != expectedLength)
                throw new SnmpAuthenticationException("Invalid authentication parameter field length.");

            byte[]? rented = null;
            Span<byte> bufferWithoutAuthParams = buffer.Length <= StackAllocThreshold
                ? stackalloc byte[StackAllocThreshold]
                : (rented = ArrayPool<byte>.Shared.Rent(buffer.Length));
            bufferWithoutAuthParams = bufferWithoutAuthParams[..buffer.Length];
            try
            {
                buffer.CopyTo(bufferWithoutAuthParams);
                bufferWithoutAuthParams[USM.AuthParamRange].Clear();

                if (!USM.IsAuthentic(authenticationKey, bufferWithoutAuthParams))
                    throw new SnmpAuthenticationException("Authentication of the incoming packet failed.");
            }
            finally
            {
                if (rented != null) ArrayPool<byte>.Shared.Return(rented);
            }
        }

        // Decode ScopedPdu if it is privacy protected and packet is not a discovery packet
        if (MsgFlags.Privacy && USM.EngineId.Length > 0)
        {
            var privacyProtocol = PrivacyProtocol.GetInstance(USM.Privacy);
            if (privacyProtocol == null)
                throw new SnmpException(SnmpException.UnsupportedPrivacyProtocol,
                    "Privacy protocol requested is not supported.");

            if (USM.PrivacyParameters.Length != privacyProtocol.PrivacyParametersLength)
                throw new SnmpException(SnmpException.InvalidPrivacyParameterLength,
                    "Invalid privacy parameters field length.");

            // Initialize a temporary OctetString class to hold encrypted ScopedPdu
            var encryptedScopedPdu = new OctetString();
            encryptedScopedPdu.Decode(buffer, offset);

            // decode encrypted packet
            var decryptedScopedPdu = privacyProtocol.Decrypt(
                encryptedScopedPdu.GetData(),
                0,
                encryptedScopedPdu.Length,
                privacyKey,
                USM.EngineBoots,
                USM.EngineTime,
                USM.PrivacyParameters.GetData());
            var tempOffset = 0;
            offset = ScopedPdu.Decode(decryptedScopedPdu, tempOffset);
        }
        else
        {
            offset = ScopedPdu.Decode(buffer, offset);
        }

        return offset;
    }

    /// <summary>
    ///     Encode SNMP version 3 packet
    /// </summary>
    /// <remarks>
    ///     Before encoding the packet into a byte array, you need to ensure all required information is
    ///     set. Examples of required information are request type, Vbs (Oid + values pairs), USM settings including
    ///     SecretName, authentication method and secret (if needed), privacy method and secret (if needed), etc.
    /// </remarks>
    /// <returns>Byte array BER encoded SNMP packet.</returns>
    public override byte[] Encode()
    {
        var pkey = GetPrivateAndAuthenticationKeys(out var akey);

        return Encode(akey ?? [], pkey ?? []);
    }

    public override int Encode(Span<byte> target)
    {
        var pkey = GetPrivateAndAuthenticationKeys(out var akey);
        return Encode(target, akey ?? [], pkey ?? []);
    }

    public override int ByteLength => HeaderLength + ScopedPduLength + UsmLength +
                                      _protocolVersion.ByteLength + AsnType.MaxHeaderSize;

    private int GlobalLength => _messageId.ByteLength + _maxMessageSize.ByteLength + MsgFlags.ByteLength +
                                _securityModel.ByteLength;

    private int HeaderLength => GlobalLength + AsnType.HeaderSize(GlobalLength);

    private int ScopedPduLength => ShouldEncrypt ? (ScopedPdu.ByteLength / 64 + 1) * 64 : ScopedPdu.ByteLength;

    private int UsmLength
    {
        get
        {
            if (ShouldAuthenticate) return USM.ByteLength;
            using (DiscoveryPacket())
            {
                return USM.ByteLength;
            }
        }
    }

    private bool ShouldEncrypt => MsgFlags.Privacy && USM.EngineId.Length > 0;

    private bool ShouldAuthenticate => MsgFlags.Authentication && USM.EngineId.Length > 0;

    /// <summary>
    ///     Encode SNMP version 3 packet
    /// </summary>
    /// <param name="authenticationKey">Authentication key (not password)</param>
    /// <param name="privacyKey">Privacy key (not password)</param>
    /// <remarks>
    ///     Before encoding the packet into a byte array, you need to ensure all required information is
    ///     set. Examples of required information is request type, Vbs (Oid + values pairs), USM settings including
    ///     SecretName, authentication method and secret (if needed), privacy method and secret (if needed), etc.
    /// </remarks>
    /// <returns>Byte array BER encoded SNMP packet.</returns>
    public byte[] Encode(ReadOnlySpan<byte> authenticationKey, ReadOnlySpan<byte> privacyKey)
    {
        Span<byte> result = stackalloc byte[ByteLength];
        var written = Encode(result, authenticationKey, privacyKey);
        return result[..written].ToArray();
    }


    /// <summary>
    ///     Encode SNMP version 3 packet
    /// </summary>
    /// <param name="buffer">The buffer to write the BER-encoded data to</param>
    /// <param name="authKey">Authentication key (not password)</param>
    /// <param name="privKey">Privacy key (not password)</param>
    /// <remarks>
    ///     Before encoding the packet into a byte array, you need to ensure all required information is
    ///     set. Examples of required information are request type, Vbs (Oid + values pairs), USM settings including
    ///     SecretName, authentication method and secret (if needed), privacy method and secret (if needed), etc.
    /// </remarks>
    /// <returns>Byte array BER encoded SNMP packet.</returns>
    public int Encode(Span<byte> buffer, ReadOnlySpan<byte> authKey, ReadOnlySpan<byte> privKey)
    {
        // encode the global message data sequence header information
        // if message id is 0 then generate a new, random message id
        _securityModel.Value = USM.Type;

        EncodeGlobalData(buffer);

        var written = HeaderLength;
        // before going down this road, check if this is a discovery packet
        using (DiscoveryPacket())
        {
            written += USM.Encode(buffer[HeaderLength..]);
        }

        // Check if privacy encryption is required
        Range encodedPduLocation;
        if (ShouldEncrypt)
        {
            encodedPduLocation = Encrypt(privKey, buffer, HeaderLength, ref written);
        }
        else
        {
            var end = ScopedPdu.Encode(buffer[written..]);
            encodedPduLocation = written..(written + end);
            written += end;
        }


        var baseLength = base.Encode(buffer, ref written);
        if (ShouldAuthenticate)
        {
            USM.Authenticate(authKey, buffer[..written]);
            buffer[baseLength..].CopyTo(buffer); //Remove the base-encoded header
            // Now re-encode the packet with the authentication information
            Span<byte> encodedPdu = stackalloc byte[encodedPduLocation.End.Value - encodedPduLocation.Start.Value];
            buffer[encodedPduLocation].CopyTo(encodedPdu);
            written = HeaderLength;
            written += USM.Encode(buffer[written..]);
            encodedPdu.CopyTo(buffer[written..]);
            written += encodedPdu.Length;
            base.Encode(buffer, ref written);
        }

        return written;
    }

    private void EncodeGlobalData(Span<byte> buffer)
    {
        Span<byte> globalHeader = stackalloc byte[AsnType.HeaderSize(GlobalLength)];
        Span<byte> globalMessageData = stackalloc byte[GlobalLength];
        var ghOffset = 0;
        // encode message id
        ghOffset += _messageId.Encode(globalMessageData[ghOffset..]);

        // encode max message size
        ghOffset += _maxMessageSize.Encode(globalMessageData[ghOffset..]);

        // message flags
        ghOffset += MsgFlags.Encode(globalMessageData[ghOffset..]);

        // security model code
        _securityModel.Encode(globalMessageData[ghOffset..]);
        // add global message data to the main buffer
        // encode sequence header and add data
        AsnType.BuildHeader(globalHeader, SnmpConstants.SMI_SEQUENCE, GlobalLength);


        globalHeader.CopyTo(buffer);
        globalMessageData.CopyTo(buffer[globalHeader.Length..]);
    }

    private Range Encrypt(ReadOnlySpan<byte> privKey, Span<byte> buffer, int packetHeader, ref int written)
    {
        var privacyProtocol = PrivacyProtocol.GetInstance(USM.Privacy);
        if (privacyProtocol == null)
            throw new SnmpException(SnmpException.UnsupportedPrivacyProtocol,
                "Specified privacy protocol is not supported.");

        // Get BER encoded ScopedPdu
        Span<byte> unencryptedPdu = stackalloc byte[ScopedPdu.ByteLength];
        var unencryptedWrite = ScopedPdu.Encode(unencryptedPdu);

        // we have to expand the key
        var auth = Authentication.GetInstance(USM.Authentication);
        if (auth == null)
            throw new SnmpException(SnmpException.UnsupportedNoAuthPriv,
                "Invalid authentication protocol. noAuthPriv mode not supported.");

        var encryptedBuffer = privacyProtocol.Encrypt(
            unencryptedPdu,
            0,
            unencryptedWrite,
            privKey,
            USM.EngineBoots,
            USM.EngineTime,
            out var privacyParameters,
            auth);

        USM.PrivacyParameters.Set(privacyParameters);
        var encryptedOctetString = new OctetString(encryptedBuffer);
        // now redo packet encoding
        written = packetHeader;
        written += USM.Encode(buffer[written..]);
        var end = encryptedOctetString.Encode(buffer[written..]);
        var location = written..(written + end);
        written += end;
        return location;
    }

    private Scope DiscoveryPacket()
    {
        var savedUserName = new OctetString();
        var privacy = MsgFlags.Privacy;
        var authentication = MsgFlags.Authentication;
        var reportable = MsgFlags.Reportable;

        if (USM.EngineId.Length <= 0)
        {
            // save USM settings before encoding a Discovery packet
            savedUserName.Set(USM.SecurityName);
            USM.SecurityName.Reset(); // delete security name for discovery packets
            MsgFlags.Authentication = false;
            MsgFlags.Privacy = false;
            MsgFlags.Reportable = true;
        }


        return new Scope(() =>
        {
            if (USM.EngineId.Length > 0) return;
            // restore saved USM values
            USM.SecurityName.Set(savedUserName);
            MsgFlags.Authentication = authentication;
            MsgFlags.Privacy = privacy;
            MsgFlags.Reportable = reportable;
        });
    }

    private class Scope(Action disposer) : IDisposable
    {
        public void Dispose()
        {
            disposer();
        }
    }

    #endregion

    #region Helper methods

    /// <summary>
    ///     Generate an authentication key from authentication password and engine id
    /// </summary>
    /// <returns>Authentication key on success or null on failure</returns>
    public byte[]? GenerateAuthenticationKey()
    {
        if (USM.EngineId.Length <= 0)
            return null;
        if (USM.AuthenticationSecret.Length <= 0)
            return null;
        if (USM.Authentication == AuthenticationDigests.None) return null;
        var authProto = Authentication.GetInstance(USM.Authentication);
        return authProto?.PasswordToKey(USM.AuthenticationSecret, USM.EngineId.GetData());
    }

    /// <summary>
    ///     Generate a privacy key from authentication password and engine id
    /// </summary>
    /// <returns>Privacy key on success or null on failure</returns>
    public byte[]? GeneratePrivacyKey()
    {
        if (USM.Authentication == AuthenticationDigests.None) return null;

        if (USM.Privacy == PrivacyProtocols.None) return null;

        if (USM.PrivacySecret.Length <= 0)
            return null;
        var authenticationDigest = Authentication.GetInstance(USM.Authentication);
        if (authenticationDigest == null) return null;
        var privacyProtocol = PrivacyProtocol.GetInstance(USM.Privacy);
        return privacyProtocol?.PasswordToKey(USM.PrivacySecret, USM.EngineId.GetData(), authenticationDigest);
    }

    /// <summary>
    ///     Build an SNMP version 3 packet suitable for use in a discovery process.
    /// </summary>
    /// <returns>Discovery process prepared SNMP version 3 packet.</returns>
    public static SnmpV3Packet DiscoveryRequest()
    {
        var packet = new SnmpV3Packet(new ScopedPdu()); // with a blank scoped pdu
        // looking through other implementation, null (length 0) username is used
        // packet.USM.SecurityName.Set("initial"); // set username to initial, as described in RFCs
        return packet; // return packet
    }

    /// <summary>
    ///     Build an SNMP discovery response packet.
    /// </summary>
    /// <remarks>
    ///     Manager application has to be able to respond to discovery requests to be able to handle
    ///     SNMPv3 INFORM notifications.
    ///     In an INFORM packet, engineId value is set to the manager stations id (unlike all other requests
    ///     where agent is the authoritative SNMP engine). For the agent to discover appropriate manager engine
    ///     id, boots and time values (required for authentication and privacy packet handling), manager has to
    ///     be able to respond to the discovery request.
    /// </remarks>
    /// <param name="messageId">Message id from the received discovery packet</param>
    /// <param name="requestId">Request id from the received discovery packets Pdu</param>
    /// <param name="engineId">Local engine id</param>
    /// <param name="engineBoots">Number of times local SNMP engine has been restarted</param>
    /// <param name="engineTime">Time since the engine was started in seconds</param>
    /// <param name="unknownEngineIdCount">Number of discovery packets received by the local SNMP engine</param>
    /// <returns>SNMP v3 packet properly formatted as a response to a discovery request</returns>
    public static SnmpV3Packet DiscoveryResponse(int messageId, int requestId, OctetString engineId, int engineBoots,
        int engineTime, int unknownEngineIdCount)
    {
        var packet = new SnmpV3Packet
        {
            Pdu =
            {
                Type = PduType.Report,
                RequestId = requestId
            }
        };
        packet.Pdu.VbList.Add(SnmpConstants.usmStatsUnknownEngineIDs, new Integer32(unknownEngineIdCount));
        // Discovery response is a report packet. We don't want to receive reports about a report
        packet.MsgFlags.Reportable = false;
        packet.SetEngineId(engineId);
        packet.MessageId = messageId;
        packet.USM.EngineBoots = engineBoots;
        packet.USM.EngineTime = engineTime;
        return packet;
    }

    /// <summary>
    ///     Build an SNMP RESPONSE packet for the received INFORM packet.
    /// </summary>
    /// <returns>SNMP version 3 packet containing RESPONSE to the INFORM packet contained in the class instance.</returns>
    public SnmpV3Packet BuildInformResponse()
    {
        return BuildInformResponse(this);
    }

    /// <summary>
    ///     Build an SNMP RESPONSE packet for the INFORM packet class.
    /// </summary>
    /// <param name="informPacket">SNMP INFORM packet</param>
    /// <returns>SNMP version 3 packet containing RESPONSE to the INFORM packet contained in the parameter.</returns>
    /// <exception cref="SnmpInvalidPduTypeException">Parameter is not an INFORM SNMP version 3 packet class</exception>
    /// <exception cref="SnmpInvalidVersionException">Parameter is not a SNMP version 3 packet</exception>
    public static SnmpV3Packet BuildInformResponse(SnmpV3Packet informPacket)
    {
        if (informPacket.Version != SnmpVersion.Ver3)
            throw new SnmpInvalidVersionException("INFORM packet can only be parsed from an SNMP version 3 packet.");
        if (informPacket.Pdu.Type != PduType.Inform)
            throw new SnmpInvalidPduTypeException("Inform response can only be built for INFORM packets.");

        var response = new SnmpV3Packet(informPacket.ScopedPdu)
        {
            MessageId = informPacket.MessageId
        };
        response.USM.SecurityName.Set(informPacket.USM.SecurityName);
        response.USM.EngineTime = informPacket.USM.EngineTime;
        response.USM.EngineBoots = informPacket.USM.EngineBoots;
        response.USM.EngineId.Set(informPacket.USM.EngineId);
        response.USM.Authentication = informPacket.USM.Authentication;
        response.USM.AuthenticationSecret = response.USM.Authentication != AuthenticationDigests.None
            ? informPacket.USM.AuthenticationSecret
            : [];
        response.USM.Privacy = informPacket.USM.Privacy;
        response.USM.PrivacySecret =
            response.USM.Privacy != PrivacyProtocols.None ? informPacket.USM.PrivacySecret : [];
        response.MsgFlags.Authentication = informPacket.MsgFlags.Authentication;
        response.MsgFlags.Privacy = informPacket.MsgFlags.Privacy;
        response.MsgFlags.Reportable = informPacket.MsgFlags.Reportable;
        response.ScopedPdu.ContextEngineId.Set(informPacket.ScopedPdu.ContextEngineId);
        response.ScopedPdu.ContextName.Set(informPacket.ScopedPdu.ContextName);
        response.Pdu.Type = PduType.Response;
        response.Pdu.TrapObjectID.Set(informPacket.Pdu.TrapObjectID);
        response.Pdu.TrapSysUpTime.Value = informPacket.Pdu.TrapSysUpTime.Value;
        response.Pdu.RequestId = informPacket.Pdu.RequestId;

        return response;
    }

    #endregion

    public bool Equals(SnmpV3Packet? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return _maxMessageSize.Equals(other._maxMessageSize) && _messageId.Equals(other._messageId) &&
               _securityModel.Equals(other._securityModel) && MsgFlags.Equals(other.MsgFlags) &&
               USM.Equals(other.USM) && ScopedPdu.Equals(other.ScopedPdu);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((SnmpV3Packet)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_maxMessageSize, _messageId, _securityModel, MsgFlags, USM, ScopedPdu);
    }
}