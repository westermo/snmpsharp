using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Runtime.Loader;

namespace SnmpSharpNet.Mib.SourceGenerator.IntegrationTests;

public class SemanticValidationTests
{
    [Test]
    public async Task Validate_RejectsInvalidIndexSemantics()
    {
        var diagnostics = Validate("""
                                   TEST-MIB DEFINITIONS ::= BEGIN

                                   placementRoot OBJECT IDENTIFIER ::= { iso 3 }
                                   PlacementEntry ::= SEQUENCE { placementValue OCTET STRING, placementTail Integer32 }
                                   placementTable OBJECT-TYPE
                                       SYNTAX SEQUENCE OF PlacementEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { placementRoot 1 }
                                   placementEntry OBJECT-TYPE
                                       SYNTAX PlacementEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       INDEX { IMPLIED placementValue, placementTail }
                                       ::= { placementTable 1 }
                                   placementValue OBJECT-TYPE
                                       SYNTAX OCTET STRING
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { placementEntry 1 }
                                   placementTail OBJECT-TYPE
                                       SYNTAX Integer32
                                       MAX-ACCESS read-only
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { placementEntry 2 }

                                   fixedRoot OBJECT IDENTIFIER ::= { iso 4 }
                                   FixedEntry ::= SEQUENCE { fixedValue OCTET STRING }
                                   fixedTable OBJECT-TYPE
                                       SYNTAX SEQUENCE OF FixedEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { fixedRoot 1 }
                                   fixedEntry OBJECT-TYPE
                                       SYNTAX FixedEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       INDEX { IMPLIED fixedValue }
                                       ::= { fixedTable 1 }
                                   fixedValue OBJECT-TYPE
                                       SYNTAX OCTET STRING (SIZE (4))
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { fixedEntry 1 }

                                   counterRoot OBJECT IDENTIFIER ::= { iso 5 }
                                   CounterEntry ::= SEQUENCE { counter32 Counter32, counter64 Counter64 }
                                   counterTable OBJECT-TYPE
                                       SYNTAX SEQUENCE OF CounterEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { counterRoot 1 }
                                   counterEntry OBJECT-TYPE
                                       SYNTAX CounterEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       INDEX { counter32, counter64 }
                                       ::= { counterTable 1 }
                                   counter32 OBJECT-TYPE
                                       SYNTAX Counter32
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { counterEntry 1 }
                                   counter64 OBJECT-TYPE
                                       SYNTAX Counter64
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { counterEntry 2 }

                                   allIndexRoot OBJECT IDENTIFIER ::= { iso 6 }
                                   AllIndexEntry ::= SEQUENCE { allIndexValue Integer32 }
                                   allIndexTable OBJECT-TYPE
                                       SYNTAX SEQUENCE OF AllIndexEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { allIndexRoot 1 }
                                   allIndexEntry OBJECT-TYPE
                                       SYNTAX AllIndexEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       INDEX { allIndexValue }
                                       ::= { allIndexTable 1 }
                                   allIndexValue OBJECT-TYPE
                                       SYNTAX Integer32
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { allIndexEntry 1 }

                                   END
                                   """);

        var ids = diagnostics.Select(diagnostic => diagnostic.Id).ToHashSet();

        await Assert.That(ids.Contains("SNMP005")).IsTrue();
        await Assert.That(ids.Contains("SNMP006")).IsTrue();
        await Assert.That(diagnostics.Count(diagnostic => diagnostic.Id == "SNMP007")).IsEqualTo(2);
        await Assert.That(ids.Contains("SNMP012")).IsTrue();
    }

    [Test]
    public async Task Validate_RejectsInvalidTableAndRowAccess()
    {
        var diagnostics = Validate("""
                                   TEST-MIB DEFINITIONS ::= BEGIN

                                   testRoot OBJECT IDENTIFIER ::= { iso 3 }
                                   TestEntry ::= SEQUENCE { testIndex Integer32, readCreate Integer32, readWrite Integer32 }
                                   testTable OBJECT-TYPE
                                       SYNTAX SEQUENCE OF TestEntry
                                       MAX-ACCESS read-only
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { testRoot 1 }
                                   testEntry OBJECT-TYPE
                                       SYNTAX TestEntry
                                       MAX-ACCESS read-only
                                       STATUS current
                                       DESCRIPTION "test"
                                       INDEX { testIndex }
                                       ::= { testTable 1 }
                                   testIndex OBJECT-TYPE
                                       SYNTAX Integer32
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { testEntry 1 }
                                   readCreate OBJECT-TYPE
                                       SYNTAX Integer32
                                       MAX-ACCESS read-create
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { testEntry 2 }
                                   readWrite OBJECT-TYPE
                                       SYNTAX Integer32
                                       MAX-ACCESS read-write
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { testEntry 3 }

                                   AugmentEntry ::= SEQUENCE { augmentCreate Integer32, augmentWrite Integer32 }
                                   augmentTable OBJECT-TYPE
                                       SYNTAX SEQUENCE OF AugmentEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { testRoot 2 }
                                   augmentEntry OBJECT-TYPE
                                       SYNTAX AugmentEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       AUGMENTS { testEntry }
                                       ::= { augmentTable 1 }
                                   augmentCreate OBJECT-TYPE
                                       SYNTAX Integer32
                                       MAX-ACCESS read-create
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { augmentEntry 1 }
                                   augmentWrite OBJECT-TYPE
                                       SYNTAX Integer32
                                       MAX-ACCESS read-write
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { augmentEntry 2 }

                                   END
                                   """);

        var ids = diagnostics.Select(diagnostic => diagnostic.Id).ToHashSet();

        await Assert.That(diagnostics.Count(diagnostic => diagnostic.Id == "SNMP008")).IsEqualTo(2);
        await Assert.That(diagnostics.Count(diagnostic => diagnostic.Id == "SNMP010")).IsEqualTo(2);
    }

