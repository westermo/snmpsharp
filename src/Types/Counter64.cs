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
using System.Globalization;

namespace SnmpSharpNet;

/// <summary>ASN.1 Counter64 value implementation.</summary>
/// <remarks>
///     Counter64 value is unsigned 64-bit integer value that is incremented by the agent
///     until maximum value is reached. When maximum value is reached, Counter64 value will
///     roll over to 0.
/// </remarks>
[Serializable]
public class Counter64 : AsnType, IComparable<ulong>, IComparable<Counter64>, ICloneable
{
    /// <summary>
    ///     Internal 64-bit unsigned integer value.
    /// </summary>
    protected ulong _value;


    /// <summary>
    ///     Constructor.
    /// </summary>
    public Counter64()
    {
        _asnType = SnmpConstants.SMI_COUNTER64;
    }

    /// <summary>Constructor.</summary>
    /// <param name="value">
    ///     Value to set class value.
    /// </param>
    public Counter64(long value) : this()
    {
        _value = Convert.ToUInt64(value);
    }

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="value">Copy value</param>
    public Counter64(Counter64 value) : this()
    {
        _value = value.Value;
    }

    /// <summary>Constructor.</summary>
    /// <param name="value">
    ///     Value to initialize the class with.
    /// </param>
    public Counter64(ulong value) : this()
    {
        _value = value;
    }

    /// <summary>Constructor.</summary>
    /// <remarks>
    ///     Initialize the class by parsing a 64-bit unsigned integer value
    ///     from the supplied string value.
    /// </remarks>
    /// <param name="value">
    ///     64-bit unsigned integer value encoded as a string.
    /// </param>
    public Counter64(string value) : this()
    {
        Set(value);
    }

    /// <summary>
    ///     Get/Set class 64-bit unsigned value
    /// </summary>
    public virtual ulong Value
    {
        get => _value;

        set => _value = value;
    }


    /// <summary>Duplicate current object.</summary>
    /// <returns>Duplicate of the current object.</returns>
    public override object Clone()
    {
        return new Counter64(this);
    }

    /// <summary>
    ///     Compare class value with the value of the second class
    /// </summary>
    /// <param name="other">Class whose value we are comparing with</param>
    /// <returns>
    ///     less than 0 if is parameter is less then, 0 if parameter is equal and greater than 0 if parameter is greater
    ///     than the class value
    /// </returns>
    public int CompareTo(Counter64? other)
    {
        if (other is null)
            return -1;
        return _value.CompareTo(other.Value);
    }


    /// <summary>
    ///     Compare class value with the UInt64 variable
    /// </summary>
    /// <param name="other">Variable to compare with</param>
    /// <returns>
    ///     less than 0 if is parameter is less then, 0 if parameter is equal and greater than 0 if parameter is greater
    ///     than the class value
    /// </returns>
    public int CompareTo(ulong other)
    {
        return _value.CompareTo(other);
    }

    /// <summary>
    ///     SET class value from another Counter64 class cast as <seealso cref="AsnType" />.
    /// </summary>
    /// <param name="value">Counter64 class cast as <seealso cref="AsnType" /></param>
    /// <exception cref="ArgumentException">Argument is not Counter64 type.</exception>
    public void Set(AsnType value)
    {
        if (value is Counter64 val)
            _value = val.Value;
        else
            throw new ArgumentException("Invalid argument type.");
    }

    /// <summary>
    ///     Parse a Counter64 value from a string.
    /// </summary>
    /// <param name="value">String containing a Counter64 value</param>
    /// <exception cref="ArgumentOutOfRangeException">Argument length is 0 (zero)</exception>
    /// <exception cref="ArgumentException">Unable to parse Counter64 value from the argument.</exception>
    public void Set(string value)
    {
        switch (value.Length)
        {
            case <= 0:
                throw new ArgumentOutOfRangeException(value, "String has to be length greater then 0");
            default:
                try
                {
                    _value = Convert.ToUInt64(value, CultureInfo.CurrentCulture);
                }
                catch
                {
                    throw new ArgumentException("Invalid argument format.");
                }

                break;
        }
    }

