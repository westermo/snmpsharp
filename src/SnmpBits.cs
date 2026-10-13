using System;

namespace SnmpSharpNet;

/// <summary>
///     Converts between the SMIv2 <c>BITS</c> wire encoding and a 64-bit mask.
/// </summary>
/// <remarks>
///     Per RFC 2578 §7.1.4 a <c>BITS</c> value is carried as an <see cref="OctetString"/> where named bit 0 is the
///     most significant bit of the first octet. In the returned mask, named bit <c>n</c> is <c>1UL &lt;&lt; n</c>.
/// </remarks>
public static class SnmpBits
{
    /// <summary>Largest named bit position representable in a 64-bit mask.</summary>
    public const int MaxBit = 63;

    /// <summary>Decodes a <c>BITS</c> octet string into a mask. Bits beyond <see cref="MaxBit"/> are ignored.</summary>
    public static ulong ToMask(OctetString value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return ToMask(value.ToArray());
    }

    /// <summary>Decodes <c>BITS</c> octets into a mask. Bits beyond <see cref="MaxBit"/> are ignored.</summary>
    public static ulong ToMask(ReadOnlySpan<byte> octets)
    {
        ulong mask = 0;
        var length = Math.Min(octets.Length, (MaxBit + 1) / 8);
        for (var octet = 0; octet < length; octet++)
        {
            for (var bit = 0; bit < 8; bit++)
            {
                if ((octets[octet] & (0x80 >> bit)) != 0)
                {
                    mask |= 1UL << (octet * 8 + bit);
                }
            }
        }

        return mask;
    }

    /// <summary>Encodes a mask as a <c>BITS</c> octet string.</summary>
    /// <param name="mask">Mask where named bit <c>n</c> is <c>1UL &lt;&lt; n</c>.</param>
    /// <param name="minimumOctets">
    ///     Minimum number of octets to emit, typically enough to hold the highest named bit. More octets are emitted
    ///     when <paramref name="mask"/> has higher bits set.
    /// </param>
    public static OctetString FromMask(ulong mask, int minimumOctets = 0)
    {
        if (minimumOctets is < 0 or > (MaxBit + 1) / 8)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumOctets));
        }

        var highestBit = mask == 0 ? -1 : 63 - System.Numerics.BitOperations.LeadingZeroCount(mask);
        var octets = new byte[Math.Max(minimumOctets, highestBit < 0 ? 0 : highestBit / 8 + 1)];
        for (var bit = 0; bit <= highestBit; bit++)
        {
            if ((mask & (1UL << bit)) != 0)
            {
                octets[bit / 8] |= (byte)(0x80 >> (bit % 8));
            }
        }

        return new OctetString(octets);
    }
}
