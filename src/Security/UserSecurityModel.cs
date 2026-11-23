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
/* Changes:
 *  Dec/05, 2008
 *   Added user name comparison in decode method
 */

using System;

namespace SnmpSharpNet;

/// <summary>
///     User security model implementation class.
/// </summary>
public class UserSecurityModel : AsnType, IEquatable<UserSecurityModel>
{
    public override string ToString()
    {
        return $"""
                User Security Model
                Engine ID: {EngineId}
                Engine Boots: {EngineBoots}
                Engine Time: {EngineTime}
                Security Name: {SecurityName}
                Authentication Parameters: {AuthenticationParameters}
                Privacy Parameters: {PrivacyParameters}
                Authentication: {Authentication}
                Privacy: {Privacy}

                """;
    }

    /// <summary>
    ///     Authentication secret
    /// </summary>
    protected byte[] _authenticationSecret;

    /// <summary>
    ///     Authoritative engine boots value
    /// </summary>
    protected Integer32 _engineBoots;

    /// <summary>
    ///     Authoritative engine time value
    /// </summary>
    protected Integer32 _engineTime;

    /// <summary>
    ///     Privacy protocol. For valid values see <see cref="PrivacyProtocols" />
    /// </summary>
    protected PrivacyProtocols _privacy;

    /// <summary>
    ///     Privacy parameters in authPriv requests
    /// </summary>
    protected OctetString _privacyParameters;

    /// <summary>
    ///     Privacy secret
    /// </summary>
    protected byte[] _privacySecret;

    /// <summary>
    ///     SNMP version 3 security name (or user name)
    /// </summary>
    protected OctetString _securityName;

    /// <summary>
    ///     Standard constructor.
    /// </summary>
    public UserSecurityModel()
    {
        _asnType = 3;
        EngineId = new OctetString();
        _engineBoots = new Integer32();
        _engineTime = new Integer32();
        Authentication = AuthenticationDigests.None;

        _securityName = new OctetString();
        _authenticationSecret = [];
        AuthenticationParameters = new OctetString();
        _privacySecret = [];
        _privacy = PrivacyProtocols.None;
        _privacyParameters = new OctetString();
    }

    /// <summary>
    ///     Copy constructor.
    /// </summary>
    /// <param name="value">Class to copy values from</param>
    public UserSecurityModel(UserSecurityModel value)
        : this()
    {
        EngineId.Set(value.EngineId);
        _engineBoots.Value = value.EngineBoots;
        _engineTime.Value = value.EngineTime;
        _securityName.Set(value.SecurityName);
        AuthenticationParameters = new OctetString();
        _privacySecret = [];
        _privacy = PrivacyProtocols.None;
        _privacyParameters = new OctetString();
    }

    /// <summary>
    ///     Get SNMP version 3 agent authoritative engine id object
    /// </summary>
    public OctetString EngineId { get; protected set; }

    /// <summary>
    ///     Get SNMP version 3 agent authoritative engine boots object
    /// </summary>
    public int EngineBoots
    {
        get => _engineBoots.Value;
        set => _engineBoots.Value = value;
    }

    /// <summary>
    ///     Get SNMP version 3 agent authoritative engine time object
    /// </summary>
    public int EngineTime
    {
        get => _engineTime.Value;
        set => _engineTime.Value = value;
    }

    /// <summary>
    ///     Get SNMP version 3 authentication parameter field object
    /// </summary>
    public OctetString AuthenticationParameters { get; private set; }

    /// <summary>
    ///     Get/Set hash to use for SNMP version 3 authentication. For available values see
    ///     <see cref="AuthenticationDigests" />
    /// </summary>
    public AuthenticationDigests Authentication { get; set; }

    /// <summary>
    ///     Security name (or user name)
    /// </summary>
    public OctetString SecurityName => _securityName;

    /// <summary>
    ///     Authentication secret (or password).
    /// </summary>
    public byte[] AuthenticationSecret
    {
        get => _authenticationSecret;
        set => _authenticationSecret = value;
    }

