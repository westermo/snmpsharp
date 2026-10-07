using System.Net;

namespace SnmpSharpNet.Tests.Types;

public class TypeConversionTests
{
    [Test]
    [Arguments(0)]
    [Arguments(-42)]
    [Arguments(int.MaxValue)]
    public async Task Integer32_RoundTripsInt(int value)
    {
        Integer32 smi = value;
        int back = smi;
        await Assert.That(smi.Value).IsEqualTo(value);
        await Assert.That(back).IsEqualTo(value);
        await Assert.That(smi.Type).IsEqualTo(SnmpConstants.SMI_INTEGER);
    }

    [Test]
    public async Task Integer32_MixedOperators()
    {
        Integer32 smi = 5;
        Integer32? nil = null;
        await Assert.That(smi == 5).IsTrue();
        await Assert.That(5 == smi).IsTrue();
        await Assert.That(smi != 6).IsTrue();
        await Assert.That(nil == 0).IsFalse();
        await Assert.That(smi + 2).IsEqualTo(7);
        await Assert.That(2 - smi).IsEqualTo(-3);
    }

    [Test]
    [Arguments(0u)]
    [Arguments(uint.MaxValue)]
    public async Task UInteger32_RoundTripsUInt(uint value)
    {
        UInteger32 smi = value;
        uint back = smi;
        await Assert.That(back).IsEqualTo(value);
        await Assert.That(smi.Type).IsEqualTo(SnmpConstants.SMI_UNSIGNED32);
    }

    [Test]
    public async Task UInteger32_MixedOperators()
    {
        UInteger32 smi = 5u;
        await Assert.That(smi == 5u).IsTrue();
        await Assert.That(5u != smi).IsFalse();
        await Assert.That(smi > 4u).IsTrue();
        await Assert.That(smi < 4u).IsFalse();
        await Assert.That(6u > smi).IsTrue();
        await Assert.That(smi + 1u).IsEqualTo(6u);
        await Assert.That(smi - 1u).IsEqualTo(4u);
    }

    [Test]
    public async Task DerivedUnsignedTypes_KeepAsnType()
    {
        Counter32 counter = 10u;
        Gauge32 gauge = 20u;
        TimeTicks ticks = 30u;
        await Assert.That(counter.Type).IsEqualTo(SnmpConstants.SMI_COUNTER32);
        await Assert.That(gauge.Type).IsEqualTo(SnmpConstants.SMI_GAUGE32);
        await Assert.That(ticks.Type).IsEqualTo(SnmpConstants.SMI_TIMETICKS);
        await Assert.That((uint)counter).IsEqualTo(10u);
        await Assert.That((uint)gauge).IsEqualTo(20u);
        await Assert.That((uint)ticks).IsEqualTo(30u);
    }

    [Test]
    public async Task ExistingConstructors_RemainUnambiguous()
    {
        var gauge = new Gauge32(1);
        var ticks = new TimeTicks(42);
        var copy = new UInteger32(new Counter32(7));
        var gaugeCopy = new Gauge32(gauge);
        await Assert.That(gauge.Value).IsEqualTo(1u);
        await Assert.That(ticks.Value).IsEqualTo(42u);
        await Assert.That(copy.Value).IsEqualTo(7u);
        await Assert.That(copy.Type).IsEqualTo(SnmpConstants.SMI_UNSIGNED32);
        await Assert.That(gaugeCopy.Value).IsEqualTo(1u);
        await Assert.That(gaugeCopy.Type).IsEqualTo(SnmpConstants.SMI_GAUGE32);
    }

    [Test]
    public async Task TimeTicks_FromTimeSpan()
    {
        var ticks = (TimeTicks)TimeSpan.FromSeconds(12.34);
        await Assert.That(ticks.Value).IsEqualTo(1234u);
        await Assert.That(((TimeSpan)ticks).TotalSeconds).IsEqualTo(12.34);
    }

