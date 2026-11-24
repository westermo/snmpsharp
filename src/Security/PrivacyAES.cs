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
using System.Security.Cryptography;

namespace SnmpSharpNet;

/// <summary>AES privacy protocol implementation class.</summary>
public class PrivacyAES : IPrivacyProtocol
{
    /// <summary>
    ///     AES protocol key bytes. Valid values are 16 (for AES128), 24 (AES192) or 32 (AES256).
    /// </summary>
    protected readonly int _keyBytes = 16; // Default is 128bit AES protocol

    /// <summary>
    ///     Salt value
    /// </summary>
    protected long _salt;

    /// <summary>
    ///     Standard constructor.
    /// </summary>
    /// <param name="keyBytes">
    ///     Key size in bytes. Acceptable values are
    ///     16 = 128-bit key size, 24 = 192-bit key size, or 32 = 256-bit key size.
    /// </param>
    protected PrivacyAES(int keyBytes)
    {
        if (keyBytes != 16 && keyBytes != 24 && keyBytes != 32)
            throw new ArgumentOutOfRangeException(nameof(keyBytes), "Valid key sizes are 16, 24 and 32 bytes.");
        _keyBytes = keyBytes;
        var salt = RandomNumberGenerator.GetBytes(sizeof(long));
        _salt = BinaryPrimitives.ReadInt64BigEndian(salt) & long.MaxValue;
    }

    /// <summary>
    ///     Encrypt <see cref="ScopedPdu" /> data BER encoded in a byte array.
    /// </summary>
    /// <param name="unencryptedData">BER encoded <see cref="ScopedPdu" /> byte array that needs to be encrypted</param>
    /// <param name="offset">Offset within the BER encoded byte array to start encryption operation from.</param>
    /// <param name="length">Length of data to encrypt</param>
    /// <param name="key">Encryption key</param>
    /// <param name="engineBoots">Authoritative engine boots value. Retrieved as part of SNMP v3 discovery process.</param>
    /// <param name="engineTime">Authoritative engine time value. Retrieved as part of SNMP v3 discovery process.</param>
    /// <param name="privacyParameters">
    ///     Byte array that will receive privacy parameters information that is the result of the
    ///     encryption procedure.
    /// </param>
    /// <param name="authDigest">Authentication digest reference. Not used by AES protocol and can be null</param>
    /// <returns>Byte array containing encrypted <see cref="ScopedPdu" /> BER encoded data</returns>
    public byte[] Encrypt(ReadOnlySpan<byte> unencryptedData, int offset, int length, ReadOnlySpan<byte> key,
        int engineBoots,
        int engineTime, out byte[] privacyParameters, IAuthenticationDigest authDigest)
    {
        // check the key before doing anything else
        if (key.IsEmpty || key.Length < _keyBytes)
            throw new ArgumentOutOfRangeException(nameof(key), "Invalid key length");

        var iv = new byte[16];
        var salt = NextSalt();
        privacyParameters = new byte[PrivacyParametersLength];
        BinaryPrimitives.WriteInt32BigEndian(iv, engineBoots);
        BinaryPrimitives.WriteInt32BigEndian(iv.AsSpan(4), engineTime);
        BinaryPrimitives.WriteInt64BigEndian(privacyParameters, salt);
        privacyParameters.CopyTo(iv, 8);

        var paddedLength = checked((length + 15) / 16 * 16);
        var paddedData = new byte[paddedLength];
        unencryptedData.Slice(offset, length).CopyTo(paddedData);
        using var aes = Aes.Create();
        aes.Key = key[..MinimumKeyLength].ToArray();
        aes.FeedbackSize = 128;
        var encrypted = aes.EncryptCfb(paddedData, iv, PaddingMode.None, 128);
        return encrypted.AsSpan(0, length).ToArray();
    }