    /// <summary>
    ///     Return class value hash code
    /// </summary>
    /// <returns>Int32 hash of the class stored value</returns>
    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    /// <summary>
    ///     Compare class value against the object argument. Supported argument types are
    ///     <see cref="Counter64" /> and UInt64.
    /// </summary>
    /// <param name="obj">Object to compare values with</param>
    /// <returns>True if object value is the same as this class, otherwise false.</returns>
    public override bool Equals(object? obj)
    {
        return obj switch
        {
            null => false,
            Counter64 counter64 => _value.Equals(counter64.Value),
            ulong @ulong => _value.Equals(@ulong),
            _ => false
        };
    }

    /// <summary>
    ///     Returns the string representation of the object.
    /// </summary>
    /// <returns>String representation of the object value</returns>
    public override string ToString()
    {
        return Value.ToString(CultureInfo.CurrentCulture);
    }

    /// <summary>
    ///     Implicit casting of Counter64 value as UInt64 value
    /// </summary>
    /// <param name="value">Counter64 class whose value is cast as UInt64 value</param>
    /// <returns>UInt64 value of the Counter64 class.</returns>
    public static implicit operator ulong(Counter64 value)
    {
        return value.Value;
    }

    /// <summary>
    ///     Check for equality of class values
    /// </summary>
    /// <param name="first">First class value to compare</param>
    /// <param name="second">Second class value to compare</param>
    /// <returns>True if values are the same, otherwise false</returns>
    public static bool operator ==(Counter64? first, Counter64? second)
    {
        if (ReferenceEquals(first, second)) return true;
        if (first is null && second is null) return true;
        if (first is null || second is null) return false;
        return first.Equals(second);
    }

    /// <summary>
    ///     Negative comparison
    /// </summary>
    /// <param name="first">First class value to compare</param>
    /// <param name="second">Second class value to compare</param>
    /// <returns>False if values are the same, otherwise true</returns>
    public static bool operator !=(Counter64 first, Counter64 second)
    {
        return !(first == second);
    }

    /// <summary>
    ///     Greater then operator
    /// </summary>
    /// <remarks>Compare two Counter64 class values and return true if first class value is greater than second.</remarks>
    /// <param name="first">First class</param>
    /// <param name="second">Second class</param>
    /// <returns>True if first class value is greater than second class value, otherwise false</returns>
    public static bool operator >(Counter64? first, Counter64? second)
    {
        return first?.Value > second?.Value;
    }

    /// <summary>
    ///     Less than operator
    /// </summary>
    /// <remarks>Compare two Counter64 class values and return true if first class value is less than second.</remarks>
    /// <param name="first">First class</param>
    /// <param name="second">Second class</param>
    /// <returns>True if first class value is less than second class value, otherwise false</returns>
    public static bool operator <(Counter64? first, Counter64? second)
    {
        return first?.Value < second?.Value;
    }

    /// <summary>
    ///     Addition operator.
    /// </summary>
    /// <remarks>
    ///     Add two Counter64 object values. Values of the two objects are added and
    ///     a new class is instantiated with the result. Original values of the two parameter classes
    ///     are preserved.
    /// </remarks>
    /// <param name="first">First Counter64 object</param>
    /// <param name="second">Second Counter64 object</param>
    /// <returns>
    ///     New object with values of the 2 parameter objects added. If both parameters are null
    ///     references then null is returned. If either of the two parameters is null, the non-null objects
    ///     value is set as the value of the new object and returned.
    /// </returns>
    public static Counter64 operator +(Counter64? first, Counter64? second)
    {
        return new Counter64(first?.Value ?? 0 + second?.Value ?? 0);
    }

    /// <summary>
    ///     Subtraction operator
    /// </summary>
    /// <remarks>
    ///     Subtract the value of the second Counter64 class value from the first Counter64 class value.
    ///     Values of the two objects are subtracted and a new class is instantiated with the result.
    ///     Original values of the two parameter classes are preserved.
    /// </remarks>
    /// <param name="first">First Counter64 object</param>
    /// <param name="second">Second Counter64 object</param>
    /// <returns>
    ///     New object with subtracted values of the 2 parameter objects. If both parameters are null
    ///     references then null is returned. If either of the two parameters is null, the non-null objects
    ///     value is set as the value of the new object and returned.
    /// </returns>
    public static Counter64 operator -(Counter64? first, Counter64? second)
    {
        return new Counter64(first?.Value ?? 0 - second?.Value ?? 0);
    }

