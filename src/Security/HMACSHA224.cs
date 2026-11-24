using System;
using System.Buffers;
using System.Security.Cryptography;

namespace SnmpSharpNet
{
    public sealed class HMACSHA224 : HMAC
    {
        private readonly SHA224 _sha224;

        public HMACSHA224(byte[] key)
            : this(GetKeySpan(key))
        {
        }

        public HMACSHA224(ReadOnlySpan<byte> key)
        {
            HashName = "SHA224";
            HashSizeValue = 224;
            BlockSizeValue = 64;
            _sha224 = SHA224.Create();
            SetKey(key);
        }

        public override byte[] Key
        {
            get => base.Key;
            set => SetKey(value);
        }

        private static ReadOnlySpan<byte> GetKeySpan(byte[] key)
        {
            ArgumentNullException.ThrowIfNull(key);
            return key;
        }

        private void SetKey(ReadOnlySpan<byte> key)
        {
            KeyValue = key.Length > BlockSizeValue ? SHA224.HashData(key) : key.ToArray();
            InitializeKey();
        }

        public override void Initialize()
        {
            _sha224.Initialize();
            InitializeKey();
        }

        private void InitializeKey()
        {
            var key = KeyValue;
            if (key.Length < BlockSizeValue)
            {
                var temp = new byte[BlockSizeValue];
                Buffer.BlockCopy(key, 0, temp, 0, key.Length);
                key = temp;
            }

            var i_pad = new byte[BlockSizeValue];

            for (int i = 0; i < BlockSizeValue; i++)
            {
                i_pad[i] = (byte)(key[i] ^ 0x36);
            }

            _sha224.TransformBlock(i_pad, 0, i_pad.Length, i_pad, 0);
        }

        protected override void HashCore(ReadOnlySpan<byte> source)
        {
            var array = ArrayPool<byte>.Shared.Rent(source.Length);
            source.CopyTo(array);
            HashCore(array, 0, source.Length);
            Array.Clear(array, 0, source.Length);
            ArrayPool<byte>.Shared.Return(array);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Array.Clear(KeyValue, 0, KeyValue.Length);
            }

            base.Dispose(disposing);
            _sha224.Dispose();
        }

        protected override bool TryHashFinal(Span<byte> destination, out int bytesWritten)
        {
            var hashSizeInBytes = HashSizeValue / 8;

            if (destination.Length >= hashSizeInBytes)
            {
                var final = HashFinal();
                if (final.Length != hashSizeInBytes)
                    throw new InvalidOperationException("SR.InvalidOperation_IncorrectImplementation");
                new ReadOnlySpan<byte>(final).CopyTo(destination);
                bytesWritten = final.Length;
                return true;
            }

            bytesWritten = 0;
            return false;
        }

        protected override void HashCore(byte[] rgb, int ib, int cb)
        {
            _sha224.TransformBlock(rgb, ib, cb, rgb, ib);
        }

        protected override byte[] HashFinal()
        {
            _sha224.TransformFinalBlock([], 0, 0);
            var innerHash = _sha224.Hash!;

            var o_pad = new byte[BlockSizeValue];
            var key = KeyValue;
            for (int i = 0; i < BlockSizeValue; i++)
            {
                o_pad[i] = (byte)((i < key.Length ? key[i] : 0) ^ 0x5C);
            }

            _sha224.Initialize();
            _sha224.TransformBlock(o_pad, 0, o_pad.Length, o_pad, 0);
            _sha224.TransformFinalBlock(innerHash, 0, innerHash.Length);

            return _sha224.Hash!;
        }
    }
}