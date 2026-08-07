using Parlot;
using Parlot.Fluent;
using SnmpSharpNet.Mib.Ast;
using System.Linq;

namespace SnmpSharpNet.Mib.Tests;

public abstract class ParseTestCase(string Name)
{
    public static IEnumerable<ParseTestCase> AllCases()
    {
        yield return EmptyModule;
        yield return TableEntryDef;
        yield return TextualConvention;
        yield return ObjectIdentifierAssignment;
        yield return ModuleIdentityAssignment;
        yield return ConceptualTableAssignment;
        yield return ConceptualRowAssignment;
        yield return LeafObjectAssignment;
        yield return NotificationTypeAssignment;
        yield return IgnoredObjectGroup;
        yield return IgnoredModuleCompliance;
    }

    public static readonly ParseTestCase EmptyModule = new ParseTestCase<ModuleDefinition>(
        nameof(EmptyModule),
        ModuleDefinition.EntryPoint,
        "WESTERMO-INTERFACE-MIB DEFINITIONS ::= BEGIN END$"
    );

    public static readonly ParseTestCase TableEntryDef = new ParseTestCase<EntryDef>(
        nameof(TableEntryDef),
        EntryDef.Parser,
        """
        IfRefEntry ::= SEQUENCE {
            ifRefIndex      IfaceRefIndex,
            ifRefifIndex    InterfaceIndex,
            ifRefifName     DisplayString,
            ifRefifDescr    DisplayString,
            ifRefifType     IANAifType
        }
        $
        """
    );

    public static readonly ParseTestCase TextualConvention = new ParseTestCase<ModuleItem>(
        nameof(TextualConvention),
        ModuleItem.Parser,
        """
        IfaceRefIndex ::= TEXTUAL-CONVENTION
            DISPLAY-HINT "d"
            STATUS       current
            DESCRIPTION
                    "A unique value, greater than zero, for each interface."
            SYNTAX       Integer32 (1..1000)
        $
        """
    );

    public static readonly ParseTestCase ObjectIdentifierAssignment = new ParseTestCase<ModuleItem>(
        nameof(ObjectIdentifierAssignment),
        ModuleItem.Parser,
        """
        wmoInterfaceObjects OBJECT IDENTIFIER ::= { wmoInterface 1 }
        $
        """
    );

    public static readonly ParseTestCase ModuleIdentityAssignment = new ParseTestCase<ModuleItem>(
        nameof(ModuleIdentityAssignment),
        ModuleItem.Parser,
        """
        wmoInterface MODULE-IDENTITY
            LAST-UPDATED "201908300000Z"
            ORGANIZATION "Westermo"
            CONTACT-INFO "Contact info here"
            DESCRIPTION "Description here"
            REVISION "201908300000Z"
            DESCRIPTION "Initial version."
        ::= { common 4 }
        $
        """
    );

    public static readonly ParseTestCase ConceptualTableAssignment = new ParseTestCase<ModuleItem>(
        nameof(ConceptualTableAssignment),
        ModuleItem.Parser,
        """
        ifRefTable OBJECT-TYPE
            SYNTAX      SEQUENCE OF IfRefEntry
            MAX-ACCESS  not-accessible
            STATUS      current
            DESCRIPTION
                    "A list of interface entries."
            ::= { wmoInterfaceObjects 1 }
        $
        """
    );

    public static readonly ParseTestCase ConceptualRowAssignment = new ParseTestCase<ModuleItem>(
        nameof(ConceptualRowAssignment),
        ModuleItem.Parser,
        """
        ifRefEntry OBJECT-TYPE
            SYNTAX      IfRefEntry
            MAX-ACCESS  not-accessible
            STATUS      current
            DESCRIPTION
                    "Interface entry"
            INDEX   { ifRefIndex }
            ::= { ifRefTable 1 }
        $
        """
    );

    public static readonly ParseTestCase LeafObjectAssignment = new ParseTestCase<ModuleItem>(
        nameof(LeafObjectAssignment),
        ModuleItem.Parser,
        """
        ifRefIndex OBJECT-TYPE
            SYNTAX      Integer32
            MAX-ACCESS  not-accessible
            STATUS      current
            DESCRIPTION
            "A unique value, greater than zero, for each interface."
            ::= { ifRefEntry 1 }
        $
        """
    );

