namespace SnmpSharpNet.Tests;

public class VbTests
{
    [Test]
    public async Task EncodedToDecodeMutable()
    {
        var initial = GetInitial();
        var buffer = new MutableByte();
        initial.encode(buffer);
        var @new = new Vb();
        @new.decode(buffer, 0);
        await Assert.That(@new.ToString()).IsEqualTo(initial.ToString());
    }

    private static Vb GetInitial()
    {
        var pdu = new Vb("1.2.3.4");
        return pdu;
    }

    [Test]
    public async Task EncodedToDecode()
    {
        var initial = GetInitial();
        Span<byte> buffer = stackalloc byte[initial.ByteLength];
        initial.encode(buffer);
        var @new = new Vb();
        @new.decode(buffer, 0);
        await Assert.That(@new.ToString()).IsEqualTo(initial.ToString());
    }

    [Test]
    public async Task BothMethodsShouldProduceEqualBuffers()
    {
        var initial = GetInitial();
        Span<byte> buffer = stackalloc byte[initial.ByteLength];
        initial.encode(buffer);
        var arr = buffer.ToArray();
        var secondBuffer = new MutableByte();
        initial.encode(secondBuffer);
        for (int i = 0; i < arr.Length; i++)
        {
            await Assert.That(arr[i]).IsEqualTo(secondBuffer[i]);
        }
    }
}