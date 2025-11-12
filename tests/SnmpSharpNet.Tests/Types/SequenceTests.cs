namespace SnmpSharpNet.Tests.Types;

public class SequenceTests
{

    [Test]
    public async Task EncodedToDecode()
    {
        var initial = new Sequence([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
        Span<byte> buffer = stackalloc byte[initial.ByteLength];
        initial.Encode(buffer);
        var @new = new Sequence();
        @new.Decode(buffer, 0);
        var seqTree = string.Join(", ", @new.Value);
        var inTree = string.Join(", ", initial.Value);
        await Assert.That(seqTree).IsEqualTo(inTree);
    }
}