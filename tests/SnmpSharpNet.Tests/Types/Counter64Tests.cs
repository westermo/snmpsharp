namespace SnmpSharpNet.Tests.Types;

public class Counter64Tests
{
    [Test]
    [Arguments(ulong.MinValue)]
    [Arguments(2u)]
    [Arguments(ulong.MaxValue)]
    public async Task EncodedToDecode(ulong a)
    {
        var initial = new Counter64(a);
        Span<byte> buffer = stackalloc byte[initial.ByteLength];
        initial.Encode(buffer);
        var @new = new Counter64();
        @new.Decode(buffer, 0);
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
        var result = integer.Encode(stack);
        await Assert.That(result).IsEqualTo(integer.ByteLength);
    }
}