    /// <summary>
    ///     Decrypt <see cref="ScopedPdu" /> BER encoded byte array.
    /// </summary>
    /// <param name="cryptedData">Encrypted data byte array</param>
    /// <param name="offset">Offset within the buffer to start decryption process from</param>
    /// <param name="length">Length of data to decrypt</param>
    /// <param name="key">Decryption key</param>
    /// <param name="engineBoots">Authoritative engine boots value. Retrieved as part of SNMP v3 discovery procedure</param>
    /// <param name="engineTime">Authoritative engine time value. Retrieved as part of SNMP v3 discovery procedure</param>
    /// <param name="privacyParameters">Privacy parameters parsed from the incoming packet.</param>
    /// <returns>Byte array containing decrypted <see cref="ScopedPdu" /> in BER encoded format.</returns>
    public byte[] Decrypt(ReadOnlySpan<byte> cryptedData, int offset, int length, ReadOnlySpan<byte> key,
        int engineBoots,
        int engineTime,
        ReadOnlySpan<byte> privacyParameters)
    {
        if (key.Length < _keyBytes)
            throw new ArgumentOutOfRangeException(nameof(key), "Invalid key length");

        var iv = new byte[16];

        BinaryPrimitives.WriteInt32BigEndian(iv, engineBoots);
        BinaryPrimitives.WriteInt32BigEndian(iv.AsSpan(4), engineTime);

        // Copy salt value to the iv array
        privacyParameters[..8].CopyTo(iv.AsSpan(8, 8));

        var paddedLength = checked((length + 15) / 16 * 16);
        var paddedData = new byte[paddedLength];
        cryptedData.Slice(offset, length).CopyTo(paddedData);
        using var aes = Aes.Create();
        aes.Key = key[..MinimumKeyLength].ToArray();
        aes.FeedbackSize = 128;
        var decrypted = aes.DecryptCfb(paddedData, iv, PaddingMode.None, 128);
        return decrypted.AsSpan(0, length).ToArray();
    }

    /// <summary>
    ///     Get minimum encryption/decryption key length required by the protocol.
    /// </summary>
    public int MinimumKeyLength => _keyBytes;

    /// <summary>
    ///     Get maximum encryption/decryption key length required by the protocol.
    /// </summary>
    public int MaximumKeyLength => _keyBytes;

    /// <summary>
    ///     Get length of the privacy parameters byte array that is generated by the encryption method and used by the
    ///     decryption method.
    /// </summary>
    public int PrivacyParametersLength => 8;

    /// <summary>
    ///     Calculates and returns length of the buffer that is the result of the encryption method.
    /// </summary>
    /// <param name="scopedPduLength">Length of the buffer that is needs to be encrypted.</param>
    /// <returns>Length of the encrypted byte array after the call to Encrypt method.</returns>
    public int GetEncryptedLength(int scopedPduLength)
    {
        return scopedPduLength;
    }

    /// <summary>
    ///     Some protocols support a method to extend the encryption or decryption key when supplied key
    ///     is too short.
    /// </summary>
    /// <param name="shortKey">Key that needs to be extended</param>
    /// <param name="password">Privacy password as configured on the SNMP agent.</param>
    /// <param name="engineID">Authoritative engine id. Value is retrieved as part of SNMP v3 discovery procedure</param>
    /// <param name="authProtocol">Authentication protocol class instance cast as <see cref="IAuthenticationDigest" /></param>
    /// <returns>Extended key value</returns>
    public byte[] ExtendShortKey(ReadOnlySpan<byte> shortKey, ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> engineID,
        IAuthenticationDigest authProtocol) => authProtocol.ExtendShortKey(shortKey, engineID, MinimumKeyLength);

    /// <summary>
    ///     Privacy protocol name. Returns string "AES"
    /// </summary>
    public virtual string Name => "AES";

    /// <summary>
    ///     AES implementation supports extending of a short encryption key. Always returns true.
    /// </summary>
    public bool CanExtendShortKey => true;

    /// <summary>
    ///     Convert privacy password into encryption key using packet authentication hash.
    /// </summary>
    /// <param name="secret">Privacy user secret</param>
    /// <param name="engineId">Authoritative engine id of the snmp agent</param>
    /// <param name="authProtocol">Authentication protocol</param>
    /// <returns>Encryption key</returns>
    /// <exception cref="SnmpPrivacyException">Thrown when key size is shorter then MinimumKeyLength</exception>
    public byte[] PasswordToKey(ReadOnlySpan<byte> secret, ReadOnlySpan<byte> engineId,
        IAuthenticationDigest authProtocol)
    {
        if (secret.Length < 8)
            throw new SnmpPrivacyException("Invalid privacy secret length.");
        var key = authProtocol.PasswordToKey(secret, engineId);
        if (key.Length < MinimumKeyLength) key = ExtendShortKey(key, secret, engineId, authProtocol);
        return key;
    }

    /// <summary>
    ///     Get next salt Int64 value. Used internally to encrypt data.
    /// </summary>
    /// <returns>Random Int64 value</returns>
    protected long NextSalt()
    {
        switch (_salt)
        {
            case long.MaxValue:
                _salt = 1;
                break;
            default:
                _salt += 1;
                break;
        }

        return _salt;
    }
}