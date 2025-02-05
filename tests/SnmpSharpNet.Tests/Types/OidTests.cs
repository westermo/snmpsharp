namespace SnmpSharpNet.Tests;

public class OidTests
{
    [Test]
    [Arguments("1.3.2.4.1.5.6.8")]
    [Arguments("1.3.2.4.1.5111.6.8")]
    [Arguments("1.3.2.4.1123123123.5111.6.8")]
    public async Task EncodedToDecodeMutable(string a)
    {
        var initial = new Oid(a);
        var buffer = new MutableByte();
        initial.encode(buffer);
        var @new = new Oid();
        @new.decode(buffer, 0);
        await Assert.That(@new).IsEqualTo(initial);
    }

    [Test]
    [Arguments("1.3.2.4.1.5.6.8")]
    [Arguments("1.3.2.4.1.5111.6.8")]
    [Arguments("1.3.2.4.1123123123.5111.6.8")]
    public async Task EncodedToDecode(string a)
    {
        var initial = new Oid(a);
        Span<byte> buffer = stackalloc byte[initial.ByteLength];
        initial.encode(buffer);
        var @new = new Oid();
        @new.decode(buffer, 0);
        await Assert.That(@new).IsEqualTo(initial);
    }

    [Test]
    [Arguments("1.3.2.4.1.5.6.8")]
    [Arguments("1.3.2.4.1.5111.6.8")]
    [Arguments("1.3.2.4.1123123123.5111.6.8")]
    public async Task BothMethodsShouldProduceEqualBuffers(string a)
    {
        var initial = new Oid(a);
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

    [Test]
    [Arguments("1")]
    [Arguments("1.3.2.4.1.5.6.8")]
    [Arguments("1.3.2.4.1.5111.6.8")]
    [Arguments("1.3.2.4.1123123123.5111.6.8")]
    public async Task EnsureLengthCalculationWorks(string value)
    {
        var integer = new Oid(value);
        Span<byte> stack = stackalloc byte[integer.ByteLength];
        var result = integer.encode(stack);
        await Assert.That(result).IsEqualTo(integer.ByteLength);
    }
}