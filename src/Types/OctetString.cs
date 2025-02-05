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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace SnmpSharpNet;

/// <summary>ASN.1 OctetString type implementation</summary>
[Serializable]
[SuppressMessage("ReSharper", "VirtualMemberCallInConstructor")]
public class OctetString : AsnType, ICloneable, IComparable<byte[]>, IComparable<OctetString>, IEnumerable<byte>
{
    /// <summary>Data buffer</summary>
    protected byte[] _data = [];

    /// <summary>Constructor</summary>
    public OctetString()
    {
        _asnType = SnmpConstants.SMI_STRING;
    }

    /// <summary>
    ///     Constructs an Octet String with the contents of the supplied string.
    /// </summary>
    /// <param name="data">String data to convert into OctetString class value.</param>
    public OctetString(string data) : this()
    {
        Set(data);
    }

    /// <summary>
    ///     Constructs the object and sets the data buffer to the byte array values
    /// </summary>
    /// <param name="data">Byte array to copy into the data buffer</param>
    public OctetString(byte[] data) : this()
    {
        Set(data);
    }

    /// <summary>
    ///     Construct the class and set value. Value can be set by reference (assigned passed array parameter
    ///     to the interal class variable) or by value (copy data in the array into a new buffer).
    /// </summary>
    /// <param name="data">Byte array to set class value to</param>
    /// <param name="useReference">
    ///     If true, set class value to reference byte array parameter, otherwise copy data into new
    ///     internal byte array
    /// </param>
    public OctetString(byte[] data, bool useReference) : this()
    {
        switch (useReference)
        {
            case true:
                SetRef(data);
                break;
            default:
                Set(data);
                break;
        }
    }

    /// <summary>Constructor creating class from values in the supplied class.</summary>
    /// <param name="second">OctetString object to copy data from.</param>
    public OctetString(OctetString second) : this()
    {
        Set(second);
    }

    /// <summary>
    ///     Constructor. Initialize the class value to a 1 byte array with the supplied value
    /// </summary>
    /// <param name="data">Value to initialize the class data to.</param>
    public OctetString(byte data) : this()
    {
        Set(data);
    }

    /// <summary>Get length of the internal byte array. 0 if byte array is undefined or zero length.</summary>
    public int Length => _data.Length;

    /// <summary>
    ///     Indexed access to the OctetString class data members.
    ///     <code>
    /// OctetString os = new OctetString("test");
    /// for(int i = 0;i &lt; os.Length;i++) {
    ///  Console.WriteLine("{0}",os[i]);
    /// }
    /// </code>
    /// </summary>
    /// <param name="index">Index position of the data value to access</param>
    /// <returns>Byte value at the index position. 0 if index is out of range</returns>
    public byte this[int index]
    {
        get
        {
            if (index < 0 || index >= Length) return 0; // Don't throw exceptions here
            return _data[index];
        }
        set
        {
            if (index < 0 || index >= Length) return; // Don't throw exceptions here
            _data[index] = value;
        }
    }

    /// <summary>
    ///     Return true if OctetString contains non-printable characters, otherwise return false.
    /// </summary>
    /// <remarks>
    ///     Values recognized as hex are byte values less then decimal 32 that are not decimal
    ///     10 or 13 and byte values that are greater then 127 decimal. One exception is byte value 0x00
    ///     when it is at the end of the byte array is not considered a hex value but a c-like string
    ///     termination character.
    /// </remarks>
    public bool IsHex
    {
        get
        {
            var isHex = false;
            for (var i = 0; i < _data.Length; i++)
            {
                var b = _data[i];
                switch (b)
                {
                    case < 32:
                    {
                        if (b != 10 && b != 13 && !(b == 0x00 && _data.Length - 1 == i)) isHex = true;
                        break;
                    }
                    case > 127:
                        isHex = true;
                        break;
                }
            }

            return isHex;
        }
    }

    /// <summary>Creates a duplicate copy of the object and returns it to the caller.</summary>
    /// <returns> A newly constructed copy of self</returns>
    public override object Clone()
    {
        return new OctetString(this);
    }

