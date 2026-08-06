using Snmp.Iso.Org.Dod.Internet.Private.Enterprises.WestermoOid.Common.WestermoInterface.WmoInterfaceObjects;
using System.Reflection;
using Snmp.Iso.Org.Dod.Internet.SnmpV2.SnmpModules.SNMPv2.SnmpMIBObjects.SnmpTraps;
using LocalSystemData = Snmp.Iso.Std.Iso8802.Ieee802dot1.Ieee802dot1mibs.Lldp.Objects.LocalSystemData;

namespace SnmpSharpNet.Mib.SourceGenerator.IntegrationTests;

public class SourceGeneratorIntegrationTests
{
    private static readonly string GeneratedDir = typeof(SourceGeneratorIntegrationTests).Assembly
                                                      .GetCustomAttributes<AssemblyMetadataAttribute>()
                                                      .Single(attribute =>
                                                          attribute.Key == "CompilerGeneratedFilesOutputPath")
                                                      .Value
                                                  ?? throw new InvalidOperationException(
                                                      "The compiler generated-files output path is not available.");

    private static string[] Files =>
        field ??= Directory.GetFiles(GeneratedDir, "*.generated.cs", SearchOption.AllDirectories);

    private static string[] Code => field ??= [.. Files.Select(File.ReadAllText)];

    private static string FindGeneratedSource(string typeDeclaration)
    {
        return Code.Single(source => source.Contains(typeDeclaration, StringComparison.Ordinal));
    }

    [Test]
    public async Task GeneratedLeaf_ExposesParentRelativeOid()
    {
        var oid = LocalSystemData.LocChassisIdSubtype.Oid;

        await Assert.That(oid).IsEquivalentTo(new Oid("1.0.8802.1.1.2.1.3.1"));
        await Assert.That(LocalSystemData.LocChassisIdSubtype.InstanceOid)
            .IsEquivalentTo(new Oid("1.0.8802.1.1.2.1.3.1.0"));
    }

    [Test]
    public async Task GeneratedLeaf_ParsesExpectedAsnType()
    {
        var dict = new Dictionary<Oid, AsnType>
        {
            [LocalSystemData.LocChassisIdSubtype.InstanceOid] = new Integer32(4)
        };
        var value = LocalSystemData.LocChassisIdSubtype.Parse(dict);

        await Assert.That(value).IsEqualTo(new Integer32(4));
    }

    [Test]
    public async Task GeneratedTable_ExposesOidAndParser()
    {
        await Assert.That(IfRefTable.Oid)
            .IsEquivalentTo(new Oid("1.3.6.1.4.1.16177.2.4.1.1"));
        await Assert.That(IfRefTable.Parse(new Dictionary<Oid, AsnType>()))
            .IsEmpty();
    }

    [Test]
    public async Task GeneratedNotification_CanBeParsedFromTopLevel()
    {
        var linkUp = new LinkUp
        {
            IfAdminStatus = new Integer32(1),
            IfIndex = new Integer32(2),
            IfOperStatus = new Integer32(3)
        };
        var oids = linkUp.ToDictionary().AsReadOnly();
        var @new = global::Snmp.Iso.Id.ParseNotifications(oids).First();
        await Assert.That(@new).IsTypeOf<LinkUp>();
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

        var entries = IfRefTable.Parse(values);
        await Assert.That(entries).IsNotNull();
        await Assert.That(entries).HasSingleItem();
        await Assert.That(entries[0].IndexIndex).IsEqualTo(1u);
        await Assert.That(entries[0].ifName).IsEquivalentTo(new OctetString("eth0"));
        var roundTripped = new Dictionary<Oid, AsnType>();
        entries.Populate(roundTripped);
        await Assert.That(roundTripped.Count).IsEqualTo(values.Count);
        await Assert.That(roundTripped[new Oid("1.3.6.1.4.1.16177.2.4.1.1.1.3.1")])
            .IsEqualTo(new OctetString("eth0"));
    }

    [Test]
    public async Task GeneratedTableEntry_DeclaredAsObjectType_IsEmittedOnce()
    {
        var source = FindGeneratedSource(
            "public class DuplicateTable : List<DuplicateTableEntry>, ISnmpTable<DuplicateTable,DuplicateTableEntry>");

        await Assert.That(source.Split("public class DuplicateTableEntry").Length)
            .IsEqualTo(2);
    }

    [Test]
    public async Task GeneratedStandaloneLeaf_IncludesOidMarkerAndDescription()
    {
        var source = FindGeneratedSource("public class Dot1qVlanNumDeletes : ISnmpLeaf<Counter32>")
            .Replace("\t", string.Empty);

        await Assert.That(source.Contains("/// 1.3.6.1.2.1.17.7.1.4.1", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("/// The number of times a VLAN entry has been deleted from",
            StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("/// the dot1qVlanCurrentTable (for any reason).  If an entry",
            StringComparison.Ordinal)).IsTrue();
        await Assert
            .That(source.Contains("/// is deleted, then inserted, and then deleted, this", StringComparison.Ordinal))
            .IsTrue();
        await Assert.That(source.Contains("/// counter will be incremented by 2.", StringComparison.Ordinal)).IsTrue();
    }


    [Test]
    public async Task GeneratedNestedLeafClasses_IncludeEscapedMultilineDescriptions()
    {
        var source =
            FindGeneratedSource(
                    "public class CustomTable : List<CustomTableEntry>, ISnmpTable<CustomTable,CustomTableEntry>")
                .Replace("\t", string.Empty);

        await Assert.That(source.Contains("/// Index line 1", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("/// &lt;tag&gt; &amp; value", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("///", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("///     deeper", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("public class CustomIndex : ISnmpLeaf<Integer32>", StringComparison.Ordinal))
            .IsTrue();
        await Assert.That(source.Contains("/// Column line 1", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("public class CustomValue : ISnmpLeaf<Integer32>", StringComparison.Ordinal))
            .IsTrue();
    }
}