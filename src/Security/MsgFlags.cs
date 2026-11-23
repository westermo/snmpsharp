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

namespace SnmpSharpNet;

/// <summary>
///     Message flags in the SNMP v3 header.
/// </summary>
/// <remarks>
///     Message flags hold flags that
///     indicate if packet is authenticated, privacy protected and if
///     report reply is expected on errors.
///     Message flags field is a 1 byte <see cref="OctetString" /> encoded
///     field.
/// </remarks>
public class MsgFlags : AsnType
{
    /// <summary>
    ///     Bit value that, when set, indicates that packet has been authenticated.
    /// </summary>
    public static readonly byte FLAG_AUTH = 0x01;

    /// <summary>
    ///     Bit value that, when set, indicates that packet has been privacy protected.
    /// </summary>
    public static readonly byte FLAG_PRIV = 0x02;

    /// <summary>
    ///     Bit value that, when set, indicates that sender of the packet expects report packet to be sent by the agent on
    ///     errors.
    /// </summary>
    public static readonly byte FLAG_REPORTABLE = 0x04;

    /// <summary>
    ///     True if authentication is used to secure the packet, otherwise false.
    /// </summary>
    protected bool _authenticationFlag;

    /// <summary>
    ///     True if ScopedPdu portion of the packet is privacy protected with encryption, otherwise false.
    /// </summary>
    protected bool _privacyFlag;

    /// <summary>
    ///     True if reportable flag is set, otherwise false.
    /// </summary>
    protected bool _reportableFlag;

    /// <summary>
    ///     Standard constructor. All flags are set to false by default.
    /// </summary>
    public MsgFlags()
    {
        _authenticationFlag = _privacyFlag = _reportableFlag = false;
    }

    /// <summary>
    ///     Constructor. Initialize individual flag values.
    /// </summary>
    /// <param name="authentication">true if authentication is used, otherwise false</param>
    /// <param name="privacy">true if privacy protection is used, otherwise false</param>
    /// <param name="reportable">true if report is expected, otherwise false</param>
    public MsgFlags(bool authentication, bool privacy, bool reportable)
    {
        _authenticationFlag = authentication;
        _privacyFlag = privacy;
        _reportableFlag = reportable;
    }

    /// <summary>
    ///     Get/Set authentication flag. Value is true if authentication is used, false if packet is not authenticated
    /// </summary>
    public bool Authentication
    {
        get => _authenticationFlag;
        set => _authenticationFlag = value;
    }

    /// <summary>
    ///     Get/Set privacy flag. Value is true if privacy protection is used, false if packet is not privacy protected.
    /// </summary>
    public bool Privacy
    {
        get => _privacyFlag;
        set => _privacyFlag = value;
    }

    /// <summary>
    ///     Get/Set reportable flag. Value is true if sender expects report packet on errors, false if sender does
    ///     not expect report packets.
    /// </summary>
    public bool Reportable
    {
        get => _reportableFlag;
        set => _reportableFlag = value;
    }

    /// <summary>
    ///     Clone this class.
    /// </summary>
    /// <returns>Cloned class cast as Object</returns>
    public override object Clone()
    {
        return new MsgFlags(_authenticationFlag, _privacyFlag, _reportableFlag);
    }

    public override int Encode(Span<byte> buffer)
    {
        byte flag = 0x00;
        switch (_authenticationFlag)
        {
            case true:
                flag |= FLAG_AUTH;
                break;
        }

        switch (_privacyFlag)
        {
            case true:
                flag |= FLAG_PRIV;
                break;
        }

        switch (_reportableFlag)
        {
            case true:
                flag |= FLAG_REPORTABLE;
                break;
        }

        var slice = BuildHeader(buffer, OCTETSTRING, 1);
        buffer[slice] = flag;
        return slice + 1;
    }

    public override int ByteLength => base.ByteLength + 1;

    public override int Decode(ReadOnlySpan<byte> buffer, int offset)
    {
        // reset class values
        _authenticationFlag = false;
        _privacyFlag = false;
        _reportableFlag = false;
        var flagObject = new OctetString();
        offset = flagObject.Decode(buffer, offset);
        switch (flagObject.Length)
        {
            case > 0:
            {
                if ((flagObject[0] & FLAG_AUTH) != 0)
                    _authenticationFlag = true;
                if ((flagObject[0] & FLAG_PRIV) != 0)
                    _privacyFlag = true;
                if ((flagObject[0] & FLAG_REPORTABLE) != 0)
                    _reportableFlag = true;
                break;
            }
            default:
                throw new SnmpDecodingException("Invalid SNMPv3 flag field.");
        }

        return offset;
    }

    /// <summary>
    ///     Return string representation of the object.
    /// </summary>
    /// <returns>String representation of the class values.</returns>
    public override string ToString()
    {
        return string.Format("Reportable {0} Authenticated {1} Privacy {2}", _reportableFlag.ToString(),
            _authenticationFlag.ToString(), _privacyFlag.ToString());
    }
}