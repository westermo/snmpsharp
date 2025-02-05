namespace SnmpSharpNet.Tests;

public class Counter64Tests
{
    [Test]
    [Arguments(ulong.MinValue)]
    [Arguments(2u)]
    [Arguments(ulong.MaxValue)]
    public async Task EncodedToDecodeMutable(ulong a)
    {
        var initial = new Counter64(a);
        var buffer = new MutableByte();
        initial.encode(buffer);
        var @new = new Counter64();
        @new.decode(buffer, 0);
        await Assert.That(@new).IsEqualTo(initial);
    }

    [Test]
    [Arguments(ulong.MinValue)]
    [Arguments(2u)]
    [Arguments(ulong.MaxValue)]
    public async Task EncodedToDecode(ulong a)
    {
        var initial = new Counter64(a);
        Span<byte> buffer = stackalloc byte[initial.ByteLength];
        initial.encode(buffer);
        var @new = new Counter64();
        @new.decode(buffer, 0);
        await Assert.That(@new).IsEqualTo(initial);
    }

    [Test]
    [Arguments(ulong.MinValue)]
    [Arguments(1)]
    [Arguments(64)]
    [Arguments(123)]
    [Arguments(128)]
    [Arguments(30000)]
    [Arguments(30000000)]
    [Arguments(ulong.MaxValue)]
    public async Task EnsureLengthCalculationWorks(ulong value)
    {
        Span<byte> stack = stackalloc byte[UInteger32.MaxEncodedSize];
        var integer = new Counter64(value);
        var result = integer.encode(stack);
        await Assert.That(result).IsEqualTo(integer.ByteLength);
    }

    [Test]
    [Arguments(ulong.MinValue)]
    [Arguments(64)]
    [Arguments(123)]
    [Arguments(ulong.MaxValue)]
    public async Task BothMethodsShouldProduceEqualBuffers(ulong a)
    {
        var initial = new Counter64(a);
        Span<byte> buffer = stackalloc byte[initial.ByteLength];
        initial.encode(buffer);
        var arr = buffer.ToArray();
        var secondBuffer = new MutableByte();
        initial.encode(secondBuffer);
        for (int i = 0; i < arr.Length; i++)
        {
            await Assert.That(arr[i]).IsEqualTo(secondBuffer[i]);
        }
    }
}