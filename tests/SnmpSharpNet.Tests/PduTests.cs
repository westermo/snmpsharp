namespace SnmpSharpNet.Tests;

public class PduTests
{
    [Test]
    public async Task EncodedToDecodeMutable()
    {
        var initial = GetInitial();
        var buffer = new MutableByte();
        initial.encode(buffer);
        var @new = new Pdu();
        @new.decode(buffer, 0);
        await Assert.That(@new.ToString()).IsEqualTo(initial.ToString());
    }

    private static Pdu GetInitial()
    {
        var pdu = new Pdu(PduType.Get)
        {
            RequestId = Random.Shared.Next()
        };
        pdu.VbList.Add(new Oid("1.3.1.4.2.5"));
        pdu.VbList.Add(new Oid("1.3.1.42.2.1.412.12.4.1"));
        return pdu;
    }

    [Test]
    public async Task EncodedToDecode()
    {
        var initial = GetInitial();
        Span<byte> buffer = stackalloc byte[initial.ByteLength];
        initial.encode(buffer);
        var @new = new Pdu();
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