    /// <summary>
    ///     IComparable interface implementation. Compare class contents with contents of the byte array.
    /// </summary>
    /// <param name="other">Byte array to compare against</param>
    /// <returns>-1 if class value is greater (longer or higher value), 1 if byte array is greater or 0 if the same</returns>
    public int CompareTo(byte[]? other)
    {
        switch (other)
        {
            case null:
                return -1;
        }

        if (_data.Length > other.Length)
            return -1;
        if (_data.Length < other.Length)
            return 1;
        for (var i = 0; i < _data.Length; i++)
        {
            if (_data[i] > other[i])
                return -1;
            if (_data[i] < other[i])
                return 1;
        }

        return 0;
    }

    /// <summary>
    ///     IComparable interface implementation. Compare class contents against another class.
    /// </summary>
    /// <param name="other">OctetString class to compare against.</param>
    /// <returns>-1 if class value is greater (longer or higher value), 1 if byte array is greater or 0 if the same</returns>
    public int CompareTo(OctetString? other)
    {
        return CompareTo(other?.GetData());
    }

    /// <summary>
    ///     Returns an enumerator that iterates through the OctetString byte collection
    /// </summary>
    /// <returns>An IEnumerator  object that can be used to iterate through the collection.</returns>
    public IEnumerator<byte> GetEnumerator()
    {
        return ((IEnumerable<byte>)_data).GetEnumerator();
    }