    public static readonly ParseTestCase NotificationTypeAssignment = new ParseTestCase<ModuleItem>(
        nameof(NotificationTypeAssignment),
        ModuleItem.Parser,
        """
        linkDown NOTIFICATION-TYPE
            OBJECTS { ifIndex, ifAdminStatus, ifOperStatus }
            STATUS  current
            DESCRIPTION
                    "A linkDown trap."
            ::= { snmpTraps 3 }
        $
        """
    );

    public static readonly ParseTestCase IgnoredObjectGroup = new ParseTestCase<ModuleItem>(
        nameof(IgnoredObjectGroup),
        ModuleItem.Parser,
        """
        wmoInterfaceGroup OBJECT-GROUP
            OBJECTS {
            ifRefifIndex,
            ifRefifName,
            ifRefifDescr,
            ifRefifType
            }
            STATUS  current
            DESCRIPTION
            "The wmoInterfaceGroup"
            ::= { wmoInterfaceGroups 1 }
        $
        """
    );

    public static readonly ParseTestCase IgnoredModuleCompliance = new ParseTestCase<ModuleItem>(
        nameof(IgnoredModuleCompliance),
        ModuleItem.Parser,
        """
        wmoInterfaceCompliance MODULE-COMPLIANCE
            STATUS  current
            DESCRIPTION
            "The compliance statement"
            MODULE
            MANDATORY-GROUPS {
            wmoInterfaceGroup
            }
            ::= { wmoInterfaceCompliances 1 }
        $
        """
    );

    public abstract void Run();

    public override string ToString() => Name;
}

class ParseTestCase<T>(string Name, Parser<T> Parser, string ToParse) : ParseTestCase(Name)
{
    public override void Run()
    {
        var expectedEnd = ToParse.IndexOf('$');
        if (expectedEnd < 0)
        {
            throw new Exception("Test case ToParse must contain a '$' sentinel");
        }

        var context = new ParseContext(new Scanner(ToParse))
        {
            WhiteSpaceParser = SMIv2.MibWhiteSpace
        };
        var success = Parser.TryParse(context, out _, out var error);
        if (!success)
        {
            throw new Exception($"Parse failed: {error?.Message}");
        }

        context.SkipWhiteSpace();
        if (context.Scanner.Cursor.Position.Offset != expectedEnd)
        {
            throw new Exception(
                $"Parser stopped at offset {context.Scanner.Cursor.Position.Offset}, expected {expectedEnd}");
        }
    }
}

public class ParsingTests
{
    private static Dictionary<string, Ast.ModuleDefinition> LoadAllAsts()
    {
        var asts = new Dictionary<string, Ast.ModuleDefinition>();
        foreach (var mibFile in Directory.GetFiles(AppContext.BaseDirectory, "*.mib"))
        {
            var contents = File.ReadAllText(mibFile);
            var ast = MibParser.ParseAst(contents, Path.GetFileName(mibFile));
            asts[ast.Identifier.ToString()] = ast;
        }

        return asts;
    }

    [Test]
    //[Arguments("TEST.mib")]
    [Arguments("IANAifType-MIB.mib", "IF-MIB.mib", "WESTERMO-OID-MIB.mib", "WESTERMO-INTERFACE-MIB.mib")]
    [Arguments("SNMP-FRAMEWORK-MIB.mib", "IANA-ADDRESS-FAMILY-NUMBERS-MIB.mib", "LLDP-MIB.mib")]
    public async Task CanParseMibs(params string[] mibs)
    {
        var importCache = new Dictionary<string, IMibThatExports>(BuiltinMib.All);

        foreach (var mib in mibs)
        {
            var module = MibParser.ParseModule(await File.ReadAllTextAsync(mib), "mib", importCache);
            importCache.Add(module.Identifier, module);
            Console.WriteLine("\n-----------------");
            Console.WriteLine($"-- {module.Identifier}");
            Console.WriteLine("-----------------");
            Console.WriteLine(module);
        }
    }

    [Test]
    public async Task TableColumns_CanBeSparse()
    {
        var importCache = new Dictionary<string, IMibThatExports>(BuiltinMib.All);
        foreach (var dep in new[] { "IANAifType-MIB.mib", "IF-MIB.mib" })
        {
            var depModule = MibParser.ParseModule(await File.ReadAllTextAsync(dep), dep, importCache);
            importCache.Add(depModule.Identifier, depModule);
        }

        var module = MibParser.ParseModule(await File.ReadAllTextAsync("MAU-MIB.mib"), "mib", importCache);
        var table = module.Items.Values
            .OfType<MibTable>()
            .First(x => x.Ident.Name == "ifMauAutoNegTable");

        await Assert.That(table.Columns.Any(x => x.Ident.Oid[^1] == 4)).IsTrue();
        await Assert.That(table.Columns.Any(x => x.Ident.Oid[^1] == 3)).IsFalse();
    }

