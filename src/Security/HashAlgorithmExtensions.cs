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
                   span[..expectedHash.Length].SequenceEqual(expectedHash);
        }

        public void HashMegabyte(ReadOnlySpan<byte> toCompute)
        {
            const int bufferSize = 8192;
            var buf = ArrayPool<byte>.Shared.Rent(bufferSize);
            /* Use while loop until we've done 1 Megabyte */
            var num = 0;
            var count = 0;
            while (count < 1048576)
            {
                for (int index = 0; index < bufferSize /*0x40*/; ++index)
                    buf[index] = toCompute[num++ % toCompute.Length];

                hashAlgorithm.TransformBlock(buf, 0, bufferSize, null, 0);
                count += bufferSize;
            }

            hashAlgorithm.TransformFinalBlock(buf, 0, 0);
            Array.Clear(buf, 0, bufferSize);
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