    /// <summary>
    ///     Returns an enumerator that iterates through the OctetString byte collection
    /// </summary>
    /// <returns>An IEnumerator  object that can be used to iterate through the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return _data.GetEnumerator();
    }

    /// <summary>
    ///     Internal method to return OctetString byte array. Used for copy operations, comparisons and similar within the
    ///     library. Not available for users of the library
    /// </summary>
    /// <returns></returns>
    internal byte[] GetData()
    {
        return _data;
    }

    /// <summary>
    ///     Empty data buffer
    /// </summary>
    public void Clear()
    {
        _data = [];
    }

    /// <summary>
    ///     Convert the OctetString class to a byte array. Internal class data buffer is *copied* and not passed to the caller.
    /// </summary>
    /// <returns>Byte array representing the OctetString class data</returns>
    public byte[] ToArray()
    {
        var tmp = new byte[_data.Length];
        Buffer.BlockCopy(_data, 0, tmp, 0, _data.Length);
        return tmp;
    }

    /// <summary>
    ///     Set object value to bytes from the supplied string. If argument string length == 0, internal OctetString
    ///     buffer is set to null.
    /// </summary>
    /// <param name="value">String containing new class data</param>
    public virtual void Set(string value)
    {
        switch (value)
        {
            case null:
                _data = [];
                return;
        }

        _data = value.Length switch
        {
            0 => [],
            _ => Encoding.UTF8.GetBytes(value)
        };
    }

    /// <summary>
    ///     Set class value from the argument byte array. If byte array argument is null or length == 0,
    ///     internal <see cref="OctetString" /> buffer is set to null.
    /// </summary>
    /// <param name="data">Byte array to copy data from.</param>
    public virtual void Set(byte[] data)
    {
        if (data is not { Length: > 0 })
        {
            _data = [];
        }
        else
        {
            _data = new byte[data.Length];
            Buffer.BlockCopy(data, 0, _data, 0, data.Length);
        }
    }

    /// <summary>
    ///     Set class value to an array 1 byte long and set the value to the supplied argument.
    /// </summary>
    /// <param name="data">Byte value to initialize the class value with</param>
    public virtual void Set(byte data)
    {
        _data = new byte[1];
        _data[0] = data;
    }

    /// <summary>
    ///     Set value at specified position to the supplied value
    /// </summary>
    /// <param name="position">Zero based offset from the beginning of the buffer</param>
    /// <param name="value">Value to set</param>
    public virtual void Set(int position, byte value)
    {
        if (position < 0 || position >= Length) return; // Don't throw exceptions here
        _data[position] = value;
    }

    /// <summary>
    ///     Set class value to reference of parameter byte array
    /// </summary>
    /// <param name="data">Data buffer parameter</param>
    public virtual void SetRef(byte[] data)
    {
        _data = data;
    }

    /// <summary>
    ///     Append string value to the OctetString class. If current class content is length 0, new
    ///     string value is set as the value of this class.
    ///     Class assumes that string value is UTF8 encoded.
    /// </summary>
    /// <param name="value">UTF8 encoded string value</param>
    public void Append(string value)
    {
        switch (_data)
        {
            case null:
                Set(value);
                break;
            default:
            {
                switch (value.Length)
                {
                    case > 0:
                    {
                        var buffer = Encoding.UTF8.GetBytes(value);
                        if (buffer.Length > 0) Append(buffer);
                        break;
                    }
                }

                break;
            }
        }
    }

    /// <summary>
    ///     Append contents of the byte array to the class value. If class value is length 0, byte array
    ///     content is set as the class value.
    /// </summary>
    /// <param name="value">Byte array</param>
    public void Append(byte[] value)
    {
        if (value == null || value.Length == 0)
            throw new ArgumentNullException(nameof(value));
        switch (_data)
        {
            case null:
                Set(value);
                break;
            default:
            {
                var tempBuffer = new byte[_data.Length + value.Length];
                Buffer.BlockCopy(_data, 0, tempBuffer, 0, _data.Length);
                Buffer.BlockCopy(value, 0, tempBuffer, _data.Length, value.Length);
                _data = tempBuffer;
                break;
            }
        }
    }

    /// <summary>Utility function to print a MAC address (binary string of 6 byte length.</summary>
    /// <returns>
    ///     If data is of the correct length (6 bytes), string representing hex mac address in the
    ///     format xxxx.xxxx.xxxx. If data is not of the correct length, empty string is returned.
    /// </returns>
    public string ToMACAddressString()
    {
        switch (Length)
        {
            case 6:
                return string.Format(CultureInfo.CurrentCulture, "{0:x2}{1:x2}.{2:x2}{3:x2}.{4:x2}{5:x2}",
                    _data[0], _data[1], _data[2], _data[3], _data[4], _data[5]);
            default:
                return "";
        }
    }

    /// <summary>
    ///     Return string representation of the OctetStrig object. If non-printable characters have been
    ///     found in the object, output is a hex representation of the string.
    /// </summary>
    /// <returns>String representation of the object.</returns>
    public override string ToString()
    {
        if (_data is not { Length: > 0 }) return "";
        var asHex = IsHex;

        var rs = asHex switch
        {
            true => ToHexString(),
            _ => new string(Encoding.UTF8.GetChars(_data))
        };

        return rs;
    }

    /// <summary>
    ///     Return string formatted hexadecimal representation of the objects value.
    /// </summary>
    /// <returns>String representation of hexadecimal formatted class value.</returns>
    public string ToHexString()
    {
        var b = new StringBuilder();
        for (var i = 0; i < _data.Length; ++i)
        {
            var x = _data[i] & 0xff;
            switch (x)
            {
                case < 16:
                    b.Append('0');
                    break;
            }

            b.Append(Convert.ToString(x, 16).ToUpper());

            if (i < _data.Length - 1)
                b.Append(' ');
        }

        return b.ToString();
    }

    /// <summary>
    ///     Compare against another object. Acceptable object types are <see cref="OctetString" /> and
    ///     <see cref="System.String" />.
    /// </summary>
    /// <param name="obj">Object of type <see cref="OctetString" /> or <see cref="System.String" /> to compare against</param>
    /// <returns>true if object content is the same, false if different or if incompatible object type</returns>
    public override bool Equals(object? obj)
    {
        byte[] d;
        switch (obj)
        {
            case OctetString s:
                d = s.GetData();
                break;
            case string s1:
                d = Encoding.UTF8.GetBytes(s1);
                break;
            default:
                return false; // Incompatible object type
        }

        // check for null value in comparison

        if (d.Length != _data.Length) return false; // Objects have different length
        for (var cnt = 0; cnt < d.Length; cnt++)
            if (d[cnt] != _data[cnt])
                return false;

        return true;
    }

    /// <summary>
    ///     Dummy override to prevent compiler warning messages.
    /// </summary>
    /// <returns>Nothing of interest</returns>
    public override int GetHashCode()
    {
        return GetData().GetHashCode();
    }

    /// <summary>
    ///     Overloading equality operator
    /// </summary>
    /// <param name="str1">Source (this) string</param>
    /// <param name="str2">String to compare with</param>
    /// <returns>True if equal, otherwise false</returns>
    public static bool operator ==(OctetString? str1, OctetString? str2)
    {
        if (ReferenceEquals(str1, str2))
            return true;
        if (str1 is null && str2 is null)
            return true;
        if (str1 is null || str2 is null)
            return false;
        return str1.Equals(str2);
    }

    /// <summary>
    ///     Negative equality operator
    /// </summary>
    /// <param name="str1">Source (this) string</param>
    /// <param name="str2">String to compare with</param>
    /// <returns>True if not equal, otherwise false</returns>
    public static bool operator !=(OctetString str1, OctetString str2)
    {
        return !(str1 == str2);
    }

    /// <summary>
    ///     Implicit operator allowing cast of OctetString objects to byte[] array
    /// </summary>
    /// <param name="oStr">OctetString to cast as byte array</param>
    /// <returns>Byte array value of the supplied OctetString</returns>
    public static implicit operator byte[](OctetString? oStr)
    {
        return oStr == null ? [] : oStr.ToArray();
    }

    /// <summary>
    ///     Reset internal buffer to null.
    /// </summary>
    public void Reset()
    {
        _data = [];
    }

    #region Encode and decode methods

    /// <summary>BER encode OctetString variable.</summary>
    /// <param name="buffer"><see cref="MutableByte" /> encoding destination.</param>
    public override void encode(MutableByte buffer)
    {
        if (_data.Length == 0)
        {
            BuildHeader(buffer, Type, 0);
        }
        else
        {
            BuildHeader(buffer, Type, _data.Length);
            buffer.Append(_data);
        }
    }


    /// <summary>BER encode OctetString variable.</summary>
    /// <param name="buffer"><see cref="MutableByte" /> encoding destination.</param>
    public override int encode(Span<byte> buffer)
    {
        if (_data.Length == 0)
        {
            return BuildHeader(buffer, Type, 0);
        }

        var slice = BuildHeader(buffer, Type, _data.Length);
        _data.CopyTo(buffer[slice..]);
        return slice + _data.Length;
    }

    /// <summary>
    ///     Decode OctetString from the BER format.
    /// </summary>
    /// <param name="buffer">BER encoded buffer</param>
    /// <param name="offset">Offset in the <see cref="MutableByte" /> to start the decoding from</param>
    /// <returns>Buffer position after the decoded value</returns>
    /// <exception cref="SnmpException">Thrown if parsed data type is invalid.</exception>
    public override int decode(byte[] buffer, int offset)
    {
        return decode(buffer.AsSpan(), offset);
    }

    /// <summary>
    ///     Decode OctetString from the BER format.
    /// </summary>
    /// <param name="buffer">BER encoded buffer</param>
    /// <param name="offset">Offset in the <see cref="MutableByte" /> to start the decoding from</param>
    /// <returns>Buffer position after the decoded value</returns>
    /// <exception cref="SnmpException">Thrown if parsed data type is invalid.</exception>
    public override int decode(Span<byte> buffer, int offset)
    {
        var asnType = ParseHeader(buffer, ref offset, out var headerLength);

        if (asnType != Type)
            throw new SnmpException("Invalid ASN.1 type.");

        // verify that there is enough data to decode
        if (buffer.Length - offset < headerLength)
            throw new OverflowException("Data buffer is too small");

        switch (headerLength)
        {
            case 0:
                // Packet contains string length == 0
                _data = [];
                break;
            default:
                //
                // copy the data
                //
                Array.Resize(ref _data, headerLength);
                buffer.Slice(offset, headerLength).CopyTo(_data);
                offset += headerLength;
                break;
        }

        return offset;
    }

    public override int ByteLength
    {
        get
        {
            var header = HeaderSize(_data.Length);
            return header + _data.Length;
        }
    }

    #endregion Encode and decode methods
}