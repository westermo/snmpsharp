namespace SnmpSharpNet.Tests.Types;

public class OpaqueTests
{

    [Test]
    [Arguments("1.3.2.4.1.5.6.8")]
    public async Task EncodedToDecode(string a)
    {
        var initial = new Opaque(a);
        Span<byte> buffer = stackalloc byte[initial.ByteLength];
        initial.Encode(buffer);
        var @new = new Opaque();
        @new.Decode(buffer, 0);
        await Assert.That(@new).IsEqualTo(initial);
    }
}