using System;
using System.Buffers;
using System.Security.Cryptography;

namespace SnmpSharpNet;

public static class HashAlgorithmExtensions
{
    extension(HashAlgorithm hashAlgorithm)
    {
        public T? WithHashed<T>(ReadOnlySpan<byte> toCompute,
            Func<Span<byte>, int, T> postHashFunction)
        {
            Span<byte> span = stackalloc byte[hashAlgorithm.HashSize / 8];
            return hashAlgorithm.TryComputeHash(toCompute, span, out var written)
                ? postHashFunction(span, written)
                : default;
        }

        public void WithHashed(ReadOnlySpan<byte> toCompute,
            Action<Span<byte>, int> postHashFunction)
        {
            Span<byte> span = stackalloc byte[hashAlgorithm.HashSize / 8];
            if (hashAlgorithm.TryComputeHash(toCompute, span, out var written))
            {
                postHashFunction(span, written);
            }
        }

        public bool CompareHashed(ReadOnlySpan<byte> toCompute,
            ReadOnlySpan<byte> expectedHash)
        {
            Span<byte> span = stackalloc byte[hashAlgorithm.HashSize / 8];
            return hashAlgorithm.TryComputeHash(toCompute, span, out _)
                   &&
                   CryptographicOperations.FixedTimeEquals(span[..expectedHash.Length], expectedHash);
        }

        public void HashMegabyte(ReadOnlySpan<byte> toCompute)
        {
            const int bufferSize = 8192;
            const int totalLength = 1048576;
            var patternLength = toCompute.Length;
            // The buffer holds the password repeated end-to-end; since its content is periodic with period
            // patternLength, any window [offset, offset + bufferSize) equals the next bufferSize bytes of the
            // infinite password stream that starts at position offset. This avoids a per-byte modulo loop.
            var filledLength = bufferSize + patternLength;
            var buf = ArrayPool<byte>.Shared.Rent(filledLength);
            var span = buf.AsSpan(0, filledLength);
            for (var written = 0; written < filledLength; written += patternLength)
            {
                var n = Math.Min(patternLength, filledLength - written);
                toCompute[..n].CopyTo(span[written..]);
            }

            var offset = 0;
            for (var count = 0; count < totalLength; count += bufferSize)
            {
                hashAlgorithm.TransformBlock(buf, offset, bufferSize, null, 0);
                offset = (offset + bufferSize) % patternLength;
            }

            hashAlgorithm.TransformFinalBlock(buf, 0, 0);
            CryptographicOperations.ZeroMemory(span);
            ArrayPool<byte>.Shared.Return(buf);
        }
    }

    public static byte[] ExtendShortKey(this IAuthenticationDigest authProtocol, ReadOnlySpan<byte> shortKey,
        ReadOnlySpan<byte> engineId, int minimumKeyLength)
    {
        var extKey = new byte[minimumKeyLength];
        Span<byte> workingKey = stackalloc byte[shortKey.Length];

        shortKey.CopyTo(workingKey);
        var copied = Math.Min(shortKey.Length, minimumKeyLength);
        shortKey[..copied].CopyTo(extKey);
        while (copied < minimumKeyLength)
        {
            var key = authProtocol.PasswordToKey(workingKey, engineId);
            var remaining = minimumKeyLength - copied;
            var toCopy = Math.Min(key.Length, remaining);
            Array.Copy(key, 0, extKey, copied, toCopy);
            copied += toCopy;

            workingKey = key;
        }

        return extKey;
    }
}