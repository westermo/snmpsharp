namespace SnmpSharpNet.Tests.Types;

public class NullTests
{
    [Test]
    public async Task EncodedToDecode()
    {
        var initial = new Null();
        Span<byte> buffer = stackalloc byte[initial.ByteLength];
        initial.Encode(buffer);
        var @new = new Null();
        @new.Decode(buffer, 0);
        await Assert.That(@new.ToString()).IsEqualTo(initial.ToString());
    }
}