    /// <summary>
    ///     Return difference between two Counter64 values taking counter roll-over into account.
    /// </summary>
    /// <param name="first">First or older value</param>
    /// <param name="second">Second or newer value</param>
    /// <returns>Difference between the two values</returns>
    public static ulong Diff(Counter64 first, Counter64 second)
    {
        var f = first.Value;
        var s = second.Value;
        var res =
            // in case of a roll-over event
            s > f ? ulong.MaxValue - f + s : s - f;
        return res;
    }

    #region Encode & Decode methods

    /// <summary>BER encode class value</summary>
    /// <param name="buffer">
    ///     MutableByte to append BER encoded value to.
    /// </param>
    public override void encode(MutableByte buffer)
    {
        var b = BitConverter.GetBytes(_value);
        var tmp = new MutableByte();
        for (var i = b.Length - 1; i >= 0; i--)
            if (b[i] != 0 || tmp.Length > 0)
                tmp.Append(b[i]);
        switch (tmp.Length)
        {
            case 0:
                tmp.Append(0); // value is 0. can't have an empty encoding
                break;
        }

        BuildHeader(buffer, Type, tmp.Length);
        buffer.Append(tmp);
    }

    /// <summary>BER encode class value</summary>
    /// <param name="buffer">
    ///     MutableByte to append BER encoded value to.
    /// </param>
    public override int encode(Span<byte> buffer)
    {
        var slice = BuildHeader(buffer, Type, MemberByteLength());
        var length = EncodeValue(buffer[slice..]);
        return slice + length;
    }

    private int EncodeValue(Span<byte> buffer)
    {
        Span<byte> b = stackalloc byte[sizeof(ulong)];
        BitConverter.TryWriteBytes(b, _value);
        var length = 0;
        for (var i = b.Length - 1; i >= 0; i--)
            if (b[i] != 0 || length > 0)
                buffer[length++] = b[i];
        if (length == 0) length++; // value is 0. can't have an empty encoding
        return length;
    }

    public const int MaxEncodedSize = MaxHeaderSize + sizeof(ulong);

    /// <summary>
    ///     Decode BER encoded Counter64 value
    /// </summary>
    /// <param name="buffer">The encoded ASN.1 data</param>
    /// <param name="offset">Offset to start value decoding from.</param>
    /// <returns>Offset after the parsed value.</returns>
    public override int decode(byte[] buffer, int offset)
    {
        return decode(buffer.AsSpan(), offset);
    }

    /// <summary>
    ///     Decode BER encoded Counter64 value
    /// </summary>
    /// <param name="buffer">The encoded ASN.1 data</param>
    /// <param name="offset">Offset to start value decoding from.</param>
    /// <returns>Offset after the parsed value.</returns>
    public override int decode(Span<byte> buffer, int offset)
    {
        //
        // parse the header first
        //
        var asnType = ParseHeader(buffer, ref offset, out var headerLength);

        if (asnType != Type)
            throw new SnmpException("Invalid ASN.1 type.");

        // check for sufficient data
        if (buffer.Length - offset < headerLength)
            throw new OverflowException("Buffer underflow error");

        switch (headerLength)
        {
            // check to see that we can actually decode
            // the value (must fit in integer == 64-bits)
            case > 9:
                throw new OverflowException("Integer too large: cannot decode");
        }

        Span<byte> tmpBuf = stackalloc byte[8]; // we need 8 bytes to represent a UInt64
        switch (headerLength)
        {
            case 9:
                // if length is 9 we have a padding byte added. Skip it
                offset += 1;
                headerLength -= 1;
                break;
        }

        while (headerLength > 0)
        {
            tmpBuf[headerLength - 1] = buffer[offset];
            offset += 1;
            headerLength -= 1;
        }

        _value = BitConverter.ToUInt64(tmpBuf);

        return offset;
    }

    public override int ByteLength
    {
        get
        {
            var length = MemberByteLength();
            return HeaderSize(length) + length;
        }
    }

    private int MemberByteLength()
    {
        Span<byte> b = stackalloc byte[sizeof(ulong)];
        BitConverter.TryWriteBytes(b, Value);
        var length = 0;
        for (var i = b.Length - 1; i >= 0; i--)
        {
            if (b[i] == 0) continue;
            length += i + 1;
            break;
        }

        if (length == 0) length++; // value is 0. can't have an empty encoding
        return length;
    }

    #endregion
}