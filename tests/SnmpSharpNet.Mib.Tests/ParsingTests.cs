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
    [Test]
    //[Arguments("TEST.mib")]
    [Arguments("IANAifType-MIB.mib", "WESTERMO-OID-MIB.mib", "WESTERMO-INTERFACE-MIB.mib")]
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
        var module = MibParser.ParseModule(await File.ReadAllTextAsync("MAU-MIB.mib"), "mib", BuiltinMib.All);
        var table = module.Items.Values
            .OfType<MibTable>()
            .First(x => x.Ident.Name == "ifMauAutoNegTable");

        await Assert.That(table.Columns.Any(x => x.Ident.Oid[^1] == 4)).IsTrue();
        await Assert.That(table.Columns.Any(x => x.Ident.Oid[^1] == 3)).IsFalse();
    }

    [Test]
    [MethodDataSource(typeof(ParseTestCase), nameof(ParseTestCase.AllCases))]
    public async Task SubParsersTest(ParseTestCase testCase)
    {
        testCase.Run();
    }
}