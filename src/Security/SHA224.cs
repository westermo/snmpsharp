using System;
using System.Security.Cryptography;

namespace SnmpSharpNet;

/// <summary>
/// Modified in large part from https://github.com/TerryJackson/Secure-Hash-Algorithms/blob/master/Sha.cs
/// </summary>
public sealed class SHA224 : HashAlgorithm
{
    private static readonly uint[] _initialHashValues =
    [
        0xc1059ed8, 0x367cd507, 0x3070dd17, 0xf70e5939,
        0xffc00b31, 0x68581511, 0x64f98fa7, 0xbefa4fa4
    ];

    private static readonly uint[] _k =
    [
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
        0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
        0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
        0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
        0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
        0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
        0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
    ];

    private readonly byte[] _hashValue;
    private readonly uint[] _state;

    public SHA224()
    {
        HashSizeValue = 224;
        _state = new uint[8];
        _hashValue = new byte[28];
        _buffer = new byte[BlockSize];
        Initialize();
    }

    private static void ForeachBlock(ReadOnlySpan<byte> paddedtext, Span<uint> state,
        Action<ReadOnlySpan<uint>, Span<uint>, Span<uint>> ProcessBlock)
    {
        // We are assuming M has been padded, so the number of bits in M is divisible by 512 
        var numberBlocks = paddedtext.Length * 8 / 512; // same as: paddedtext.Length / 64

        Span<uint> words = stackalloc uint[16];
        Span<uint> W = stackalloc uint[64];
        for (var i = 0; i < numberBlocks; i++)
        {
            ShaUtilities.ByteArrayToUintArray(paddedtext.Slice(i * 64, 64), words);
            ProcessBlock(words, state, W);
        }

        W.Clear();
        words.Clear();
    }

    private static void PadPlainText512(ReadOnlySpan<byte> plaintext, Span<byte> paddedText, int numberOfZerosToAdd)
    {
        var count = 0;
        foreach (var b in plaintext)
        {
            paddedText[count++] = b;
        }

        // Start the padding by concatenating 1000_0000 = 0x80 = 128
        paddedText[count++] = 0x80;

        // Next add n zero bytes
        for (var i = 0; i < numberOfZerosToAdd; i++)
        {
            paddedText[count++] = 0;
        }

        // Now add 8 bytes (64 bits) to represent the length of the message in bits
        Span<byte> B = stackalloc byte[sizeof(ulong)];
        BitConverter.TryWriteBytes(B, (ulong)plaintext.Length * 8);

        B.Reverse();
        B.CopyTo(paddedText[count..]);
        B.Clear();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Array.Clear(_hashValue, 0, _hashValue.Length);
            Array.Clear(_state, 0, _state.Length);
            Array.Clear(_buffer, 0, _buffer.Length);
        }

