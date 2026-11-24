using System.Globalization;
using System.Text;

namespace SnmpSharpNet.Tests.Types;

public class SpanValueTests
{
    [Test]
    public async Task NumericValuesSupportSpanParsingAndUtf8Formatting()
    {
        var integer = new Integer32(-12345);
        Span<char> chars = stackalloc char[16];
        await Assert.That(integer.TryFormat(chars, out var charsWritten, default, CultureInfo.InvariantCulture))
            .IsTrue();
        await Assert.That(new string(chars[..charsWritten])).IsEqualTo("-12345");
        await Assert.That(Integer32.TryParse(chars[..charsWritten], CultureInfo.InvariantCulture, out var parsed))
            .IsTrue();
        await Assert.That(parsed.Value).IsEqualTo(-12345);

        Span<byte> utf8 = stackalloc byte[16];
        await Assert.That(integer.TryFormat(utf8, out var bytesWritten, default, CultureInfo.InvariantCulture))
            .IsTrue();
        await Assert.That(Encoding.UTF8.GetString(utf8[..bytesWritten])).IsEqualTo("-12345");

        await Assert.That(UInteger32.TryParse("4294967295".AsSpan(), CultureInfo.InvariantCulture, out var unsigned))
            .IsTrue();
        await Assert.That(unsigned.Value).IsEqualTo(uint.MaxValue);
        await Assert.That(Counter64.TryParse("18446744073709551615".AsSpan(), CultureInfo.InvariantCulture,
            out var counter)).IsTrue();
        await Assert.That(counter.Value).IsEqualTo(ulong.MaxValue);
    }

    [Test]
    public async Task ObjectIdentifiersSupportSpanParsingAndFormatting()
    {
        var oid = Oid.Parse(".1.3.6.1.4.1".AsSpan(), CultureInfo.InvariantCulture);
        Span<char> chars = stackalloc char[32];
        await Assert.That(oid.TryFormat(chars, out var charsWritten, default, CultureInfo.InvariantCulture))
            .IsTrue();
        await Assert.That(new string(chars[..charsWritten])).IsEqualTo("1.3.6.1.4.1");

        Span<byte> utf8 = stackalloc byte[32];
        await Assert.That(oid.TryFormat(utf8, out var bytesWritten, default, CultureInfo.InvariantCulture))
            .IsTrue();
        await Assert.That(Encoding.UTF8.GetString(utf8[..bytesWritten])).IsEqualTo("1.3.6.1.4.1");
    }

    [Test]
    public async Task OctetStringUsesValidUtf8AsTextAndFormatsWithoutIntermediateStrings()
    {
        var text = new OctetString("café");
        await Assert.That(text.IsHex).IsFalse();
        Span<char> chars = stackalloc char[8];
        await Assert.That(text.TryFormat(chars, out var charsWritten, default, CultureInfo.InvariantCulture))
            .IsTrue();
        await Assert.That(new string(chars[..charsWritten])).IsEqualTo("café");

        Span<byte> utf8 = stackalloc byte[8];
        await Assert.That(text.TryFormat(utf8, out var bytesWritten, default, CultureInfo.InvariantCulture))
            .IsTrue();
        await Assert.That(utf8[..bytesWritten].SequenceEqual(Encoding.UTF8.GetBytes("café"))).IsTrue();

        var invalidUtf8 = new OctetString(new byte[] { 0xc3, 0x28 });
        await Assert.That(invalidUtf8.IsHex).IsTrue();
        await Assert.That(invalidUtf8.ToString()).IsEqualTo("C3 28");
    }

    [Test]
    public async Task IpAndEthernetAddressesHaveSpanParsableRepresentations()
    {
        await Assert.That(IpAddress.TryParse("192.0.2.1".AsSpan(), CultureInfo.InvariantCulture, out var ip))
            .IsTrue();
        Span<char> ipChars = stackalloc char[16];
        await Assert.That(ip.TryFormat(ipChars, out var ipCharsWritten, default, CultureInfo.InvariantCulture))
            .IsTrue();
        await Assert.That(new string(ipChars[..ipCharsWritten])).IsEqualTo("192.0.2.1");
        await Assert.That(IpAddress.TryParse("2001:db8::1", CultureInfo.InvariantCulture, out _)).IsFalse();

        await Assert.That(EthernetAddress.TryParse("00:11:22:aa:bb:cc".AsSpan(), CultureInfo.InvariantCulture,
            out var mac)).IsTrue();
        Span<char> macChars = stackalloc char[14];
        await Assert.That(mac.TryFormat(macChars, out var macCharsWritten, default, CultureInfo.InvariantCulture))
            .IsTrue();
        await Assert.That(new string(macChars[..macCharsWritten])).IsEqualTo("0011.22aa.bbcc");
    }

    [Test]
    public async Task TimeTicksSpanFormattingPreservesDurationRepresentation()
    {
        var ticks = new TimeTicks(12345);
        Span<char> chars = stackalloc char[40];
        await Assert.That(ticks.TryFormat(chars, out var charsWritten, default, CultureInfo.InvariantCulture))
            .IsTrue();
        var formatted = new string(chars[..charsWritten]);
        await Assert.That(formatted).IsEqualTo(ticks.ToString());
        await Assert.That(TimeTicks.TryParse(formatted, CultureInfo.InvariantCulture, out var parsed)).IsTrue();
        await Assert.That(parsed.Value).IsEqualTo(ticks.Value);
    }

    [Test]
    public async Task IntegerEncodersWriteMinimalBigEndianValues()
    {
        Span<byte> buffer = stackalloc byte[Integer32.MaxEncodedSize];
        var length = new Integer32(-129).Encode(buffer);
        await Assert.That(buffer[..length].SequenceEqual(new byte[] { 0x02, 0x02, 0xff, 0x7f })).IsTrue();

        length = new Integer32(128).Encode(buffer);
        await Assert.That(buffer[..length].SequenceEqual(new byte[] { 0x02, 0x02, 0x00, 0x80 })).IsTrue();

        Span<byte> unsignedBuffer = stackalloc byte[UInteger32.MaxEncodedSize];
        length = new UInteger32(uint.MaxValue).Encode(unsignedBuffer);
        await Assert.That(unsignedBuffer[..length].SequenceEqual(
            new byte[] { SnmpConstants.SMI_UNSIGNED32, 0x05, 0x00, 0xff, 0xff, 0xff, 0xff })).IsTrue();

        Span<byte> counterBuffer = stackalloc byte[Counter64.MaxEncodedSize];
        length = new Counter64(ulong.MaxValue).Encode(counterBuffer);
        await Assert.That(counterBuffer[..length].SequenceEqual(
            new byte[] { SnmpConstants.SMI_COUNTER64, 0x08, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff }))
            .IsTrue();
    }
}