    [Test]
    public async Task QBridgeObjectTypeDescriptions_ArePreservedInAst()
    {
        var asts = LoadAllAsts();
        var module = asts["Q-BRIDGE-MIB"];

        var table = module.Items.OfType<ConceptualTable>()
            .Single(x => x.Name.ToString() == "dot1qVlanStaticTable");
        var row = module.Items.OfType<ConceptualRow>()
            .Single(x => x.Name.ToString() == "dot1qVlanStaticEntry");
        var augmentingRow = module.Items.OfType<AugmentingConceptualRow>()
            .Single(x => x.Name.ToString() == "dot1qPortVlanEntry");
        var leaf = module.Items.OfType<LeafObject>()
            .Single(x => x.Name.ToString() == "dot1qVlanStaticName");

        await Assert.That(table.Description?.ToString()).IsEqualTo("""
A table containing static configuration information for
        each VLAN configured into the device by (local or
        network) management.  All entries are permanent and will
        be restored after the device is reset.
""");
        await Assert.That(row.Description?.ToString()).IsEqualTo("""
Static information for a VLAN configured into the
        device by (local or network) management.
""");
        await Assert.That(augmentingRow.Description?.ToString()).IsEqualTo("""
Information controlling VLAN configuration for a port
        on the device.  This is indexed by dot1dBasePort.
""");
        await Assert.That(leaf.Description?.ToString()).IsEqualTo("""
An administratively assigned string, which may be used
        to identify the VLAN.
""");
    }

    [Test]
    public async Task NumericRootOidAssignment_IsResolved()
    {
        var module = MibParser.ParseModule("""
            TEST-MIB DEFINITIONS ::= BEGIN
            zeroRoot OBJECT IDENTIFIER ::= { 0 0 }
            END
            """);

        await Assert.That(module.AllOids["zeroRoot"].Oid).IsEquivalentTo(new uint[] { 0, 0 });
    }

    [Test]
    public async Task NumericRootOidAssignment_WithMultipleArcsIsResolved()
    {
        var module = MibParser.ParseModule("""
            TEST-MIB DEFINITIONS ::= BEGIN
            numericRoot OBJECT IDENTIFIER ::= { 1 3 6 1 }
            END
            """);

        await Assert.That(module.AllOids["numericRoot"].Oid).IsEquivalentTo(new uint[] { 1, 3, 6, 1 });
    }

