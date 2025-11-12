namespace SnmpSharpNet.Tests.Types;

public class EndOfMibViewTests
{
    [Test]
    public async Task EncodedToDecode()
    {
        var initial = new EndOfMibView();
        Span<byte> buffer = stackalloc byte[initial.ByteLength];
        initial.Encode(buffer);
        var @new = new EndOfMibView();
        @new.Decode(buffer, 0);
        await Assert.That(@new.ToString()).IsEqualTo(initial.ToString());
    }
}