    [Test]
    public async Task TimeTicks_FromNegativeTimeSpan_Throws()
    {
        await Assert.That(() => (TimeTicks)TimeSpan.FromSeconds(-1)).Throws<OverflowException>();
    }

    [Test]
    public async Task Counter64_ConversionsAndOperators()
    {
        Counter64 smi = ulong.MaxValue;
        ulong back = smi;
        await Assert.That(back).IsEqualTo(ulong.MaxValue);
        await Assert.That(smi.Type).IsEqualTo(SnmpConstants.SMI_COUNTER64);

        Counter64 five = 5ul;
        await Assert.That(five == 5ul).IsTrue();
        await Assert.That(5ul != five).IsFalse();
        await Assert.That(five > 4ul).IsTrue();
        await Assert.That(4ul < five).IsTrue();
        await Assert.That(five + 1ul).IsEqualTo(6ul);
        await Assert.That(10ul - five).IsEqualTo(5ul);
    }

    [Test]
    public async Task OctetString_StringRoundTrip()
    {
        OctetString smi = "hello";
        string text = smi;
        await Assert.That(text).IsEqualTo("hello");
        await Assert.That(smi.ToArray().SequenceEqual("hello"u8.ToArray())).IsTrue();
    }

    [Test]
    public async Task OctetString_NonPrintable_ConvertsToHex()
    {
        OctetString smi = new byte[] { 0x00, 0x01, 0xFF };
        string text = smi;
        await Assert.That(text).IsEqualTo("00 01 FF");
    }

    [Test]
    public async Task OctetString_ByteArrayRoundTrip()
    {
        var data = new byte[] { 1, 2, 3 };
        OctetString smi = data;
        byte[] back = smi;
        await Assert.That(back.SequenceEqual(data)).IsTrue();
    }

    [Test]
    public async Task OctetString_NullConversions()
    {
        string? nullString = null;
        byte[]? nullBytes = null;
        OctetString? fromString = nullString;
        OctetString? fromBytes = nullBytes;
        OctetString? nil = null;
        string? backToString = nil;
        await Assert.That(fromString).IsNull();
        await Assert.That(fromBytes).IsNull();
        await Assert.That(backToString).IsNull();
    }

    [Test]
    public async Task Oid_StringRoundTrip()
    {
        Oid oid = "1.3.6.1.2.1";
        string text = oid;
        await Assert.That(text).IsEqualTo("1.3.6.1.2.1");
        await Assert.That(oid.Length).IsEqualTo(6);
    }

    [Test]
    public async Task Oid_InvalidString_Throws()
    {
        await Assert.That(() =>
        {
            Oid oid = "1..3";
            return oid;
        }).Throws<FormatException>();
    }

    [Test]
    public async Task Oid_UIntArrayRoundTrip_ReturnsCopy()
    {
        Oid oid = new uint[] { 1, 3, 6 };
        uint[] first = oid;
        first[0] = 99;
        uint[] second = oid;
        await Assert.That(second.SequenceEqual(new uint[] { 1, 3, 6 })).IsTrue();
    }

    [Test]
    public async Task Oid_StringConcatenation()
    {
        Oid oid = "1.3.6";
        var text = "oid: " + oid;
        await Assert.That(text).IsEqualTo("oid: 1.3.6");
        var appended = oid + (Oid)"1.2";
        await Assert.That(appended?.ToString()).IsEqualTo("1.3.6.1.2");
    }

    [Test]
    public async Task IpAddress_ConvertsToAndFromIPAddress()
    {
        var ip = (IpAddress)IPAddress.Parse("192.168.1.10");
        IPAddress back = ip;
        await Assert.That(ip.ToString()).IsEqualTo("192.168.1.10");
        await Assert.That(back).IsEqualTo(IPAddress.Parse("192.168.1.10"));
    }

    [Test]
    public async Task IpAddress_FromIPv6_Throws()
    {
        await Assert.That(() => (IpAddress)IPAddress.IPv6Loopback).Throws<OverflowException>();
    }
}