    [Test]
    public async Task Validate_RejectsAccessibleAuxiliaryIndexesAndNotificationObjects()
    {
        var diagnostics = Validate("""
                                   TEST-MIB DEFINITIONS ::= BEGIN

                                   testRoot OBJECT IDENTIFIER ::= { iso 3 }
                                   TestEntry ::= SEQUENCE { testIndex Integer32, testValue Integer32 }
                                   testTable OBJECT-TYPE
                                       SYNTAX SEQUENCE OF TestEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { testRoot 1 }
                                   testEntry OBJECT-TYPE
                                       SYNTAX TestEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       INDEX { testIndex }
                                       ::= { testTable 1 }
                                       testIndex OBJECT-TYPE
                                       SYNTAX Integer32
                                       MAX-ACCESS read-only
                                       STATUS current
                                       DESCRIPTION "test"
                                           ::= { testEntry 1 }
                                   testValue OBJECT-TYPE
                                       SYNTAX Integer32
                                       MAX-ACCESS read-only
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { testEntry 2 }
                                   notificationObject OBJECT-TYPE
                                       SYNTAX Integer32
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { testRoot 2 }
                                   testNotification NOTIFICATION-TYPE
                                       OBJECTS { notificationObject }
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { testRoot 3 }

                                   END
                                   """);

        var ids = diagnostics.Select(diagnostic => diagnostic.Id).ToHashSet();

        await Assert.That(ids.Contains("SNMP009")).IsTrue();
        await Assert.That(ids.Contains("SNMP011")).IsTrue();
    }

    [Test]
    public async Task Validate_RejectsInvalidOpaqueAndImpliedVariableLengthIndexes()
    {
        var diagnostics = Validate("""
                                   TEST-MIB DEFINITIONS ::= BEGIN

                                   indexRoot OBJECT IDENTIFIER ::= { iso 3 }

                                   OpaqueEntry ::= SEQUENCE { opaqueIndex Opaque }
                                   opaqueTable OBJECT-TYPE
                                       SYNTAX SEQUENCE OF OpaqueEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { indexRoot 1 }
                                   opaqueEntry OBJECT-TYPE
                                       SYNTAX OpaqueEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       INDEX { opaqueIndex }
                                       ::= { opaqueTable 1 }
                                   opaqueIndex OBJECT-TYPE
                                       SYNTAX Opaque
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { opaqueEntry 1 }

                                   EmptyEntry ::= SEQUENCE { emptyIndex OCTET STRING }
                                   emptyTable OBJECT-TYPE
                                       SYNTAX SEQUENCE OF EmptyEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { indexRoot 2 }
                                   emptyEntry OBJECT-TYPE
                                       SYNTAX EmptyEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       INDEX { IMPLIED emptyIndex }
                                       ::= { emptyTable 1 }
                                   emptyIndex OBJECT-TYPE
                                       SYNTAX OCTET STRING (SIZE (0..4))
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { emptyEntry 1 }

                                   BitsEntry ::= SEQUENCE { bitsIndex BITS { enabled(0) } }
                                   bitsTable OBJECT-TYPE
                                       SYNTAX SEQUENCE OF BitsEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { indexRoot 3 }
                                   bitsEntry OBJECT-TYPE
                                       SYNTAX BitsEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       INDEX { IMPLIED bitsIndex }
                                       ::= { bitsTable 1 }
                                   bitsIndex OBJECT-TYPE
                                       SYNTAX BITS { enabled(0) }
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { bitsEntry 1 }

                                   ValidBitsEntry ::= SEQUENCE { validBitsIndex BITS { enabled(0) } }
                                   validBitsTable OBJECT-TYPE
                                       SYNTAX SEQUENCE OF ValidBitsEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { indexRoot 4 }
                                   validBitsEntry OBJECT-TYPE
                                       SYNTAX ValidBitsEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       INDEX { validBitsIndex }
                                       ::= { validBitsTable 1 }
                                   validBitsIndex OBJECT-TYPE
                                       SYNTAX BITS { enabled(0) }
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { validBitsEntry 1 }

                                   NonEmptyEntry ::= SEQUENCE { nonEmptyIndex OCTET STRING }
                                   nonEmptyTable OBJECT-TYPE
                                       SYNTAX SEQUENCE OF NonEmptyEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { indexRoot 5 }
                                   nonEmptyEntry OBJECT-TYPE
                                       SYNTAX NonEmptyEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       INDEX { IMPLIED nonEmptyIndex }
                                       ::= { nonEmptyTable 1 }
                                   nonEmptyIndex OBJECT-TYPE
                                       SYNTAX OCTET STRING (SIZE (1..4))
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { nonEmptyEntry 1 }

                                   END
                                   """);

        await Assert.That(diagnostics.Count(diagnostic => diagnostic.Id == "SNMP013")).IsEqualTo(1);
        await Assert.That(diagnostics.Count(diagnostic => diagnostic.Id == "SNMP014")).IsEqualTo(2);
    }

