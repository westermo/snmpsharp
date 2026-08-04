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
    [MethodDataSource(typeof(ParseTestCase), nameof(ParseTestCase.AllCases))]
    public async Task SubParsersTest(ParseTestCase testCase)
    {
        testCase.Run();
    }
}