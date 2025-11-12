namespace SnmpSharpNet.Tests.Types;

public class NoSuchInstanceTests
{
    [Test]
    public async Task EncodedToDecode()
    {
        var initial = new NoSuchInstance();
        Span<byte> buffer = stackalloc byte[initial.ByteLength];
        initial.Encode(buffer);
        var @new = new NoSuchInstance();
        @new.Decode(buffer, 0);
        await Assert.That(@new.ToString()).IsEqualTo(initial.ToString());
    }
}