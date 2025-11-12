namespace SnmpSharpNet.Tests.Types;

public class Integer32Tests
{

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(30000000)]
    [Arguments(int.MaxValue)]
    [Arguments(-2)]
    [Arguments(-1)]
    [Arguments(-22)]
    [Arguments(int.MinValue)]
    public async Task EncodedToDecode(int a)
    {
        var initial = new Integer32(a);
        Span<byte> buffer = stackalloc byte[initial.ByteLength];
        initial.Encode(buffer);
        var @new = new Integer32();
        @new.Decode(buffer, 0);
        await Assert.That(@new).IsEqualTo(initial);
    }

    [Test]
    [Arguments(int.MinValue)]
    [Arguments(-2212314)]
    [Arguments(-22)]
    [Arguments(-1)]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(64)]
    [Arguments(123)]
    [Arguments(128)]
    [Arguments(30000)]
    [Arguments(30000000)]
    [Arguments(int.MaxValue)]
    public async Task EnsureLengthCalculationWorks(int value)
    {
        Span<byte> stack = stackalloc byte[Integer32.MaxEncodedSize];
        var integer = new Integer32(value);
        var result = integer.Encode(stack);
        await Assert.That(result).IsEqualTo(integer.ByteLength);
    }
}