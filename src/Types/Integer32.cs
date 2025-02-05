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

/// <summary>ASN.1 Integer32 class.</summary>
/// <remarks>
///     This class defines the SNMP 32-bit signed integer
///     used by the SNMP SMI. This class also serves as a
///     base class for any additional SNMP SMI types that
///     exits now or may be defined in the future.
/// </remarks>
[Serializable]
public class Integer32 : AsnType, IComparable<Integer32>, IComparable<int>, ICloneable
{
    /// <summary>Internal class value</summary>
    protected int _value;

    /// <summary>
    ///     Constructor
    /// </summary>
    public Integer32()
    {
        _asnType = SnmpConstants.SMI_INTEGER;
    }

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="val">
    ///     Class value initializer
    /// </param>
    public Integer32(int val) : this()
    {
        _value = val;
    }


    /// <summary>
    ///     Copy constructor
    /// </summary>
    /// <param name="second">
    ///     Class value initializer
    /// </param>
    public Integer32(Integer32 second) : this()
    {
        Set(second);
    }

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="val">
    ///     Integer value in a string format.
    /// </param>
    public Integer32(string val) : this()
    {
        Set(val);
    }

    /// <summary>
    ///     Get/SET internal integer value
    /// </summary>
    public int Value
    {
        get => _value;
        set => _value = value;
    }

    /// <summary> Returns a duplicate of the current object.</summary>
    /// <returns> A newly allocated duplicate object.</returns>
    public override object Clone()
    {
        return new Integer32(this);
    }

    /// <summary>
    ///     Compare implementation that will compare this class value with argument Int32 value.
    /// </summary>
    /// <param name="other">Int32 value to compare class value with.</param>
    /// <returns>
    ///     less than 0 if is parameter is less then, 0 if parameter is equal and greater than 0 if parameter is greater
    ///     than the class value
    /// </returns>
    public int CompareTo(int other)
    {
        return _value.CompareTo(other);
    }

    /// <summary>
    ///     Compare implementation that will compare this class value with the value of another <see cref="Integer32" /> class.
    /// </summary>
    /// <param name="other">Integer32 value to compare class value with.</param>
    /// <returns>
    ///     less than 0 if is parameter is less then, 0 if parameter is equal and greater than 0 if parameter is greater
    ///     than the class value
    /// </returns>
    public int CompareTo(Integer32? other)
    {
        return other is null ? 1 : _value.CompareTo(other.Value);
    }

    /// <summary>
    ///     SET class value from another Integer32 class cast as <see cref="AsnType" />.
    /// </summary>
    /// <param name="value">Integer32 class cast as <see cref="AsnType" /></param>
    /// <exception cref="ArgumentException">Argument is not Integer32 type.</exception>
    public void Set(AsnType? value)
    {
        switch (value)
        {
            case Integer32 val:
                _value = val.Value;
                break;
            default:
                throw new ArgumentException("Invalid argument type.");
        }
    }

    /// <summary>
    ///     Parse an Integer32 value from a string.
    /// </summary>
    /// <param name="value">String containing an Integer32 value</param>
    /// <exception cref="ArgumentOutOfRangeException">Argument string is length == 0</exception>
    /// <exception cref="ArgumentException">Unable to parse Integer32 value from the argument.</exception>
    public void Set(string value)
    {
        switch (value.Length)
        {
            case <= 0:
                throw new ArgumentOutOfRangeException(value, "String has to be length greater then 0");
            default:
                try
                {
                    _value = int.Parse(value);
                }
                catch
                {
                    throw new ArgumentException("Invalid argument format.");
                }

                break;
        }
    }

    /// <summary> Returns the string representation of the object.</summary>
    /// <returns>String representation of the class value.</returns>
    public override string ToString()
    {
        return _value.ToString();
    }

