using SnmpSharpNet;
using SnmpSharpNet.Mib.Attributes;

[assembly: MibModules("WESTERMO-OID-MIB", "WESTERMO-INTERFACE-MIB")]

namespace SnmpSharpNet.Mib.SourceGenerator.IntegrationTests;

[MibOids("WESTERMO-INTERFACE-MIB")]
internal partial class WestermoInterfaceMib;

public class SourceGeneratorIntegrationTests
{
    private static readonly Dictionary<Oid, AsnType> Values = new() {
        // <WESTERMO-INTERFACE-MIB>::ifRefTable: 1.3.6.1.4.1.16177.2.4.1.1
        // Index 1 and 3, but a bit mixed in the ordering
        { new Oid("1.3.6.1.4.1.16177.2.4.1.1.3.5"), new Integer32(6) }, // ifRefifType
        { new Oid("1.3.6.1.4.1.16177.2.4.1.1.1.1"), new Integer32(1) },  // ifRefIndex
        { new Oid("1.3.6.1.4.1.16177.2.4.1.1.1.2"), new Integer32(10) }, // ifRefifIndex
        { new Oid("1.3.6.1.4.1.16177.2.4.1.1.1.3"), new OctetString("eth0") }, // ifRefifName
        { new Oid("1.3.6.1.4.1.16177.2.4.1.1.3.1"), new Integer32(3) },  // ifRefIndex
        { new Oid("1.3.6.1.4.1.16177.2.4.1.1.3.2"), new Integer32(13) }, // ifRefifIndex
        { new Oid("1.3.6.1.4.1.16177.2.4.1.1.1.4"), new OctetString("Ethernet 0") }, // ifRefifDescr
        { new Oid("1.3.6.1.4.1.16177.2.4.1.1.1.5"), new Integer32(6) }, // ifRefifType
        // let's simulate the value getting lost for some reason...
        //{ new Oid("1.3.6.1.4.1.16177.2.4.1.1.3.3"), new OctetString("eth3") }, // ifRefifName
        { new Oid("1.3.6.1.4.1.16177.2.4.1.1.3.4"), new OctetString("Ethernet 3") }, // ifRefifDescr
    };

    [Test]
    public async Task FromValues()
    {
        var carrier = WestermoInterfaceMib.FromValues(Values);
        
        await Assert.That(carrier).IsNotNull();
        await Assert.That(carrier!.ifRefTable).IsNotNull();
        await Assert.That(carrier.ifRefTable.Count).IsGreaterThan(0);

        // Verify the table entry at index 1 contains expected values
        var entryIndex = new Oid("1");
        await Assert.That(carrier.ifRefTable.ContainsKey(entryIndex)).IsTrue();
        var entry = carrier.ifRefTable[entryIndex];
        await Assert.That(entry.ifRefIndex).IsEqualTo(new Integer32(1));
        await Assert.That(entry.ifRefifIndex).IsEqualTo(new Integer32(10));
        await Assert.That(entry.ifRefifName).IsEqualTo(new OctetString("eth0"));
        await Assert.That(entry.ifRefifDescr).IsEqualTo(new OctetString("Ethernet 0"));
        await Assert.That(entry.ifRefifType).IsEqualTo(new Integer32(6));

        // Verify the table entry at index 3 contains expected values
        entryIndex = new Oid("3");
        await Assert.That(carrier.ifRefTable.ContainsKey(entryIndex)).IsTrue();
        entry = carrier.ifRefTable[entryIndex];
        await Assert.That(entry.ifRefIndex).IsEqualTo(new Integer32(3));
        await Assert.That(entry.ifRefifIndex).IsEqualTo(new Integer32(13));
        await Assert.That(entry.ifRefifName.Type).IsEqualTo(SnmpConstants.SMI_NOSUCHINSTANCE);
        await Assert.That(entry.ifRefifDescr).IsEqualTo(new OctetString("Ethernet 3"));
        await Assert.That(entry.ifRefifType).IsEqualTo(new Integer32(6));
    }

    [Test]
    public async Task FromValues_Should_Fail()
    {
        var carrier = WestermoInterfaceMib.FromValues(new Dictionary<Oid, AsnType>());
        
        await Assert.That(carrier).IsNotNull();
        await Assert.That(carrier!.ifRefTable.Count).IsEqualTo(0);
    }
}
