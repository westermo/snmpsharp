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
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace SnmpSharpNet;

/// <summary>
///     Process-wide memoization of RFC 3414 password-to-key localization results.
/// </summary>
/// <remarks>
///     Password localization hashes 1 MB of data and was previously recomputed for every encoded and decoded
///     SNMPv3 packet. Entries are keyed by a SHA-256 fingerprint of (password, engine id) so plaintext secrets
///     are never stored as dictionary keys. Callers always receive a private copy of the cached key.
/// </remarks>
internal static class LocalizedKeyCache
{
    private const int MaxEntries = 256;
    private const int StackLimit = 512;

    private static readonly ConcurrentDictionary<string, byte[]> Cache = new(StringComparer.Ordinal);

    internal delegate byte[] KeyDeriver(ReadOnlySpan<byte> userPassword, ReadOnlySpan<byte> engineId);

    internal static byte[] GetOrDerive(string algorithm, ReadOnlySpan<byte> userPassword,
        ReadOnlySpan<byte> engineId, KeyDeriver derive)
    {
        var cacheKey = CreateCacheKey(algorithm, userPassword, engineId);
        if (Cache.TryGetValue(cacheKey, out var cached))
            return (byte[])cached.Clone();

        var derived = derive(userPassword, engineId);
        if (Cache.Count >= MaxEntries)
            Cache.Clear();
        Cache.TryAdd(cacheKey, (byte[])derived.Clone());
        return derived;
    }

    internal static void Clear() => Cache.Clear();

    private static string CreateCacheKey(string algorithm, ReadOnlySpan<byte> userPassword,
        ReadOnlySpan<byte> engineId)
    {
        var length = sizeof(int) + userPassword.Length + engineId.Length;
        byte[]? rented = null;
        var buffer = length <= StackLimit
            ? stackalloc byte[StackLimit]
            : rented = ArrayPool<byte>.Shared.Rent(length);
        buffer = buffer[..length];
        try
        {
            BinaryPrimitives.WriteInt32LittleEndian(buffer, userPassword.Length);
            userPassword.CopyTo(buffer[sizeof(int)..]);
            engineId.CopyTo(buffer[(sizeof(int) + userPassword.Length)..]);
            Span<byte> fingerprint = stackalloc byte[SHA256.HashSizeInBytes];
            SHA256.HashData(buffer, fingerprint);
            return string.Concat(algorithm, ":", Convert.ToHexString(fingerprint));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