    [Test]
    public async Task NumericOidAssignment_WithOutOfRangeArcIsRejected()
    {
        await Assert.That(() => MibParser.ParseModule("""
                TEST-MIB DEFINITIONS ::= BEGIN
                negativeArc OBJECT IDENTIFIER ::= { iso -1 }
                END
                """))
            .Throws<ArgumentOutOfRangeException>();

        await Assert.That(() => MibParser.ParseModule("""
                TEST-MIB DEFINITIONS ::= BEGIN
                tooLargeArc OBJECT IDENTIFIER ::= { iso 4294967296 }
                END
                """))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task QBridgeDescriptions_ArePropagatedToResolvedItems()
    {
        var modules = MibParser.ParseModules(LoadAllAsts());
        var module = modules["Q-BRIDGE-MIB"];

        var standaloneLeaf = module.Items.Values.OfType<MibLeaf>()
            .Single(x => x.Ident.Name == "dot1qVlanNumDeletes");
        var table = module.Items.Values.OfType<MibTable>()
            .Single(x => x.Ident.Name == "dot1qVlanStaticTable");
        var column = module.AllItems.Values.OfType<MibLeaf>()
            .Single(x => x.Ident.Name == "dot1qVlanStaticName");

        await Assert.That(standaloneLeaf.Description).IsEqualTo("""
The number of times a VLAN entry has been deleted from
        the dot1qVlanCurrentTable (for any reason).  If an entry
        is deleted, then inserted, and then deleted, this
        counter will be incremented by 2.
""");
        await Assert.That(table.Description).IsEqualTo("""
A table containing static configuration information for
        each VLAN configured into the device by (local or
        network) management.  All entries are permanent and will
        be restored after the device is reset.
""");
        await Assert.That(table.EntryDescription).IsEqualTo("""
Static information for a VLAN configured into the
        device by (local or network) management.
""");
        await Assert.That(column.Description).IsEqualTo("""
An administratively assigned string, which may be used
        to identify the VLAN.
""");
    }

    [Test]
    public async Task IfMibNotifications_ArePropagatedToResolvedItems()
    {
        var modules = MibParser.ParseModules(LoadAllAsts());
        var module = modules["IF-MIB"];

        var notification = module.Items.Values.OfType<MibNotification>()
            .Single(x => x.Ident.Name == "linkDown");

        await Assert.That(string.Join(",", notification.Objects.Select(x => x.Ident.Name)))
            .IsEqualTo("ifIndex,ifAdminStatus,ifOperStatus");
        await Assert.That(notification.Description?.StartsWith(
            "A linkDown trap signifies that the SNMP entity, acting in",
            StringComparison.Ordinal)).IsTrue();
        await Assert.That(notification.Description?.Contains(
            "of ifOperStatus.",
            StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task PresentationAndDefaults_ArePreservedAndValidated()
    {
        var module = MibParser.ParseModule("""
            TEST-MIB DEFINITIONS ::= BEGIN

            testRoot OBJECT IDENTIFIER ::= { iso 3 }

            TestDisplay ::= TEXTUAL-CONVENTION
                DISPLAY-HINT "d-2"
                STATUS current
                DESCRIPTION "A displayable test value."
                REFERENCE "RFC test"
                SYNTAX Integer32 (0..10000)

            testValue OBJECT-TYPE
                SYNTAX TestDisplay
                UNITS "centiwidgets"
                MAX-ACCESS read-write
                STATUS deprecated
                DESCRIPTION "A configurable test value."
                REFERENCE "Section 1"
                DEFVAL { 250 }
                ::= { testRoot 1 }

            testBits OBJECT-TYPE
                SYNTAX BITS { enabled(0), alarm(9) }
                MAX-ACCESS read-only
                STATUS current
                DESCRIPTION "A bit set."
                DEFVAL { { enabled, alarm } }
                ::= { testRoot 2 }

            testOid OBJECT-TYPE
                SYNTAX OBJECT IDENTIFIER
                MAX-ACCESS read-only
                STATUS current
                DESCRIPTION "An object identifier."
                DEFVAL { { iso(1) 3 6 } }
                ::= { testRoot 3 }

            END
            """);

        var value = module.Items.Values.OfType<MibLeaf>()
            .Single(item => item.Ident.Name == "testValue");
        var bits = module.Items.Values.OfType<MibLeaf>()
            .Single(item => item.Ident.Name == "testBits");
        var oid = module.Items.Values.OfType<MibLeaf>()
            .Single(item => item.Ident.Name == "testOid");

        await Assert.That(value.Units).IsEqualTo("centiwidgets");
        await Assert.That(value.Description).IsEqualTo("A configurable test value.");
        await Assert.That(value.Reference).IsEqualTo("Section 1");
        await Assert.That(value.Accessibility).IsEqualTo(SMIv2Accessibility.ReadWrite);
        await Assert.That(value.Status).IsEqualTo(SMIv2Status.Deprecated);
        await Assert.That(value.Type.TextualConvention).IsNotNull();
        await Assert.That(value.Type.TextualConvention!.Name).IsEqualTo("TestDisplay");
        await Assert.That(value.Type.TextualConvention.DisplayHint).IsEqualTo("d-2");
        await Assert.That(value.Type.TextualConvention.Description).IsEqualTo("A displayable test value.");
        await Assert.That(value.Type.TextualConvention.Reference).IsEqualTo("RFC test");
        await Assert.That(value.Type.TextualConvention.Status).IsEqualTo(SMIv2Status.Current);
        await Assert.That(value.DefaultValue!.Kind).IsEqualTo(MibDefaultValueKind.Number);
        await Assert.That(value.DefaultValue.Number).IsEqualTo(250);
        await Assert.That(bits.DefaultValue!.Kind).IsEqualTo(MibDefaultValueKind.Bits);
        await Assert.That(bits.DefaultValue.Names).IsEquivalentTo(["enabled", "alarm"]);
        await Assert.That(oid.DefaultValue!.Kind).IsEqualTo(MibDefaultValueKind.ObjectIdentifier);
        await Assert.That(oid.DefaultValue.ObjectIdentifier).IsEquivalentTo([1u, 3u, 6u]);
    }

    [Test]
    public async Task PresentationAndDefaults_AreStructuredInAst()
    {
        var ast = MibParser.ParseAst("""
            TEST-MIB DEFINITIONS ::= BEGIN
            testRoot OBJECT IDENTIFIER ::= { iso 3 }
            TestDisplay ::= TEXTUAL-CONVENTION
                DISPLAY-HINT "1x:"
                STATUS current
                DESCRIPTION "Display description"
                REFERENCE "Display reference"
                SYNTAX OCTET STRING (SIZE (6))
            testValue OBJECT-TYPE
                SYNTAX TestDisplay
                UNITS "octets"
                MAX-ACCESS read-write
                STATUS deprecated
                DESCRIPTION "Object description"
                REFERENCE "Object reference"
                DEFVAL { '001122334455'H }
                ::= { testRoot 1 }
            END
            """);

        var convention = ast.Items.OfType<TextualConvention>().Single();
        var leaf = ast.Items.OfType<LeafObject>().Single();

        await Assert.That(convention.Metadata.DisplayHint?.ToString()).IsEqualTo("1x:");
        await Assert.That(convention.Metadata.Description.ToString()).IsEqualTo("Display description");
        await Assert.That(convention.Metadata.Reference?.ToString()).IsEqualTo("Display reference");
        await Assert.That(leaf.Metadata.Units?.ToString()).IsEqualTo("octets");
        await Assert.That(leaf.Metadata.Accessibility).IsEqualTo(SMIv2Accessibility.ReadWrite);
        await Assert.That(leaf.Metadata.Status).IsEqualTo(SMIv2Status.Deprecated);
        await Assert.That(leaf.Metadata.Reference?.ToString()).IsEqualTo("Object reference");
        await Assert.That(leaf.DefaultValue!.Kind).IsEqualTo(AstDefaultValueKind.HexString);
        await Assert.That(leaf.DefaultValue.Text?.ToString()).IsEqualTo("001122334455");
    }

    [Test]
    public async Task Defaults_RejectInvalidTypesAndRanges()
    {
        await Assert.That(() => MibParser.ParseModule("""
                TEST-MIB DEFINITIONS ::= BEGIN
                testRoot OBJECT IDENTIFIER ::= { iso 3 }
                outOfRange OBJECT-TYPE
                    SYNTAX Integer32 (0..1)
                    MAX-ACCESS read-only
                    STATUS current
                    DESCRIPTION "test"
                    DEFVAL { 2 }
                    ::= { testRoot 1 }
                END
                """))
            .Throws<Exception>();

        await Assert.That(() => MibParser.ParseModule("""
                TEST-MIB DEFINITIONS ::= BEGIN
                testRoot OBJECT IDENTIFIER ::= { iso 3 }
                counterDefault OBJECT-TYPE
                    SYNTAX Counter32
                    MAX-ACCESS read-only
                    STATUS current
                    DESCRIPTION "test"
                    DEFVAL { 1 }
                    ::= { testRoot 1 }
                END
                """))
            .Throws<Exception>();

        await Assert.That(() => MibParser.ParseModule("""
                TEST-MIB DEFINITIONS ::= BEGIN
                testRoot OBJECT IDENTIFIER ::= { iso 3 }
                counterDefault OBJECT-TYPE
                    SYNTAX Counter64
                    MAX-ACCESS read-only
                    STATUS current
                    DESCRIPTION "test"
                    DEFVAL { 1 }
                    ::= { testRoot 1 }
                END
                """))
            .Throws<Exception>();
    }

    [Test]
    public async Task ObjectIdentifierDefault_RejectsUnknownNamedComponent()
    {
        await Assert.That(() => MibParser.ParseModule("""
                TEST-MIB DEFINITIONS ::= BEGIN
                testRoot OBJECT IDENTIFIER ::= { iso 3 }
                testOid OBJECT-TYPE
                    SYNTAX OBJECT IDENTIFIER
                    MAX-ACCESS read-only
                    STATUS current
                    DESCRIPTION "test"
                    DEFVAL { { iso(1) unknown(3) 6 } }
                    ::= { testRoot 1 }
                END
                """))
            .Throws<Exception>();
    }

    [Test]
    [MethodDataSource(typeof(ParseTestCase), nameof(ParseTestCase.AllCases))]
    public async Task SubParsersTest(ParseTestCase testCase)
    {
        testCase.Run();
    }
}