    [Test]
    public async Task Validate_RequiresNewNotificationsToUseAZeroPenultimateArc()
    {
        var diagnostics = Validate("""
                                   TEST-MIB DEFINITIONS ::= BEGIN

                                   notificationRoot OBJECT IDENTIFIER ::= { iso 3 }
                                   notificationPrefix OBJECT IDENTIFIER ::= { notificationRoot 0 }
                                   validNotification NOTIFICATION-TYPE
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { notificationPrefix 1 }
                                   invalidNotification NOTIFICATION-TYPE
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { notificationRoot 2 }
                                   invalidNumericNotification NOTIFICATION-TYPE
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { 1 3 6 1 2 }

                                   END
                                   """);

        await Assert.That(diagnostics.Count(diagnostic => diagnostic.Id == "SNMP015")).IsEqualTo(2);
    }

    [Test]
    public async Task Validate_RejectsInvalidConceptualTableDirectStructure()
    {
        var diagnostics = Validate("""
                                   TEST-MIB DEFINITIONS ::= BEGIN

                                   tableRoot OBJECT IDENTIFIER ::= { iso 3 }

                                   WrongEntry ::= SEQUENCE { wrongColumn Integer32 }
                                   wrongTable OBJECT-TYPE
                                       SYNTAX SEQUENCE OF WrongEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { tableRoot 1 }
                                   wrongEntry OBJECT-TYPE
                                       SYNTAX WrongEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       INDEX { wrongColumn }
                                       ::= { wrongTable 2 }
                                   wrongColumn OBJECT-TYPE
                                       SYNTAX Integer32
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { wrongEntry 1 }

                                   ConflictEntry ::= SEQUENCE {
                                       zeroColumn Integer32,
                                       duplicateColumn Integer32,
                                       duplicateColumnAgain Integer32
                                   }
                                   conflictTable OBJECT-TYPE
                                       SYNTAX SEQUENCE OF ConflictEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { tableRoot 2 }
                                   conflictEntry OBJECT-TYPE
                                       SYNTAX ConflictEntry
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       INDEX { duplicateColumn }
                                       ::= { conflictTable 1 }
                                   conflictSibling OBJECT IDENTIFIER ::= { conflictTable 2 }
                                   zeroColumn OBJECT-TYPE
                                       SYNTAX Integer32
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { conflictEntry 0 }
                                   duplicateColumn OBJECT-TYPE
                                       SYNTAX Integer32
                                       MAX-ACCESS not-accessible
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { conflictEntry 1 }
                                   duplicateColumnAgain OBJECT-TYPE
                                       SYNTAX Integer32
                                       MAX-ACCESS read-only
                                       STATUS current
                                       DESCRIPTION "test"
                                       ::= { conflictEntry 1 }

                                   END
                                   """);

        var ids = diagnostics.Select(diagnostic => diagnostic.Id).ToHashSet();

        await Assert.That(ids.Contains("SNMP016")).IsTrue();
        await Assert.That(ids.Contains("SNMP017")).IsTrue();
        await Assert.That(ids.Contains("SNMP018")).IsTrue();
        await Assert.That(ids.Contains("SNMP019")).IsTrue();
    }

    [Test]
    public async Task Validate_ReportsInvalidNumericOidArcsAsParserErrors()
    {
        var diagnostics = Validate("""
                                   TEST-MIB DEFINITIONS ::= BEGIN
                                   negativeArc OBJECT IDENTIFIER ::= { iso -1 }
                                   tooLargeArc OBJECT IDENTIFIER ::= { iso 4294967296 }
                                   END
                                   """);

        await Assert.That(diagnostics.Count(diagnostic => diagnostic.Id == "SNMP001")).IsEqualTo(1);
    }

    private static IReadOnlyList<Diagnostic> Validate(string source)
    {
        AssemblyLoadContext.Default.LoadFromAssemblyPath(
            Path.Combine(AppContext.BaseDirectory, "SnmpSharpNet.Mib.dll"));
        var compilation = CSharpCompilation.Create(
            "SemanticValidation",
            [CSharpSyntaxTree.ParseText("public class Test { }")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new SnmpGenerator().AsSourceGenerator()],
            [new InMemoryAdditionalText("TEST-MIB.mib", source)]);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        return
        [
            .. driver.GetRunResult().Results.Single().Diagnostics,
            .. outputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Id.StartsWith("SNMP", StringComparison.Ordinal))
        ];
    }

    private sealed class InMemoryAdditionalText(string path, string source) : AdditionalText
    {
        public override string Path => path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(source);
    }
}