    /// <summary>
    ///     Privacy secret (or password)
    /// </summary>
    public byte[] PrivacySecret
    {
        get => _privacySecret;
        set => _privacySecret = value;
    }

    /// <summary>
    ///     Get/set privacy protocol value. For available privacy protocols, see <see cref="PrivacyProtocols" />
    ///     enumeration.
    /// </summary>
    public PrivacyProtocols Privacy
    {
        get => _privacy;
        set => _privacy = value;
    }

    /// <summary>
    ///     Get privacy parameters object.
    /// </summary>
    public OctetString PrivacyParameters
    {
        get => _privacyParameters;
        set => _privacyParameters = value;
    }

    /// <summary>
    ///     Clone object
    /// </summary>
    /// <returns>Duplicate copy of the object</returns>
    public override object Clone()
    {
        return new UserSecurityModel(this);
    }

    /// <summary>
    ///     Set SNMP version 3 agent engine time related values.
    /// </summary>
    /// <param name="engineTime">SNMP version 3 agent engine time value</param>
    /// <param name="engineBoots">SNMP version 3 agent engine boot value</param>
    public void SetEngineTime(int engineTime, int engineBoots)
    {
        _engineTime.Value = engineTime;
        _engineBoots.Value = engineBoots;
    }

    /// <summary>
    ///     Authenticate SNMP version 3 message.
    ///     Before calling this member, entire SNMP version 3 packet needs to be encoded. After authentication
    ///     process is completed, authenticationParameters value in the USM header is updated and SNMPv3 packet
    ///     needs to be re-encoded to include it in the BER encoded stream prior to transmission.
    /// </summary>
    /// <param name="wholePacket">SNMP version 3 BER encoded packet.</param>
    public void Authenticate(ref byte[] wholePacket)
    {
        if (Authentication == AuthenticationDigests.None) return;
        var authProto = SnmpSharpNet.Authentication.GetInstance(Authentication)!;
        var authParam = authProto.Authenticate(AuthenticationSecret, EngineId.GetData(), wholePacket);
        AuthenticationParameters = new OctetString(authParam);
    }

    /// <summary>
    ///     Authenticate SNMP version 3 message.
    ///     Before calling this member, entire SNMP version 3 packet needs to be encoded. After authentication
    ///     process is completed, authenticationParameters value in the USM header is updated and SNMPv3 packet
    ///     needs to be re-encoded to include it in the BER encoded stream prior to transmission.
    /// </summary>
    /// <param name="authKey">Authentication key (not password)</param>
    /// <param name="wholePacket">SNMP version 3 BER encoded packet.</param>
    public void Authenticate(ReadOnlySpan<byte> authKey, ReadOnlySpan<byte> wholePacket)
    {
        var authProto = SnmpSharpNet.Authentication.GetInstance(Authentication);
        if (authProto == null) return;
        var authParam = authProto.Authenticate(authKey, wholePacket);
        AuthenticationParameters = new OctetString(authParam);
    }

    /// <summary>
    ///     Authenticate incoming packet
    /// </summary>
    /// <param name="wholePacket">Received BER encoded SNMP version 3 packet</param>
    /// <returns>True if packet is successfully authenticated, otherwise false.</returns>
    public bool IsAuthentic(byte[] wholePacket)
    {
        if (Authentication == AuthenticationDigests.None) return false; // Nothing to authenticate

        var authProto = SnmpSharpNet.Authentication.GetInstance(Authentication);
        if (authProto != null)
            return authProto.AuthenticateIncomingMsg(AuthenticationSecret, EngineId.GetData(),
                AuthenticationParameters.GetData(),
                wholePacket);

        return false; // Nothing to authenticate
    }

