namespace SnmpSharpNet.Tests.Types;

public class TimeTicksTests
{
    [Test]
    [Arguments(1u)]
    [Arguments(2u)]
    [Arguments(3u)]
    public async Task EncodedToDecode(uint a)
    {
        var initial = new TimeTicks(a);
        Span<byte> buffer = stackalloc byte[initial.ByteLength];
        initial.Encode(buffer);
        var @new = new TimeTicks();
        @new.Decode(buffer, 0);
        await Assert.That(@new).IsEqualTo(initial);
    }
}