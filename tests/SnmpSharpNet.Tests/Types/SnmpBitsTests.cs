namespace SnmpSharpNet.Tests.Types;

public class SnmpBitsTests
{
    [Test]
    public async Task ToMask_UsesMostSignificantBitFirst()
    {
        var mask = SnmpBits.ToMask(new OctetString(new byte[] { 0b1000_0001, 0b0100_0000 }));

        await Assert.That(mask).IsEqualTo((1UL << 0) | (1UL << 7) | (1UL << 9));
    }

    [Test]
    public async Task ToMask_IgnoresBitsBeyond63()
    {
        var octets = new byte[9];
        octets[0] = 0x80;
        octets[8] = 0xFF;

        await Assert.That(SnmpBits.ToMask(new OctetString(octets))).IsEqualTo(1UL);
    }

    [Test]
    public async Task FromMask_PadsToMinimumOctets()
    {
        var value = SnmpBits.FromMask(1UL << 1, 2);

        await Assert.That(Convert.ToHexString(value.ToArray())).IsEqualTo("4000");
    }

    [Test]
    public async Task FromMask_EmptyMaskWithoutMinimum_IsEmpty()
    {
        await Assert.That(SnmpBits.FromMask(0).ToArray()).IsEmpty();
    }

    [Test]
    public async Task RoundTrips()
    {
        const ulong mask = (1UL << 0) | (1UL << 13) | (1UL << 63);

        await Assert.That(SnmpBits.ToMask(SnmpBits.FromMask(mask))).IsEqualTo(mask);
        await Assert.That(SnmpBits.FromMask(mask).ToArray().Length).IsEqualTo(8);
    }
}
