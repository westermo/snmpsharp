using System;
using System.Buffers;
using System.Security.Cryptography;

namespace SnmpSharpNet;

public static class HashAlgorithmExtensions
{
    public static T? WithHashed<T>(this HashAlgorithm hashAlgorithm, Span<byte> toCompute,
        Func<Span<byte>, int, T> postHashFunction)
    {
        Span<byte> span = stackalloc byte[hashAlgorithm.HashSize / 8];
        return hashAlgorithm.TryComputeHash(toCompute, span, out var written)
            ? postHashFunction(span, written)
            : default;
    }

    public static void WithHashed(this HashAlgorithm hashAlgorithm, Span<byte> toCompute,
        Action<Span<byte>, int> postHashFunction)
    {
        Span<byte> span = stackalloc byte[hashAlgorithm.HashSize / 8];
        if (hashAlgorithm.TryComputeHash(toCompute, span, out var written))
        {
            postHashFunction(span, written);
        }
    }

    public static bool CompareHashed(this HashAlgorithm hashAlgorithm, Span<byte> toCompute,
        ReadOnlySpan<byte> expectedHash)
    {
        Span<byte> span = stackalloc byte[hashAlgorithm.HashSize / 8];
        return hashAlgorithm.TryComputeHash(toCompute, span, out _)
               &&
               span[..expectedHash.Length].SequenceEqual(expectedHash);
    }
}