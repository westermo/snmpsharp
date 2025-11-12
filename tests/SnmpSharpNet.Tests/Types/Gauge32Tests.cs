namespace SnmpSharpNet.Tests.Types;

public class Gauge32Tests
{
    [Test]
    [Arguments(1u)]
    [Arguments(2u)]
    [Arguments(3u)]
    public async Task EncodedToDecode(uint a)
    {
        var initial = new Gauge32(a);
        Span<byte> buffer = stackalloc byte[initial.ByteLength];
        initial.Encode(buffer);
        var @new = new Gauge32();
        @new.Decode(buffer, 0);
        await Assert.That(@new).IsEqualTo(initial);
    }
}