    /// <summary>
    ///     Implicit casting of Integer32 value as Int32 value
    /// </summary>
    /// <param name="value">Integer32 class whose value is cast as Int32 value</param>
    /// <returns>Int32 value of the Integer32 class.</returns>
    public static implicit operator int(Integer32? value)
    {
        if (value is null)
            return 0;
        return value.Value;
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
    ///     Set class value to a random integer.
    /// </summary>
    public void SetRandom()
    {
        var rand = new Random();
        _value = rand.Next();
    }

    /// <summary>
    ///     Compare class value against the object argument. Supported argument types are
    ///     <see cref="Integer32" /> and Int32.
    /// </summary>
    /// <param name="obj">Object to compare values with</param>
    /// <returns>True if object value is the same as this class, otherwise false.</returns>
    public override bool Equals(object? obj)
    {
        switch (obj)
        {
            case Integer32 integer32:
                return _value.Equals(integer32.Value);
            case int i32:
                return _value.Equals(i32);
            default:
                return false; // last resort
        }
    }

    /// <summary>
    ///     Comparison operator
    /// </summary>
    /// <param name="first">First <see cref="Integer32" /> class value to compare</param>
    /// <param name="second">Second <see cref="Integer32" /> class value to compare</param>
    /// <returns>True if class values match, otherwise false</returns>
    public static bool operator ==(Integer32? first, Integer32? second)
    {
        if (ReferenceEquals(first, second)) return true;
        if (first is null && second is null) return true;
        if (first is null || second is null) return false;
        return first.Equals(second);
    }

    /// <summary>
    ///     Negative comparison operator
    /// </summary>
    /// <param name="first">First <see cref="Integer32" /> class value to compare</param>
    /// <param name="second">Second <see cref="Integer32" /> class value to compare</param>
    /// <returns>True if class values do NOT match, otherwise false</returns>
    public static bool operator !=(Integer32 first, Integer32 second)
    {
        return !(first == second);
    }

    /// <summary>
    ///     Addition operator.
    /// </summary>
    /// <remarks>
    ///     Add two Integer32 object values. Values of the two objects are added and
    ///     a new class is instantiated with the result. Original values of the two parameter classes
    ///     are preserved.
    /// </remarks>
    /// <param name="first">First Integer32 object</param>
    /// <param name="second">Second Integer32 object</param>
    /// <returns>
    ///     New object with values of the 2 parameter objects added. If both parameters are null
    ///     references then null is returned. If either of the two parameters is null, the non-null objects
    ///     value is set as the value of the new object and returned.
    /// </returns>
    public static Integer32? operator +(Integer32? first, Integer32? second)
    {
        if (first is null && second is null) return null;
        if (first is null) return second;
        return second is null ? first : new Integer32(first.Value + second.Value);
    }

    /// <summary>
    ///     Subtraction operator
    /// </summary>
    /// <remarks>
    ///     Subtract the value of the second Integer32 class value from the first Integer32 class value.
    ///     Values of the two objects are subtracted and a new class is instantiated with the result.
    ///     Original values of the two parameter classes are preserved.
    /// </remarks>
    /// <param name="first">First Integer32 object</param>
    /// <param name="second">Second Integer32 object</param>
    /// <returns>
    ///     New object with subtracted values of the 2 parameter objects. If both parameters are null
    ///     references then null is returned. If either of the two parameters is null, the non-null objects
    ///     value is set as the value of the new object and returned.
    /// </returns>
    public static Integer32? operator -(Integer32? first, Integer32? second)
    {
        if (first is null && second is null) return null;
        if (first is null) return second;
        if (second is null) return first;

        return new Integer32(first.Value - second.Value);
    }

    #region encode and decode methods

    /// <summary>
    ///     Used to encode the integer value into an ASN.1 buffer.
    ///     The passed encoder defines the method for encoding the
    ///     data.
    /// </summary>
    /// <param name="buffer">Buffer target to write the encoded data</param>
    public override void encode(MutableByte buffer)
    {
        var val = _value;

        var b = BitConverter.GetBytes(_value);

        var tmp = new MutableByte();
        switch (val)
        {
            // if value is negative
            case < 0:
            {
                for (var i = 3; i >= 0; i--)
                    if (tmp.Length > 0 || b[i] != 0xff)
                        tmp.Append(b[i]);

                switch (tmp.Length)
                {
                    // if the value is -1 then all bytes in an integer are 0xff and will be skipped above
                    case 0:
                        tmp.Append(0xff);
                        break;
                }

                switch (tmp[0] & 0x80)
                {
                    // make sure value is negative
                    case 0:
                        tmp.Prepend(0xff);
                        break;
                }

                break;
            }
            case 0:
                // this is just a shortcut to save processing time
                tmp.Append(0);
                break;
            default:
            {
                // byte[] b = BitConverter.GetBytes(val);
                for (var i = 3; i >= 0; i--)
                    if (b[i] != 0 || tmp.Length > 0)
                        tmp.Append(b[i]);
                switch (tmp.Length)
                {
                    // if buffer length is 0 then value is 0, and we have to add it to the buffer
                    case 0:
                        tmp.Append(0);
                        break;
                    default:
                    {
                        if ((tmp[0] & 0x80) != 0)
                            // first bit of the first byte has to be 0 otherwise value is negative.
                            tmp.Prepend(0);
                        break;
                    }
                }

                break;
            }
        }

        switch (tmp.Length)
        {
            // check for 9 1s at the beginning of the encoded value
            case > 1 when tmp[0] == 0xff && (tmp[1] & 0x80) != 0:
                tmp.Prepend(0);
                break;
        }

        BuildHeader(buffer, Type, tmp.Length);

        buffer.Append(tmp);
    }

    /// <summary>
    ///     Used to encode the integer value into an ASN.1 buffer.
    ///     The passed encoder defines the method for encoding the
    ///     data.
    /// </summary>
    /// <param name="buffer">Buffer target to write the encoded data</param>
    public override int encode(Span<byte> buffer)
    {
        var slice = BuildHeader(buffer, Type, MemberLength());
        var encoded = EncodeValue(buffer[slice..]);
        return slice + encoded;
    }

    private int EncodeValue(Span<byte> buffer)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BitConverter.TryWriteBytes(bytes, _value);

        var length = 0;

        switch (_value)
        {
            // if value is negative
            case < 0:
            {
                for (var i = 3; i >= 0; i--)
                {
                    if (length > 0 || bytes[i] != 0xff)
                    {
                        buffer[length++] = bytes[i];
                    }
                }

                // if the value is -1 then all bytes in an integer are 0xff and will be skipped above
                if (length == 0) buffer[length++] = 0xff;

                // make sure value is negative
                if ((buffer[0] & 0x80) == 0)
                {
                    buffer[..length].CopyTo(buffer[1..]);
                    length++;
                    buffer[0] = 0xff;
                }

                break;
            }
            case 0:
                // this is just a shortcut to save processing time
                length++;
                break;
            default:
            {
                for (var i = 3; i >= 0; i--)
                    if (bytes[i] != 0 || length > 0)
                        buffer[length++] = bytes[i];
                // if buffer length is 0 then value is 0, and we have to add it to the buffer
                if (length == 0)
                {
                    buffer[length++] = 0;
                }
                else if ((buffer[0] & 0x80) != 0)
                {
                    // first bit of the first byte has to be 0 otherwise value is negative.
                    buffer[..length].CopyTo(buffer[1..]);
                    length++;
                    buffer[0] = 0;
                }

                break;
            }
        }

        if (length <= 1) return length;
        // check for 9 1s at the beginning of the encoded value
        if (buffer[0] != 0xff || (buffer[1] & 0x80) == 0) return length;
        // first bit of the first byte has to be 0 otherwise value is negative.
        buffer[..length].CopyTo(buffer[1..]);
        length++;
        buffer[0] = 0;

        return length;
    }

