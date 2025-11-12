namespace SnmpSharpNet.Tests.Types;

public class V2PartyClockTests
{
    [Test]
    [Arguments(1u)]
    [Arguments(2u)]
    [Arguments(3u)]
    public async Task EncodedToDecode(uint a)
    {
        var initial = new V2PartyClock(new UInteger32(a));
        Span<byte> buffer = stackalloc byte[initial.ByteLength];
        initial.Encode(buffer);
        var @new = new V2PartyClock();
        @new.Decode(buffer, 0);
        await Assert.That(@new).IsEqualTo(initial);
    }
}