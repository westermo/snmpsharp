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
///     Vb item. Stores Oid => value pair for each value
/// </summary>
public class Vb : AsnType, ICloneable
{
    /// <summary>
    ///     OID of the object
    /// </summary>
    private Oid? _oid;

    /// <summary>
    ///     Value of the object
    /// </summary>
    private AsnType? _value;

    /// <summary>
    ///     Standard constructor. Initializes values to null.
    /// </summary>
    public Vb()
    {
        _asnType = SEQUENCE | CONSTRUCTOR;
    }

    /// <summary>
    ///     Construct Vb with the supplied OID and Null value
    /// </summary>
    /// <param name="oid">OID</param>
    public Vb(Oid? oid)
        : this()
    {
        _oid = oid?.Clone() as Oid;
        _value = new Null();
    }

    /// <summary>
    ///     Construct Vb with the OID and value
    /// </summary>
    /// <param name="oid">OID</param>
    /// <param name="value">Value</param>
    public Vb(Oid? oid, AsnType? value)
        : this(oid)
    {
        _value = value?.Clone() as AsnType;
    }

    /// <summary>
    ///     Construct Vb with the oid value and <seealso cref="Null" /> value.
    /// </summary>
    /// <param name="oid">String representing OID value to set</param>
    public Vb(string oid)
        : this()
    {
        _oid = new Oid(oid);
        _value = new Null();
    }

    /// <summary>
    ///     Copy constructor. Initialize class with cloned values from second class.
    /// </summary>
    /// <param name="second">Vb class to clone data from.</param>
    public Vb(Vb second)
        : this()
    {
        Set(second);
    }

    /// <summary>
    ///     SET/Get AsnType value of the Vb
    /// </summary>
    public AsnType? Value
    {
        set => _value = value?.Clone() as AsnType;
        get => _value;
    }

    /// <summary>
    ///     Get/SET OID of the Vb
    /// </summary>
    public Oid? Oid
    {
        set => _oid = value?.Clone() as Oid;
        get => _oid;
    }

    /// <summary>
    ///     Clone Vb object
    /// </summary>
    /// <returns>Cloned Vb object cast to System.Object</returns>
    public override object Clone()
    {
        return new Vb(_oid, _value);
    }

    /// <summary>
    ///     SET class value from supplied Vb class
    /// </summary>
    /// <param name="value">Vb class to clone data from</param>
    public void Set(Vb value)
    {
        _oid = value.Oid?.Clone() as Oid;
        _value = value.Value?.Clone() as Oid;
    }

    /// <summary>
    ///     Reset Vb value to Null
    /// </summary>
    public void ResetValue()
    {
        _value = new Null();
    }

    /// <summary>
    ///     Return printable string in the format oid: value
    /// </summary>
    /// <returns>Format Vb string</returns>
    public override string ToString()
    {
        return _oid?.ToString() + ": (" + SnmpConstants.GetTypeName(_value?.Type ?? UNIVERSAL) + ") " + _value;
    }

    /// <summary>
    ///     BER encode the variable binding
    /// </summary>
    /// <param name="buffer">
    ///     <see cref="MutableByte" /> class to the end of which encoded variable
    ///     binding values will be added.
    /// </param>
    public override void encode(MutableByte buffer)
    {
        // encode oid to the temporary buffer
        var oidbuf = new MutableByte();
        _oid?.encode(oidbuf);
        // encode value to a temporary buffer
        var valbuf = new MutableByte();
        _value?.encode(valbuf);

        // calculate data content length of the vb
        var vblen = oidbuf.Length + valbuf.Length;
        // encode vb header at the end of the result
        BuildHeader(buffer, Type, vblen);
        // add values to the encoded arrays to the end of the result
        buffer.Append(oidbuf);
        buffer.Append(valbuf);
    }

    /// <summary>
    ///     Decode BER encoded variable binding.
    /// </summary>
    /// <param name="buffer">
    ///     BER encoded buffer
    /// </param>
    /// <param name="offset">
    ///     Offset in the data buffer from where to start decoding. Offset is
    ///     passed by reference and will contain the offset of the byte immediately after the parsed
    ///     variable binding.
    /// </param>
    /// <returns>Buffer position after the decoded value</returns>
    public override int decode(byte[] buffer, int offset)
    {
        return decode(buffer.AsSpan(), offset);
    }

    public override int encode(Span<byte> buffer)
    {
        var written = BuildHeader(buffer, Type, MemberByteLength());
        // encode oid to the temporary buffer
        if (_oid is not null)
        {
            written += _oid.encode(buffer[written..]);
        }

        // encode value to a temporary buffer
        if (_value is not null)
        {
            written += _value.encode(buffer[written..]);
        }

        // calculate data content length of the vb
        return written;
    }

    public override int ByteLength
    {
        get
        {
            var mbl = MemberByteLength();
            var header = HeaderSize(mbl);
            return header + mbl;
        }
    }

    private int MemberByteLength() => (_oid?.ByteLength ?? 0) + (_value?.ByteLength ?? 0);

    public override int decode(Span<byte> buffer, int offset)
    {
        var asnType = ParseHeader(buffer, ref offset, out var headerLength);

        if (asnType != Type)
            throw new SnmpException($"Invalid ASN.1 type. Expected 0x{Type:x2} received 0x{asnType:x2}");

        // verify the length
        if (buffer.Length - offset < headerLength)
            throw new OverflowException("Buffer underflow error");

        _oid = new Oid();
        offset = _oid.decode(buffer, offset);
        var saveOffset = offset;
        // Look ahead in the header to see the data type we need to parse
        asnType = ParseHeader(buffer, ref saveOffset, out headerLength);
        _value = SnmpConstants.GetSyntaxObject(asnType);
        switch (_value)
        {
            case null:
                throw new SnmpDecodingException(
                    $"Invalid ASN.1 type encountered 0x{asnType:x2}. Unable to continue decoding.");
            default:
                offset = _value.decode(buffer, offset);
                return offset;
        }
    }
}