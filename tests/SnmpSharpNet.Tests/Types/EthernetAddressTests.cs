namespace SnmpSharpNet.Tests.Types;

public class EthernetAddressTests
{
    [Test]
    public async Task EncodedToDecode()
    {
        var initial = new EthernetAddress([1, 2, 3, 4, 5, 6]);
        Span<byte> buffer = stackalloc byte[initial.ByteLength];
        initial.Encode(buffer);
        var @new = new EthernetAddress();
        @new.Decode(buffer, 0);
        await Assert.That(@new).IsEqualTo(initial);
    }
}