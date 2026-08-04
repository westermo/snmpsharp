using Snmp.Iso.Org.Dod.Internet.Private.Enterprises.WestermoOid.Common.WestermoInterface.WmoInterfaceObjects;
using Snmp.Iso.Std.Iso8802.Ieee802dot1.Ieee802dot1mibs.Lldp.Objects;
using SnmpSharpNet;
using LldpLocalSystemData = Snmp.Iso.Std.Iso8802.Ieee802dot1.Ieee802dot1mibs.Lldp.Objects.LocalSystemData;

namespace SnmpSharpNet.Mib.SourceGenerator.IntegrationTests;

public class SourceGeneratorIntegrationTests
{
    [Test]
    public async Task GeneratedLeaf_ExposesParentRelativeOid()
    {
        var oid = global::Snmp.Iso.Std.Iso8802.Ieee802dot1.Ieee802dot1mibs.Lldp.Objects.LocalSystemData
            .LocChassisIdSubtype.Oid;

        await Assert.That(oid).IsEqualTo(new Oid("1.0.8802.1.1.2.1.3.1"));
        await Assert.That(LldpLocalSystemData.LocChassisIdSubtype.InstanceOid)
            .IsEqualTo(new Oid("1.0.8802.1.1.2.1.3.1.0"));
    }

    [Test]
    public async Task GeneratedLeaf_ParsesExpectedAsnType()
    {
        var value = LldpLocalSystemData.LocChassisIdSubtype.Get(new Integer32(4));

        await Assert.That(value).IsEqualTo(new Integer32(4));
    }

    [Test]
    public async Task GeneratedTable_ExposesOidAndParser()
    {
        await Assert.That(IfRefTable.Oid)
            .IsEqualTo(new Oid("1.3.6.1.4.1.16177.2.4.1.1"));
        await Assert.That(IfRefTable.FromValues(new Dictionary<Oid, AsnType>()))
            .IsEmpty();
    }

    [Test]
    public async Task GeneratedTable_RoundTripsValues()
    {
        var values = new Dictionary<Oid, AsnType>
        {
            [new Oid("1.3.6.1.4.1.16177.2.4.1.1.1.2.1")] = new Integer32(10),
            [new Oid("1.3.6.1.4.1.16177.2.4.1.1.1.3.1")] = new OctetString("eth0"),
            [new Oid("1.3.6.1.4.1.16177.2.4.1.1.1.4.1")] = new OctetString("Ethernet 0"),
            [new Oid("1.3.6.1.4.1.16177.2.4.1.1.1.5.1")] = new Integer32(6),
        };

        var entries = IfRefTable.FromValues(values);

        await Assert.That(entries).HasSingleItem();
        await Assert.That(entries[0].IndexIndex).IsEqualTo(1u);
        await Assert.That(entries[0].ifName).IsEqualTo(new OctetString("eth0"));
        var roundTripped = IfRefTable.ToValues(entries);
        await Assert.That(roundTripped.Count).IsEqualTo(values.Count);
        await Assert.That(roundTripped[new Oid("1.3.6.1.4.1.16177.2.4.1.1.1.3.1")])
            .IsEqualTo(new OctetString("eth0"));
    }
}