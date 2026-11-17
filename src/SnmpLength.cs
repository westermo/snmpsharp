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
using System.Buffers.Binary;

namespace SnmpSharpNet;

public readonly ref struct SnmpLength
{
    public SnmpLength(ReadOnlySpan<byte> encoded)
    {
        if ((encoded[0] & HIGH_BIT) == 0)
        {
            // short form encoding
            IntegerRepresentation = encoded[0];
            ByteLength = 1;
            return;
        }

        var length = encoded[0] & ~HIGH_BIT; // store byte length of the encoded length value
        if (encoded.Length < length)
            throw new OverflowException($"Buffer is to short, expected length {length} but got {encoded.Length}");
        var value = 0;
        for (var i = 0; i < length; i++)
        {
            value <<= 8;
            value |= encoded[i + 1];
        }
        ByteLength = length + 1;
        IntegerRepresentation = value;
    }
    public SnmpLength(int integerRepresentation)
    {
        switch (integerRepresentation)
        {
            case < 0:
                throw new ArgumentOutOfRangeException(nameof(integerRepresentation), "Length cannot be less then 0.");
        }
        ByteLength = HeaderSize(integerRepresentation);
        IntegerRepresentation = integerRepresentation;
    }
    public readonly int ByteLength;
    public readonly int IntegerRepresentation;

    public static implicit operator int(SnmpLength length) => length.IntegerRepresentation;
    public static int HeaderSize(int asnLength)
    {
        if (asnLength < 128) return 1;
        var res = 1;
        while (asnLength != 0)
        {
            asnLength >>= 8;
            res++;
        }

        return res;
    }
    public readonly int CopyTo(Span<byte> target)
    {
        Span<byte> len = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(len, IntegerRepresentation);
        var start = 0;
        while (start < len.Length && len[start] == 0) start++;
        var cut = len[start..];
        var length = cut.Length;
        if (length == 0)
        {
            len[^1] = 0;
            cut = len[^1..];
            length = 1;
        }

        // check for short form encoding
        if (length == 1 && (cut[0] & HIGH_BIT) == 0)
        {
            target[0] = cut[0]; // done
            return 1;
        }

        // long form encoding
        var encHeader = (byte)length;
        encHeader = (byte)(encHeader | HIGH_BIT);
        target[0] = encHeader;
        cut.CopyTo(target[1..]);
        return cut.Length + 1;
    }

    /// <summary>
    ///     Defines the "high bit" that is the sign extension bit for a 8-bit signed value.
    /// </summary>
    private const byte HIGH_BIT = 0x80;
}
