namespace SnmpSharpNet.Tests.Types;

public class OctetStringTests
{
    [Test]
    [Arguments("yooooo")]
    public async Task EncodedToDecode(string a)
    {
        var initial = new OctetString(a);
        Span<byte> buffer = stackalloc byte[initial.ByteLength];
        initial.Encode(buffer);
        var @new = new OctetString();
        @new.Decode(buffer, 0);
        await Assert.That(@new).IsEqualTo(initial);
    }
}