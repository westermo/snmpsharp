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
using System.Security.Cryptography;

namespace SnmpSharpNet;

/// <summary>
///     MD5 Authentication class.
/// </summary>
public class AuthenticationMD5 : IAuthenticationDigest
{
    private const int authenticationLength = 12;
    private const int digestLength = 16;

    /// <summary>
    ///     Standard constructor
    /// </summary>
    public AuthenticationMD5()
    {
    }

    /// <summary>
    ///     Authenticate packet and return authentication parameters value to the caller
    /// </summary>
    /// <param name="authenticationSecret">User authentication secret</param>
    /// <param name="engineId">SNMP agent authoritative engine id</param>
    /// <param name="wholeMessage">Message to authenticate</param>
    /// <returns>Authentication parameters value</returns>
    public byte[] authenticate(Span<byte> authenticationSecret, Span<byte> engineId, Span<byte> wholeMessage)
    {
        var result = new byte[authenticationLength];
        var authKey = PasswordToKey(authenticationSecret, engineId);
        using var sha = new HMACMD5(authKey);
        sha.WithHashed(wholeMessage, (span, _) => { span[..authenticationLength].CopyTo(result); });
        return result;
    }

    /// <summary>
    ///     Authenticate packet and return authentication parameters value to the caller
    /// </summary>
    /// <param name="authKey">Pre-generated authentication key</param>
    /// <param name="wholeMessage">Message being authenticated</param>
    /// <returns>Authentication parameters value</returns>
    public byte[] authenticate(Span<byte> authKey, Span<byte> wholeMessage)
    {
        var result = new byte[authenticationLength];

        using var sha = new HMACMD5(authKey.ToArray());
        sha.WithHashed(wholeMessage, (span, _) => { span[..authenticationLength].CopyTo(result); });
        return result;
    }

    /// <summary>
    ///     Verifies correct MD5 authentication of the frame. Prior to calling this method, you have to extract authentication
    ///     parameters from the wholeMessage and reset authenticationParameters field in the USM information block to 12 0x00
    ///     values.
    /// </summary>
    /// <param name="userPassword">User password</param>
    /// <param name="engineId">Authoritative engine id</param>
    /// <param name="authenticationParameters">Extracted USM authentication parameters</param>
    /// <param name="wholeMessage">Whole message with authentication parameters zeroed (0x00) out</param>
    /// <returns>True if message authentication has passed the check, otherwise false</returns>
    public bool authenticateIncomingMsg(Span<byte> userPassword, Span<byte> engineId,
        Span<byte> authenticationParameters,
        Span<byte> wholeMessage)
    {
        var authKey = PasswordToKey(userPassword, engineId);
        return authenticateIncomingMsg(authKey, authenticationParameters, wholeMessage);
    }

    /// <summary>
    ///     Verify MD5 authentication of a packet.
    /// </summary>
    /// <param name="authKey">Authentication key (not password)</param>
    /// <param name="authenticationParameters">Authentication parameters extracted from the packet being authenticated</param>
    /// <param name="wholeMessage">Entire packet being authenticated</param>
    /// <returns>True on authentication success, otherwise false</returns>
    public bool authenticateIncomingMsg(Span<byte> authKey, Span<byte> authenticationParameters,
        Span<byte> wholeMessage)
    {
        using var md5 = new HMACMD5(authKey.ToArray());
        return md5.CompareHashed(wholeMessage, authenticationParameters);
    }

    /// <summary>
    ///     Convert user password to acceptable authentication key.
    /// </summary>
    /// <param name="userPassword">Authentication password</param>
    /// <param name="engineID">Authoritative engine id</param>
    /// <returns>Localized authentication key</returns>
    /// <exception cref="SnmpAuthenticationException">Thrown when key length is less then 8 bytes</exception>
    public byte[] PasswordToKey(Span<byte> userPassword, Span<byte> engineID)
    {
        const int bufferSize = 8192;
        // key length has to be at least 8 bytes long (RFC3414)
        if (userPassword.Length < 8)
            throw new SnmpAuthenticationException("Secret key is too short.");
        using var sha = MD5.Create();

        var count = 0;

        var buf = ArrayPool<byte>.Shared.Rent(bufferSize);
        /* Use while loop until we've done 1 Megabyte */
        var remnant = 0;
        Span<byte> offsetUserPassword = stackalloc byte[userPassword.Length];
        userPassword.CopyTo(offsetUserPassword);
        Span<byte> tmp = stackalloc byte[userPassword.Length];
        while (count < 1048576)
        {
            if (remnant > 0)
            {
                offsetUserPassword[remnant..].CopyTo(tmp);
                offsetUserPassword[..remnant].CopyTo(tmp[(userPassword.Length - remnant)..]);
                tmp.CopyTo(offsetUserPassword);
            }

            for (int i = 0; i < bufferSize; i += userPassword.Length)
            {
                var remaining = Math.Min(offsetUserPassword.Length, bufferSize - i);
                // Take the next octet of the password, wrapping
                // to the beginning of the password as necessary.
                offsetUserPassword[..remaining].CopyTo(buf.AsSpan(i));
                remnant = offsetUserPassword.Length - remaining;
            }

            sha.TransformBlock(buf, 0, bufferSize, null, 0);
            count += bufferSize;
        }

        sha.TransformFinalBlock(buf, 0, 0);
        Array.Clear(buf, 0, bufferSize);
        ArrayPool<byte>.Shared.Return(buf);

        var digest = sha.Hash.AsSpan();
        Span<byte> source = stackalloc byte[digest.Length + engineID.Length + digest.Length];
        digest.CopyTo(source);
        engineID.CopyTo(source[digest.Length..]);
        digest.CopyTo(source[(digest.Length + engineID.Length)..]);
        var result = MD5.HashData(source);
        source.Clear();
        offsetUserPassword.Clear();
        tmp.Clear();
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
    ///     Return protocol name.
    /// </summary>
    public string Name => "HMAC-MD5";

    /// <summary>
    ///     Compute hash using authentication protocol.
    /// </summary>
    /// <param name="data">Data to hash</param>
    /// <param name="offset">Compute hash from the source buffer offset</param>
    /// <param name="count">Compute hash for source data length</param>
    /// <returns>Hash value</returns>
    public byte[] ComputeHash(Span<byte> data, int offset, int count)
    {
        var res = MD5.HashData(data.Slice(offset, count));
        return res;
    }
}