    public const int MaxEncodedSize = MaxHeaderSize + sizeof(int);

    /// <summary>
    ///     Used to decode the integer value from the BER buffer.
    ///     The passed encoder is used to decode the BER encoded information
    ///     and the integer value is stored in the internal object.
    /// </summary>
    /// <param name="buffer">Buffer holding BER encoded data</param>
    /// <param name="offset">Offset in the buffer to start parsing from</param>
    /// <returns>Buffer position after the decoded value</returns>
    public override int decode(byte[] buffer, int offset)
    {
        return decode(buffer.AsSpan(), offset);
    }

    /// <summary>
    ///     Used to decode the integer value from the BER buffer.
    ///     The passed encoder is used to decode the BER encoded information
    ///     and the integer value is stored in the internal object.
    /// </summary>
    /// <param name="buffer">Buffer holding BER encoded data</param>
    /// <param name="offset">Offset in the buffer to start parsing from</param>
    /// <returns>Buffer position after the decoded value</returns>
    public override int decode(Span<byte> buffer, int offset)
    {
        //
        // parse the header first
        //
        var asnType = ParseHeader(buffer, ref offset, out var headerLength);

        if (asnType != Type)
            throw new SnmpException("Invalid ASN.1 type");

        if (buffer.Length - offset < headerLength)
            throw new OverflowException("Buffer underflow error");

        var isNegative = false;

        switch (headerLength)
        {
            case > 5:
                throw new OverflowException("Integer size is invalid. Unable to decode.");
        }

        if ((buffer[offset] & HIGH_BIT) != 0) isNegative = true;
        switch (buffer[offset])
        {
            case 0x80 when headerLength > 2 &&
                           buffer[offset + 1] == 0xff && (buffer[offset + 2] & 0x80) != 0:
                // this is a filler byte to comply with no 9 x consecutive 1s
                offset += 1;
                headerLength -= 1; // we've used one byte of the encoded length
                break;
        }

        switch (isNegative)
        {
            case true:
                _value = -1;
                break;
            default:
                _value = 0;
                break;
        }

        for (var i = 0; i < headerLength; i++)
        {
            _value <<= 8;
            _value |= buffer[offset++];
        }

        return offset;
    }

    public override int ByteLength
    {
        get
        {
            var length = MemberLength();
            return HeaderSize(length) + length;
        }
    }

    private int MemberLength()
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BitConverter.TryWriteBytes(bytes, Value);

        var length = 0;
        var zeroth = -1;

        switch (Value)
        {
            case < 0:
            {
                for (var i = 3; i >= 0; i--)
                {
                    if (bytes[i] == 0xff) continue;
                    zeroth = i;
                    length += i + 1;
                    break;
                }

                if (length == 0)
                {
                    length++;
                }

                if (zeroth != -1)
                {
                    if ((bytes[zeroth] & 0x80) == 0)
                    {
                        length++;
                    }
                }

                break;
            }
            case 0:
                length++;
                break;
            default:
            {
                for (var i = 3; i >= 0; i--)
                {
                    if (bytes[i] == 0) continue;
                    zeroth = i;
                    length += i + 1;
                    break;
                }

                if (length == 0)
                {
                    length++;
                }

                if (zeroth != -1)
                {
                    if ((bytes[zeroth] & 0x80) != 0)
                    {
                        length++;
                    }
                }

                break;
            }
        }

        if (length <= 1) return length;
        if (zeroth <= 0) return length;
        var slice = bytes.Slice(zeroth - 1, 2);
        if (slice[0] == 0xff && (slice[1] & 0x80) != 0)
        {
            length++;
        }

        return length;
    }

    #endregion encode and decode methods
}