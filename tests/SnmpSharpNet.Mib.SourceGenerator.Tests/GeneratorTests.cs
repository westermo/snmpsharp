//using SnmpSharpNet;
using SnmpSharpNet.Mib.Attributes;

//[assembly: MibModules("IANAifType-MIB", "IANA-ADDRESS-FAMILY-NUMBERS-MIB", "IF-MIB", "WESTERMO-OID-MIB", "WESTERMO-INTERFACE-MIB", "SNMP-FRAMEWORK-MIB", "LLDP-MIB")]

namespace SnmpSharpNet.Mib.SourceGenerator.IntegrationTests;

[MibOids("WESTERMO-INTERFACE-MIB")]
internal partial class WestermoInterfaceMib;

[MibOids("LLDP-MIB::lldpLocChassisIdSubtype", "LLDP-MIB::lldpLocChassisId")]
internal partial class LldpMib;

public class SourceGeneratorIntegrationTests
{
    private static readonly Dictionary<Oid, AsnType> Values = new() {
        // <LLDP-MIB>::lldpLocChassisIdSubtype
        { new Oid("1.0.8802.1.1.2.1.3.1.0"), new Integer32(4) },
        // <LLDP-MIB>::lldpLocChassisId
        { new Oid("1.0.8802.1.1.2.1.3.2.0"), new OctetString([0x00, 0x11, 0xB4, 0x62, 0x2E, 0xE0]) },

        // <WESTERMO-INTERFACE-MIB>::ifRefTable: 1.3.6.1.4.1.16177.2.4.1.1
        // OID layout: ifRefTable.1.<column>.<index>
        // Index 1 and 3, but a bit mixed in the ordering
        { new Oid("1.3.6.1.4.1.16177.2.4.1.1.1.5.3"), new Integer32(6) }, // ifRefifType, index 3
        { new Oid("1.3.6.1.4.1.16177.2.4.1.1.1.1.1"), new Integer32(1) },  // ifRefIndex, index 1
        { new Oid("1.3.6.1.4.1.16177.2.4.1.1.1.2.1"), new Integer32(10) }, // ifRefifIndex, index 1
        { new Oid("1.3.6.1.4.1.16177.2.4.1.1.1.3.1"), new OctetString("eth0") }, // ifRefifName, index 1
        { new Oid("1.3.6.1.4.1.16177.2.4.1.1.1.1.3"), new Integer32(3) },  // ifRefIndex, index 3
        { new Oid("1.3.6.1.4.1.16177.2.4.1.1.1.2.3"), new Integer32(13) }, // ifRefifIndex, index 3
        { new Oid("1.3.6.1.4.1.16177.2.4.1.1.1.4.1"), new OctetString("Ethernet 0") }, // ifRefifDescr, index 1
        { new Oid("1.3.6.1.4.1.16177.2.4.1.1.1.5.1"), new Integer32(6) }, // ifRefifType, index 1
        { new Oid("1.3.6.1.4.1.16177.2.4.1.1.1.3.3"), new OctetString("eth3") }, // ifRefifName, index 3
        { new Oid("1.3.6.1.4.1.16177.2.4.1.1.1.4.3"), new OctetString("Ethernet 3") }, // ifRefifDescr, index 3
    };

    [Test]
    public async Task WestermoTable_FromValues()
    {

        var carrier = WestermoInterfaceMib.FromValues(Values);
        
        await Assert.That(carrier).IsNotNull();
        await Assert.That(carrier!.ifRefTable).IsNotNull();
        await Assert.That(carrier.ifRefTable.Count).IsGreaterThan(0);


        // Verify the table entry at index 1 contains expected values
        var entry = carrier.ifRefTable.First(x => x.indexifRefIndex == new Integer32(1));
        await Assert.That(entry.ifRefIndex).IsEqualTo(new Integer32(1));
        await Assert.That(entry.ifRefifIndex).IsEqualTo(new Integer32(10));
        await Assert.That(entry.ifRefifName).IsEqualTo(new OctetString("eth0"));
        await Assert.That(entry.ifRefifDescr).IsEqualTo(new OctetString("Ethernet 0"));
        await Assert.That(entry.ifRefifType).IsEqualTo(new Integer32(6));

        // Verify the table entry at index 3 contains expected values
        entry = carrier.ifRefTable.First(x => x.indexifRefIndex == new Integer32(3));
        await Assert.That(entry.ifRefIndex).IsEqualTo(new Integer32(3));
        await Assert.That(entry.ifRefifIndex).IsEqualTo(new Integer32(13));
        await Assert.That(entry.ifRefifName).IsEqualTo(new OctetString("eth3"));
        await Assert.That(entry.ifRefifDescr).IsEqualTo(new OctetString("Ethernet 3"));
        await Assert.That(entry.ifRefifType).IsEqualTo(new Integer32(6));
    }

    [Test]
    public async Task LldpScalars_FromValues()
    {

        var lldp = LldpMib.FromValues(Values);
        
        await Assert.That(lldp).IsNotNull();

        await Assert.That(lldp.lldpLocChassisIdSubtype).IsEqualTo(new Integer32(4));
        await Assert.That(lldp.lldpLocChassisId).IsEqualTo(new OctetString([0x00, 0x11, 0xB4, 0x62, 0x2E, 0xE0]));
    }
    

    [Test]
    public async Task FromValues_Should_Fail()
    {
        var carrier = WestermoInterfaceMib.FromValues(new Dictionary<Oid, AsnType>());
        
        await Assert.That(carrier).IsNotNull();
        await Assert.That(carrier!.ifRefTable.Count).IsEqualTo(0);
    }
}
