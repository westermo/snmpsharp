using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Runtime.Loader;

namespace SnmpSharpNet.Mib.SourceGenerator.IntegrationTests;

public class BuiltinModuleGenerationTests
{
    private const string EnterpriseMib = """
                                         TEST-MIB DEFINITIONS ::= BEGIN
                                         IMPORTS
                                             MODULE-IDENTITY, OBJECT-TYPE, Integer32, enterprises
                                                 FROM SNMPv2-SMI
                                             DisplayString
                                                 FROM SNMPv2-TC;

                                         testMib MODULE-IDENTITY
                                             LAST-UPDATED "202401010000Z"
                                             ORGANIZATION "Test"
                                             CONTACT-INFO "Test"
                                             DESCRIPTION "Test"
                                             ::= { enterprises 99999 }

                                         testName OBJECT-TYPE
                                             SYNTAX DisplayString
                                             MAX-ACCESS read-only
                                             STATUS current
                                             DESCRIPTION "test"
                                             ::= { testMib 1 }

                                         testCount OBJECT-TYPE
                                             SYNTAX Integer32
                                             MAX-ACCESS read-only
                                             STATUS current
                                             DESCRIPTION "test"
                                             ::= { testMib 2 }
                                         END
                                         """;

    [Test]
    public async Task ImportedBuiltinModule_EmitsItsNodes()
    {
        var (diagnostics, sources) = Run(("TEST-MIB.mib", EnterpriseMib));

        await Assert.That(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(HasIdClass(sources, "Snmp.Iso.Org.Dod.Internet.Private.Enterprises")).IsTrue();
        await Assert.That(HasIdClass(sources, "Snmp.Iso.Org.Dod.Internet.Mgmt.Mib2.Transmission")).IsTrue();
        await Assert.That(HasIdClass(sources, "Snmp.Iso.Org.Dod.Internet.SnmpV2.SnmpDomains")).IsTrue();
        await Assert.That(HasIdClass(sources, "Snmp.Itu.ZeroDotZero")).IsTrue();
        await Assert.That(sources.Single(s => s.Contains("namespace Snmp.Itu.ZeroDotZero\n", StringComparison.Ordinal)))
            .Contains("0.0");
    }

    [Test]
    public async Task BuiltinModulesThatAreNotImported_AreNotEmitted()
    {
        var (diagnostics, sources) = Run(("TEST-MIB.mib", EnterpriseMib));

        await Assert.That(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(sources.Any(s => s.Contains(".Mib2.Rmon", StringComparison.Ordinal))).IsFalse();
        await Assert.That(sources.Any(s => s.Contains(".SnmpModules.SnmpMIB", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task ImportedBuiltinModule_NamesArcsAboveUserDefinitions()
    {
        var (diagnostics, sources) = Run(("TEST-MIB.mib", """
                                                         TEST-MIB DEFINITIONS ::= BEGIN
                                                         IMPORTS
                                                             OBJECT-TYPE, Integer32 FROM SNMPv2-SMI
                                                             history FROM RMON2-MIB;

                                                         myHistory OBJECT IDENTIFIER ::= { history 99 }

                                                         testValue OBJECT-TYPE
                                                             SYNTAX Integer32
                                                             MAX-ACCESS read-only
                                                             STATUS current
                                                             DESCRIPTION "test"
                                                             ::= { myHistory 1 }
                                                         END
                                                         """));

        await Assert.That(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(HasIdClass(sources, "Snmp.Iso.Org.Dod.Internet.Mgmt.Mib2.Rmon")).IsTrue();
        await Assert.That(HasIdClass(sources, "Snmp.Iso.Org.Dod.Internet.Mgmt.Mib2.Rmon.History.MyHistory"))
            .IsTrue();
        await Assert.That(HasIdClass(sources, "Snmp.Iso.Org.Dod.Internet.Mgmt.Mib2.Rmon.Alarm")).IsTrue();
    }

    [Test]
    public async Task ImportedBuiltinModule_IncludesBuiltinDependenciesTransitively()
    {
        var (diagnostics, sources) = Run(("TEST-MIB.mib", """
                                                         TEST-MIB DEFINITIONS ::= BEGIN
                                                         IMPORTS
                                                             snmpMIB FROM SNMPv2-MIB;

                                                         myNode OBJECT IDENTIFIER ::= { snmpMIB 99 }
                                                         END
                                                         """));

        await Assert.That(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(HasIdClass(sources, "Snmp.Iso.Org.Dod.Internet.SnmpV2.SnmpModules.SnmpMIB")).IsTrue();
        await Assert.That(HasIdClass(sources, "Snmp.Iso.Org.Dod.Internet.SnmpV2.SnmpModules.SnmpMIB.MyNode"))
            .IsTrue();
        await Assert.That(sources.Any(s => s.Contains(".SnmpV2.Arc3", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task SuppliedCoreModules_AreParsedAndEmitted()
    {
        var (diagnostics, sources) = Run(
            ("TEST-MIB.mib", EnterpriseMib),
            ("SNMPv2-SMI.mib", ReadMib("SNMPv2-SMI.mib")),
            ("SNMPv2-TC.mib", ReadMib("SNMPv2-TC.mib")),
            ("SNMPv2-CONF.mib", ReadMib("SNMPv2-CONF.mib")));

        await Assert.That(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(HasIdClass(sources, "Snmp.Iso.Org.Dod.Internet.Private.Enterprises")).IsTrue();
        await Assert.That(HasIdClass(sources, "Snmp.Iso.Org.Dod.Internet.Mgmt.Mib2.Transmission")).IsTrue();
        await Assert.That(HasIdClass(sources, "Snmp.Itu.ZeroDotZero")).IsTrue();
    }

    private static bool HasIdClass(IEnumerable<string> sources, string @namespace) =>
        sources.Any(source => source.Contains($"namespace {@namespace}\n", StringComparison.Ordinal)
                              && source.Contains("public class Id : IBranchIdentifier", StringComparison.Ordinal));

    private static string ReadMib(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "mibs", fileName));

    private static (IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<string> Sources) Run(
        params (string Path, string Source)[] mibs)
    {
        AssemblyLoadContext.Default.LoadFromAssemblyPath(
            Path.Combine(AppContext.BaseDirectory, "SnmpSharpNet.Mib.dll"));
        // Only framework assemblies: the test assembly already contains the generated types
        var frameworkDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.StartsWith(frameworkDir, StringComparison.OrdinalIgnoreCase))
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(Oid).Assembly.Location));
        var compilation = CSharpCompilation.Create(
            "BuiltinModuleGeneration",
            [CSharpSyntaxTree.ParseText("public class Test { }")],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new SnmpGenerator().AsSourceGenerator()],
            [.. mibs.Select(mib => new InMemoryAdditionalText(mib.Path, mib.Source))]);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        var result = driver.GetRunResult().Results.Single();
        return (
            [
                .. result.Diagnostics,
                .. outputCompilation.GetDiagnostics()
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            ],
            [.. result.GeneratedSources.Select(source => source.SourceText.ToString())]);
    }

    private sealed class InMemoryAdditionalText(string path, string source) : AdditionalText
    {
        public override string Path => path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(source);
    }
}
