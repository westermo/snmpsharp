namespace SnmpSharpNet.Tests.Types;

public class Counter32Tests
{

    [Test]
    [Arguments(uint.MaxValue)]
    [Arguments(2u)]
    [Arguments(uint.MinValue)]
    public async Task EncodedToDecode(uint a)
    {
        var initial = new Counter32(a);
        Span<byte> buffer = stackalloc byte[initial.ByteLength];
        initial.Encode(buffer);
        var @new = new Counter32();
        @new.Decode(buffer, 0);
        await Assert.That(@new).IsEqualTo(initial);
    }
}