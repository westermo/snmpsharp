namespace SnmpSharpNet.Tests.Security;

public class MsgFlagsTests
{
    [Test]
    public async Task EncodedToDecode()
    {
        var initial = new MsgFlags(false, true, false);
        Span<byte> buffer = stackalloc byte[initial.ByteLength];
        initial.Encode(buffer);
        var @new = new MsgFlags();
        @new.Decode(buffer, 0);
        await Assert.That(@new.ToString()).IsEqualTo(initial.ToString());
    }
}