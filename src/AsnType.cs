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
///     Base class for all ASN.1 value classes
/// </summary>
public abstract class AsnType
{
    /// <summary>Bool true/false value type</summary>
    public const byte BOOLEAN = 0x01;

    /// <summary>Signed 32-bit integer type</summary>
    public const byte INTEGER = 0x02;

    /// <summary>Bit sequence type</summary>
    public const byte BITSTRING = 0x03;

    /// <summary>Octet (byte) value type</summary>
    public const byte OCTETSTRING = 0x04;

    /// <summary>Null (no value) type</summary>
    public const byte NULL = 0x05;

    /// <summary>Object id type</summary>
    public const byte OBJECTID = 0x06;

    /// <summary>Arbitrary data type</summary>
    public const byte SEQUENCE = 0x10;

    /// <summary>
    ///     Defined by referencing a fixed, unordered list of types,
    ///     some of which may be declared optional. Each value is an
    ///     unordered list of values, one from each component type.
    /// </summary>
    public const byte SET = 0x11;

    /// <summary>
    ///     Generally useful, application-independent types and
    ///     construction mechanisms.
    /// </summary>
    public const byte UNIVERSAL = 0x00;

    /// <summary>
    ///     Relevant to a particular application. These are defined
    ///     in standards other than ASN.1.
    /// </summary>
    public const byte APPLICATION = 0x40;

    /// <summary>
    ///     Also relevant to a particular application, but limited by context
    /// </summary>
    public const byte CONTEXT = 0x80;

    /// <summary>
    ///     These are types not covered by any standard but instead defined by users.
    /// </summary>
    public const byte PRIVATE = 0xC0;

    /// <summary> A primitive data object.</summary>
    public const byte PRIMITIVE = 0x00;

    /// <summary> A constructed data object such as a set or sequence.</summary>
    public const byte CONSTRUCTOR = 0x20;

    /// <summary>
    ///     Defines the "high bit" that is the sign extension bit for a 8-bit signed value.
    /// </summary>
    protected const byte HIGH_BIT = 0x80;

    /// <summary>
    ///     Defines the BER extension "value" that is used to mark an extension type.
    /// </summary>
    protected const byte EXTENSION_ID = 0x1F;

    /// <summary>
    ///     ASN.1 type byte.
    /// </summary>
    protected byte _asnType;

    /// <summary>
    ///     Get ASN.1 value type stored in this class.
    /// </summary>
    public byte Type
    {
        get => _asnType;
        set => _asnType = value;
    }

    /// <summary>
    ///     Abstract Clone() member function
    /// </summary>
    /// <returns>Duplicated current object cast as Object</returns>
    public abstract object Clone();
    /// <summary>
    ///     Encodes the data object in the specified buffer
    /// </summary>
    /// <param name="buffer">The buffer to write the encoded information</param>
    public abstract int Encode(Span<byte> buffer);

    /// <summary>
    ///     Decodes the ASN.1 buffer and sets the values in the AsnType object.
    /// </summary>
    /// <param name="buffer">The encoded data buffer</param>
    /// <param name="offset">The offset of the first valid byte.</param>
    /// <returns>
    ///     New offset pointing to the byte after the last decoded position
    /// </returns>
    public abstract int Decode(ReadOnlySpan<byte> buffer, int offset);

    /// <summary>
    ///     Append BER encoded length to the <see cref="byte[]" />
    /// </summary>
    /// <param name="mb">MutableArray to append BER encoded length to</param>
    /// <param name="asnLength">Length value to encode.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when length value to encode is less then 0</exception>
    internal static int BuildLength(Span<byte> mb, int asnLength)
    {
        return new SnmpLength(asnLength).CopyTo(mb);
    }

    /// <summary>
    ///     byte[] version of ParseLength. Retrieve BER encoded length from a byte array at supplied offset
    /// </summary>
    /// <param name="mb">BER encoded data</param>
    /// <param name="offset">Offset to start parsing length from</param>
    /// <returns>Length value</returns>
    /// <exception cref="OverflowException">Thrown when buffer is too short</exception>
    internal static int ParseLength(ReadOnlySpan<byte> mb, ref int offset)
    {
        var span = mb[offset..];
        var length = new SnmpLength(span);
        offset += length.ByteLength;
        return length;
    }
    public static int BuildHeader(Span<byte> mb, byte asnType, int asnLength)
    {
        return new SnmpHeader(asnType, asnLength).CopyTo(mb);
    }

    public const int MaxHeaderSize = 2 + sizeof(int);

    public static int HeaderSize(int asnLength)
    {
        if (asnLength < 128) return 2;
        var res = 2;
        while (asnLength != 0)
        {
            asnLength >>= 8;
            res++;
        }

        return res;
    }

    public virtual int ByteLength => HeaderSize(0);

    /// <summary>
    ///     Parse ASN.1 header.
    /// </summary>
    /// <param name="buffer">BER encoded data</param>
    /// <param name="offset">Offset in the packet to start parsing the header from</param>
    /// <param name="length">Length of the data in the section starting with parsed header</param>
    /// <returns>ASN.1 type of the header</returns>
    /// <exception cref="OverflowException">Thrown when buffer is too short</exception>
    /// <exception cref="SnmpException">Thrown when invalid type is encountered in the header</exception>
    internal static byte ParseHeader(ReadOnlySpan<byte> buffer, ref int offset, out int length)
    {
        var header = new SnmpHeader(buffer[offset..]);
        length = header.Length;
        offset += header.ByteLength;
        return header.AsnType;
    }
}