    /// <summary>
    ///     Authenticate incoming packet
    /// </summary>
    /// <param name="authKey">Authentication key (not password)</param>
    /// <param name="wholePacket">Received BER encoded SNMP version 3 packet</param>
    /// <returns>True if packet is successfully authenticated, otherwise false.</returns>
    public bool IsAuthentic(ReadOnlySpan<byte> authKey, ReadOnlySpan<byte> wholePacket)
    {
        if (Authentication == AuthenticationDigests.None) return false; // Nothing to authenticate
        var authProto = SnmpSharpNet.Authentication.GetInstance(Authentication);
        return authProto != null &&
               authProto.AuthenticateIncomingMsg(authKey, AuthenticationParameters.GetData(), wholePacket);
        // Nothing to authenticate
    }

    public override int Encode(Span<byte> buffer)
    {
        EnsureAuthParameters();
        var mbl = MemberByteLength;
        var written = BuildHeader(buffer, OCTETSTRING, mbl + HeaderSize(mbl));
        written += BuildHeader(buffer[written..], SnmpConstants.SMI_SEQUENCE, mbl);
        // First encode all the values that will form the sequence
        written += EngineId.Encode(buffer[written..]);
        // Encode engine boots
        written += _engineBoots.Encode(buffer[written..]);
        // encode engine time
        written += _engineTime.Encode(buffer[written..]);
        written += _securityName.Encode(buffer[written..]);
        written += AuthenticationParameters.Encode(buffer[written..]);
        written += _privacyParameters.Encode(buffer[written..]);
        return written;
    }

    private void EnsureAuthParameters()
    {
        if (Authentication != AuthenticationDigests.None)
        {
            switch (AuthenticationParameters.Length)
            {
                case <= 0:
                {
                    // If authentication is used, set authentication parameters field to 0x00 with the authentification length of the auth protocol.
                    var size = 12;
                    var authProto = SnmpSharpNet.Authentication.GetInstance(Authentication);
                    if (authProto != null) size = authProto.AuthentificationHeaderLength;
                    AuthenticationParameters.Set(new byte[size]);
                    break;
                }
            }
        }
        else
        {
            AuthenticationParameters.Reset();
        }

        if (_privacy != PrivacyProtocols.None)
        {
            switch (_privacyParameters.Length)
            {
                case <= 0:
                {
                    var privProto = PrivacyProtocol.GetInstance(_privacy);
                    if (privProto != null)
                    {
                        var parameter = new byte[privProto.PrivacyParametersLength];
                        for (var i = 0;
                             i < privProto.PrivacyParametersLength;
                             i++)
                            parameter[i] =
                                0x00; // This is not necessary since all array members are, by default, initialized to 0
                        _privacyParameters.Set(parameter);
                    }
                    else
                    {
                        throw new SnmpException(SnmpException.UnsupportedPrivacyProtocol,
                            "Unrecognized privacy protocol specified.");
                    }

                    break;
                }
            }
        }
        else
        {
            _privacyParameters.Reset();
        }
    }

    public override int ByteLength
    {
        get
        {
            EnsureAuthParameters();
            var mbl = MemberByteLength;
            var firstHeader = HeaderSize(mbl);
            var secondHeader = HeaderSize(mbl + firstHeader);
            return mbl + firstHeader + secondHeader;
        }
    }

    private int MemberByteLength =>
        EngineId.ByteLength + _engineBoots.ByteLength + _engineTime.ByteLength +
        _securityName.ByteLength + AuthenticationParameters.ByteLength +
        _privacyParameters.ByteLength;

