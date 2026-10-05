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

using System.Buffers;

namespace SnmpSharpNet;

using System;

/// <summary>
///     SHA-256 Authentication class.
/// </summary>
public class AuthenticationSHA224 : IAuthenticationDigest
{
    private const int authenticationLength = 21;
    private const int digestLength = 28;

    public static AuthenticationSHA224 Instance { get; } = new();

    /// <summary>
    ///     Standard constructor.
    /// </summary>
    private AuthenticationSHA224()
    {
    }

    /// <summary>
    ///     Authenticate packet and return authentication parameters value to the caller
    /// </summary>
    /// <param name="authenticationSecret">User authentication secret</param>
    /// <param name="engineId">SNMP agent authoritative engine id</param>
    /// <param name="wholeMessage">Message to authenticate</param>
    /// <returns>Authentication parameters value</returns>
    public byte[] Authenticate(ReadOnlySpan<byte> authenticationSecret, ReadOnlySpan<byte> engineId,
        ReadOnlySpan<byte> wholeMessage)
    {
        var authKey = PasswordToKey(authenticationSecret, engineId);
        return Authenticate(authKey, wholeMessage);
    }

    /// <summary>
    ///     Authenticate packet and return authentication parameters value to the caller
    /// </summary>
    /// <param name="authKey">Authentication key (not password)</param>
    /// <param name="wholeMessage">Message to authenticate</param>
    /// <returns>Authentication parameters value</returns>
    public byte[] Authenticate(ReadOnlySpan<byte> authKey, ReadOnlySpan<byte> wholeMessage)
    {
        var result = new byte[authenticationLength];
        using var sha = new HMACSHA224(authKey.ToArray());
        sha.WithHashed(wholeMessage, (span, _) => { span[..authenticationLength].CopyTo(result); });
        return result;
    }

    /// <summary>
    ///     Verifies correct SHA-224 authentication of the frame. Prior to calling this method, you have to extract
    ///     authentication
    ///     parameters from the wholeMessage and reset authenticationParameters field in the USM information block to 12 0x00
    ///     values.
    /// </summary>
    /// <param name="userPassword">User password</param>
    /// <param name="engineId">Authoritative engine id</param>
    /// <param name="authenticationParameters">Extracted USM authentication parameters</param>
    /// <param name="wholeMessage">Whole message with authentication parameters zeroed (0x00) out</param>
    /// <returns>True if message authentication has passed the check, otherwise false</returns>
    public bool AuthenticateIncomingMsg(ReadOnlySpan<byte> userPassword, ReadOnlySpan<byte> engineId,
        ReadOnlySpan<byte> authenticationParameters, ReadOnlySpan<byte> wholeMessage)
    {
        var authKey = PasswordToKey(userPassword, engineId);
        return AuthenticateIncomingMsg(authKey, authenticationParameters, wholeMessage);
    }

    /// <summary>
    ///     Verify SHA-224 authentication of a packet.
    /// </summary>
    /// <param name="authKey">Authentication key (not password)</param>
    /// <param name="authenticationParameters">Authentication parameters extracted from the packet being authenticated</param>
    /// <param name="wholeMessage">Entire packet being authenticated</param>
    /// <returns>True on authentication success, otherwise false</returns>
    public bool AuthenticateIncomingMsg(ReadOnlySpan<byte> authKey, ReadOnlySpan<byte> authenticationParameters,
        ReadOnlySpan<byte> wholeMessage)
    {
        using var sha = new HMACSHA224(authKey.ToArray());
        return sha.CompareHashed(wholeMessage, authenticationParameters);
    }

    /// <summary>
    ///     Convert user password to acceptable authentication key.
    /// </summary>
    /// <param name="userPassword">User password</param>
    /// <param name="engineID">Authoritative engine id</param>
    /// <returns>Localized authentication key</returns>
    /// <exception cref="SnmpAuthenticationException">Thrown when key length is less then 8 bytes</exception>
    public byte[] PasswordToKey(ReadOnlySpan<byte> userPassword, ReadOnlySpan<byte> engineID)
    {
        // key length has to be at least 8 bytes long (RFC3414)
        if (userPassword.Length < 8)
            throw new SnmpAuthenticationException("Secret key is too short.");
        return LocalizedKeyCache.GetOrDerive(nameof(AuthenticationSHA224), userPassword, engineID, DeriveKey);
    }

    private static byte[] DeriveKey(ReadOnlySpan<byte> userPassword, ReadOnlySpan<byte> engineID)
    {
        using var sha = SHA224.Create();
        sha.HashMegabyte(userPassword);
        var digest = sha.Hash.AsSpan();
        Span<byte> source = stackalloc byte[digest.Length + engineID.Length + digest.Length];
        digest.CopyTo(source);
        engineID.CopyTo(source[digest.Length..]);
        digest.CopyTo(source[(digest.Length + engineID.Length)..]);
        var result = SHA224.HashData(source);
        source.Clear();
        return result;
    }

    /// <summary>
    ///     Length of the digest generated by the authentication protocol
    /// </summary>
    public int DigestLength => digestLength;

    /// <summary>
    ///     Length of the authentification header generated by the authentication protocol
    /// </summary>
    public int AuthentificationHeaderLength => authenticationLength;

    /// <summary>
    ///     Return authentication protocol name
    /// </summary>
    public string Name => "HMAC-SHA256";

    /// <summary>
    ///     Compute hash using authentication protocol.
    /// </summary>
    /// <param name="data">Data to hash</param>
    /// <param name="offset">Compute hash from the source buffer offset</param>
    /// <param name="count">Compute hash for source data length</param>
    /// <returns>Hash value</returns>
    public byte[] ComputeHash(ReadOnlySpan<byte> data, int offset, int count) =>
        SHA224.HashData(data.Slice(offset, count));
}