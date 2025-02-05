namespace SnmpSharpNet.Tests;

public class VbCollectionTests
{
    [Test]
    public async Task EncodedToDecodeMutable()
    {
        var initial = GetInitial();
        var buffer = new MutableByte();
        initial.encode(buffer);
        var @new = new VbCollection();
        @new.decode(buffer, 0);
        await Assert.That(@new.Count).IsEqualTo(@new.Count);
        for (int i = 0; i < initial.Count; i++)
        {
            var a = initial[i];
            var b = @new[i];
            await Assert.That(a.ToString()).IsEqualTo(b.ToString());
        }
    }

    private static VbCollection GetInitial()
    {
        var pdu = new VbCollection
        {
            new Oid("1.3.1"),
            new Oid("1.3.1.42.2.1.412.12.4.1")
        };
        return pdu;
    }

    [Test]
    public async Task EncodedToDecode()
    {
        var initial = GetInitial();
        Span<byte> buffer = stackalloc byte[initial.ByteLength];
        initial.encode(buffer);
        var @new = new VbCollection();
        @new.decode(buffer, 0);
        await Assert.That(@new.Count).IsEqualTo(@new.Count);
        for (int i = 0; i < initial.Count; i++)
        {
            var a = initial[i];
            var b = @new[i];
            await Assert.That(a.ToString()).IsEqualTo(b.ToString());
        }
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