    /// <summary>
    ///     Decode USM portion of the SNMP version 3 packet.
    /// </summary>
    /// <param name="buffer">Received SNMP packet BER encoded</param>
    /// <param name="offset">Offset within the buffer to start decoding USM information</param>
    /// <returns>Buffer position after the decoded value</returns>
    /// <exception cref="SnmpDecodingException">Thrown when decoding enountered invalid data type in USM information</exception>
    /// <exception cref="OverflowException">Thrown when packet is too small to contain information length specified in header</exception>
    public override int Decode(ReadOnlySpan<byte> buffer, int offset)
    {
        // Grab the octet string header
        var type = ParseHeader(buffer, ref offset, out var len);
        if (type != OCTETSTRING)
            throw new SnmpDecodingException("Invalid value type found while looking for USM header.");

        if (len > buffer.Length - offset)
            throw new OverflowException("Packet too small");
        // Now grab the sequence header
        type = ParseHeader(buffer, ref offset, out len);
        if (type != SnmpConstants.SMI_SEQUENCE)
            throw new SnmpDecodingException("Sequence missing from USM header.");

        if (len > buffer.Length - offset)
            throw new OverflowException("Packet too small");

        // now grab values one at the time
        offset = EngineId.Decode(buffer, offset);

        offset = _engineBoots.Decode(buffer, offset);

        offset = _engineTime.Decode(buffer, offset);

        offset = _securityName.Decode(buffer, offset);
        var saveOffset = offset;
        offset = AuthenticationParameters.Decode(buffer, offset);
        if (AuthenticationParameters.Length > 0)
        {
            // walk through and set the authentication parameters to 0x00 in the packet
            saveOffset += 2; // Skip BER encoded variable type and length
            // for (var i = 0; i < AuthenticationParameters.Length; i++)
            // {
            //     buffer[saveOffset + i] = 0x00;
            // }

            var saveEnd = saveOffset + AuthenticationParameters.Length;
            AuthParamRange = saveOffset..saveEnd;
        }


        offset = _privacyParameters.Decode(buffer, offset);
        return offset;
    }

    public Range AuthParamRange { get; private set; }

    /// <summary>
    ///     Checks for validity and completeness of information in this class. This method doesn't "know" what you
    ///     are trying to do so it tests for minimal information required.
    /// </summary>
    /// <returns>True if information is valid and complete enough for a successful request, otherwise false</returns>
    public bool Valid()
    {
        if ((Authentication != AuthenticationDigests.None || _privacy != PrivacyProtocols.None) &&
            _securityName.Length <= 0)
            return false; // Have to provide a user name when using authentication or privacy
        if (Authentication == AuthenticationDigests.None &&
            _privacy != PrivacyProtocols.None) return false; // noAuthPriv is not supported by SNMP version 3

        // check that secrets are properly configured
        if (Authentication != AuthenticationDigests.None && _authenticationSecret.Length <= 0)
            return false; // authentication configured without a secret
        return _privacy == PrivacyProtocols.None || _privacySecret.Length > 0;
        // privacy configured without a secret
        // no point in checking the rest since discovery process is done with all other values being null or 0
    }

    /// <summary>
    ///     Reset USM object to default values. All OctetString and byte[] members are reset to 0 length and
    ///     privacy and authentication protocols are set to none.
    /// </summary>
    public void Reset()
    {
        _asnType = 3;
        EngineId = new OctetString();
        _engineBoots = new Integer32();
        _engineTime = new Integer32();
        Authentication = AuthenticationDigests.None;

        _securityName = new OctetString();
        _authenticationSecret = [];
        AuthenticationParameters = new OctetString();
        _privacySecret = [];
        _privacy = PrivacyProtocols.None;
        _privacyParameters = new OctetString();
    }

    public bool Equals(UserSecurityModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return _authenticationSecret.Equals(other._authenticationSecret) && _engineBoots.Equals(other._engineBoots) &&
               _engineTime.Equals(other._engineTime) && _privacy == other._privacy &&
               _privacyParameters.Equals(other._privacyParameters) && _privacySecret.Equals(other._privacySecret) &&
               _securityName.Equals(other._securityName) && EngineId.Equals(other.EngineId) &&
               AuthenticationParameters.Equals(other.AuthenticationParameters) &&
               Authentication == other.Authentication && AuthParamRange.Equals(other.AuthParamRange);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((UserSecurityModel)obj);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(_authenticationSecret);
        hashCode.Add(_engineBoots);
        hashCode.Add(_engineTime);
        hashCode.Add((int)_privacy);
        hashCode.Add(_privacyParameters);
        hashCode.Add(_privacySecret);
        hashCode.Add(_securityName);
        hashCode.Add(EngineId);
        hashCode.Add(AuthenticationParameters);
        hashCode.Add((int)Authentication);
        hashCode.Add(AuthParamRange);
        return hashCode.ToHashCode();
    }
}