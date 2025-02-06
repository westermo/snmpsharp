using System;
using System.Collections.Generic;

namespace SnmpSharpNet;

static class ShaUtilities
{
    #region Functions to convert between byte arrays and uint arrays, and Word64 arrays.

    // Returns an array of 4 bytes.
    public static void UintToByteArray(uint x, Span<byte> span)
    {
        BitConverter.TryWriteBytes(span, x);
        span.Reverse();
    }

    public static void UintArrayToByteArray(Span<uint> words, Span<byte> target)
    {
        var targetIndex = 0;

        for (var index = 0; index < words.Length; index++)
        {
            UintToByteArray(words[index], target[targetIndex..(targetIndex + 4)]);
            targetIndex += 4;
        }
    }


    public static uint ByteArrayToUint(ReadOnlySpan<byte> B)
    {
        return (uint)(B[0] << 24 | B[1] << 16 | B[2] << 8 | B[3]);
    }

    public static void ByteArrayToUintArray(ReadOnlySpan<byte> B, Span<uint> target)
    {
        for (var i = 0; i < target.Length; i++)
        {
            target[i] = ByteArrayToUint(B[(4 * i)..]);
        }
    }

    #endregion
}