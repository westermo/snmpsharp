namespace SnmpSharpNet.Tests.Types;

public class V2ErrorTests
{
    [Test]
    public async Task EncodedToDecode()
    {
        var initial = new V2Error();
        Span<byte> buffer = stackalloc byte[initial.ByteLength];
        initial.Encode(buffer);
        var @new = new V2Error();
        @new.Decode(buffer, 0);
        await Assert.That(@new.ToString()).IsEqualTo(initial.ToString());
    }
}