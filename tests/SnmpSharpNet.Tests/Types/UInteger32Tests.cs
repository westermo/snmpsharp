namespace SnmpSharpNet.Tests.Types;

public class UInteger32Tests
{
    [Test]
    [Arguments(uint.MaxValue)]
    [Arguments(2u)]
    [Arguments(uint.MinValue)]
    public async Task EncodedToDecode(uint a)
    {
        var initial = new UInteger32(a);
        Span<byte> buffer = stackalloc byte[initial.ByteLength];
        initial.Encode(buffer);
        var @new = new UInteger32();
        @new.Decode(buffer, 0);
        await Assert.That(@new).IsEqualTo(initial);
    }

    [Test]
    [Arguments(uint.MinValue)]
    [Arguments(1)]
    [Arguments(64)]
    [Arguments(123)]
    [Arguments(128)]
    [Arguments(30000)]
    [Arguments(30000000)]
    [Arguments(uint.MaxValue)]
    public async Task EnsureLengthCalculationWorks(uint value)
    {
        Span<byte> stack = stackalloc byte[UInteger32.MaxEncodedSize];
        var integer = new UInteger32(value);
        var result = integer.Encode(stack);
        await Assert.That(result).IsEqualTo(integer.ByteLength);
    }
}