        base.Dispose(disposing);
    }

    private static int PadSize(ReadOnlySpan<byte> plaintext, out int numberOfZerosToAdd)
    {
        // After padding the total bits of the output will be divisible by 512.
        var numberBits = plaintext.Length * 8;
        var t = (numberBits + 8 + 64) / 512;

        // Note that 512 * (t + 1) is the least multiple of 512 greater than (numberBits + 8 + 64)
        // Therefore the number of zero bits we need to add is
        var k = 512 * (t + 1) - (numberBits + 8 + 64);

        // Since numberBits % 8 = 0, we know k % 8 = 0. So n = k / 8 is the number of zero bytes to add.
        numberOfZerosToAdd = k / 8;
        return plaintext.Length + 1 + numberOfZerosToAdd + sizeof(ulong);
    }

    // Most of these functions have a uint version and a Word64 version.
    // Sometimes they are the same (Ch, Maj,..) but sometimes different (Sigma0_256, Sigma0_512).
    // We do not need a RotL or Parity function for Word64 since they are only used in Sha-1.

    private static uint ShR(int n, uint x)
    {
        // should have 0 <= n < 32
        return x >> n;
    }

    private static uint RotR(int n, uint x)
    {
        // should have 0 <= n < 32
        return (x >> n) | (x << (32 - n));
    }

    private static uint Ch(uint x, uint y, uint z)
    {
        return (x & y) ^ (~x & z);
    }


    private static uint Maj(uint x, uint y, uint z)
    {
        return (x & y) ^ (x & z) ^ (y & z);
    }

    private static uint Sigma0_256(uint x)
    {
        return RotR(2, x) ^ RotR(13, x) ^ RotR(22, x);
    }

    private static uint Sigma1_256(uint x)
    {
        return RotR(6, x) ^ RotR(11, x) ^ RotR(25, x);
    }

    private static uint Sigma0_224(uint x)
    {
        return RotR(7, x) ^ RotR(18, x) ^ ShR(3, x);
    }

    private static uint Sigma1_224(uint x)
    {
        return RotR(17, x) ^ RotR(19, x) ^ ShR(10, x);
    }

    private static void Sha224Algorithm(ReadOnlySpan<byte> plaintext, ReadOnlySpan<uint> initialState,
        Span<uint> state, Span<byte> targetBuffer)
    {
        //Make the allocation, either on the stack or on the heap depending on size
        var totalSize = PadSize(plaintext, out var numberOfZerosToAdd);
        var paddedText = totalSize < 4096 ? stackalloc byte[totalSize] : new byte[totalSize];

        PadPlainText512(plaintext, paddedText, numberOfZerosToAdd);

        initialState.CopyTo(state);
        ForeachBlock(paddedText, state, ProcessBlock);
        paddedText.Clear();
        if (targetBuffer.Length == 0) return;
        // Concatenate all the uint Hash Values
        Span<byte> hash = stackalloc byte[state.Length * 4];
        ShaUtilities.UintArrayToByteArray(state, hash);

        // The number of bytes in the final output hash 

        hash[..NumberOfBytes].CopyTo(targetBuffer);
        hash.Clear();
    }

    private static void ProcessBlock(ReadOnlySpan<uint> block, Span<uint> state, Span<uint> W)
    {
        // The message schedule.

        // Prepare the message schedule W.
        // The first 16 words in W are the same as the words of the block.
        // The remaining 64-16 = 48 words in W are functions of the previously defined words. 
        block.CopyTo(W);
        for (var t = 16; t < 64; t++)
            W[t] = Sigma1_224(W[t - 2]) + W[t - 7] + Sigma0_224(W[t - 15]) + W[t - 16];

        // Set the working variables a,...,h to the current hash values.
        var a = state[0];
        var b = state[1];
        var c = state[2];
        var d = state[3];
        var e = state[4];
        var f = state[5];
        var g = state[6];
        var h = state[7];

        for (var t = 0; t < 64; t++)
        {
            var T1 = h + Sigma1_256(e) + Ch(e, f, g) + _k[t] + W[t];
            var T2 = Sigma0_256(a) + Maj(a, b, c);
            h = g;
            g = f;
            f = e;
            e = d + T1;
            d = c;
            c = b;
            b = a;
            a = T1 + T2;
        }

        // Update the current value of the hash H after processing block i.
        state[0] += a;
        state[1] += b;
        state[2] += c;
        state[3] += d;
        state[4] += e;
        state[5] += f;
        state[6] += g;
        state[7] += h;
    }

    private const int NumberOfBytes = 224 / 8;

    public override void Initialize()
    {
        Array.Copy(_initialHashValues, _state, _initialHashValues.Length);
    }

    protected override void HashCore(byte[] array, int initialBlockStart, int currentBlockSize)
    {
        HashCore(array, initialBlockStart, currentBlockSize);
    }

    private int _bufferLength;
    private readonly byte[] _buffer;
    private const int BlockSize = 512;

    private void HashCore(ReadOnlySpan<byte> array, int initialBlockStart, int currentBlockSize)
    {
        var offset = initialBlockStart;
        var remaining = currentBlockSize;

        if (_bufferLength > 0)
        {
            var toCopy = Math.Min(remaining, BlockSize - _bufferLength);
            array.Slice(offset, toCopy).CopyTo(_buffer.AsSpan(_bufferLength, toCopy));
            _bufferLength += toCopy;
            offset += toCopy;
            remaining -= toCopy;

            if (_bufferLength == BlockSize)
            {
                Transform(_buffer, _hashValue);
                _bufferLength = 0;
            }
        }

        while (remaining >= BlockSize)
        {
            Transform(array.Slice(offset, BlockSize), Span<byte>.Empty);
            offset += BlockSize;
            remaining -= BlockSize;
        }

        if (remaining <= 0) return;
        array.Slice(offset, remaining).CopyTo(_buffer.AsSpan(0, remaining));
        _bufferLength = remaining;
    }

    protected override byte[] HashFinal()
    {
        Transform(_buffer.AsSpan(0, _bufferLength), _hashValue);
        return _hashValue;
    }

    private void Transform(ReadOnlySpan<byte> block, Span<byte> hash)
    {
        Sha224Algorithm(block, _state, _state, hash);
    }

    public static byte[] HashData(ReadOnlySpan<byte> data)
    {
        var array = new byte[224 / 8];
        Sha224Algorithm(data, _initialHashValues, stackalloc uint[_initialHashValues.Length], array);
        return array;
    }

    public new static SHA224 Create() => new();
}