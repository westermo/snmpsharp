using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using SnmpSharpNet.Mib.Ast;

namespace SnmpSharpNet.Mib.SourceGenerator.IntegrationTests;

public class GeneratedMibModulesTests
{
    private const string RegistryHintName = "Snmp.GeneratedMibModules.generated.cs";

    // Contains characters that need escaping in C# string literals.
    private const string QuotingMib = """
                                      QUOTE-TEST-MIB DEFINITIONS ::= BEGIN
                                      -- A comment with "quotes", a \backslash\ and {braces}
                                      quoteRoot OBJECT IDENTIFIER ::= { iso 77 }
                                      quoteChild OBJECT IDENTIFIER ::= { quoteRoot 1 }
                                      END
                                      """;

    [Test]
    public async Task CompiledRegistry_ExposesSourcesAndParsedAsts()
    {
        await Assert.That(global::Snmp.GeneratedMibModules.ModuleNames).Contains("IF-MIB");
        await Assert.That(global::Snmp.GeneratedMibModules.ModuleNames).Contains("LLDP-MIB");
        await Assert.That(global::Snmp.GeneratedMibModules.Sources["IF-MIB"]).Contains("IF-MIB DEFINITIONS ::= BEGIN");

        var asts = global::Snmp.GeneratedMibModules.Asts;
        await Assert.That(asts.Count).IsEqualTo(global::Snmp.GeneratedMibModules.ModuleNames.Count);
        await Assert.That(asts["IF-MIB"].Identifier.ToString()).IsEqualTo("IF-MIB");
        await Assert.That(ReferenceEquals(asts, global::Snmp.GeneratedMibModules.Asts)).IsTrue();
    }

    [Test]
    public async Task CompiledRegistry_ResolvesTogetherWithRuntimeModules()
    {
        var runtimeAst = MibParser.ParseAst("""
                                            RUNTIME-EXTENSION-MIB DEFINITIONS ::= BEGIN
                                            IMPORTS ifMIB FROM IF-MIB;
                                            runtimeExtension OBJECT IDENTIFIER ::= { ifMIB 999 }
                                            END
                                            """);

        var modules = global::Snmp.GeneratedMibModules.Resolve([runtimeAst]);
        await Assert.That(modules.ContainsKey("IF-MIB")).IsTrue();
        await Assert.That(modules.ContainsKey("RUNTIME-EXTENSION-MIB")).IsTrue();

        var tree = global::Snmp.GeneratedMibModules.BuildTree([runtimeAst]);
        await Assert.That(tree.TryFind([1, 3, 6, 1, 2, 1, 31, 999], out var extension)).IsTrue();
        await Assert.That(extension.Name).IsEqualTo("runtimeExtension");
        await Assert.That(extension.ModuleName).IsEqualTo("RUNTIME-EXTENSION-MIB");
    }

    [Test]
    public async Task CompiledRegistry_TreeMatchesGeneratedOids()
    {
        var tree = global::Snmp.GeneratedMibModules.BuildTree();
        var oid = global::Snmp.Iso.Std.Iso8802.Ieee802dot1.Ieee802dot1mibs.Lldp.Objects.LocalSystemData
            .LocChassisIdSubtype.Oid;

        await Assert.That(tree.TryFind(oid.ToArray(), out var node)).IsTrue();
        await Assert.That(node.Name).IsEqualTo("lldpLocChassisIdSubtype");
        await Assert.That(node.Item).IsTypeOf<MibLeaf>();
    }

    [Test]
    public async Task Generator_EmitsCompilingRegistry_WhenMibAssemblyIsReferenced()
    {
        var (sources, compilation) = Run(QuotingMib, referenceMib: true);

        await Assert.That(sources.ContainsKey(RegistryHintName)).IsTrue();
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        await Assert.That(errors).IsEmpty();

        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        await Assert.That(emit.Success).IsTrue();

        var assembly = new AssemblyLoadContext("registry-test", isCollectible: true)
            .LoadFromStream(new MemoryStream(stream.ToArray()));
        var registry = assembly.GetType("Snmp.GeneratedMibModules", throwOnError: true)!;
        var embedded = (IReadOnlyDictionary<string, string>)registry
            .GetProperty("Sources", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        var asts = (IReadOnlyDictionary<string, ModuleDefinition>)registry
            .GetProperty("Asts", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;

        await Assert.That(embedded["QUOTE-TEST-MIB"]).IsEqualTo(QuotingMib);
        await Assert.That(asts["QUOTE-TEST-MIB"].Items.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Generator_SkipsRegistry_WhenMibAssemblyIsNotReferenced()
    {
        var (sources, _) = Run(QuotingMib, referenceMib: false);

        await Assert.That(sources.Keys.Any(name => name.StartsWith("Snmp.Identifiers/", StringComparison.Ordinal)))
            .IsTrue();
        await Assert.That(sources.ContainsKey(RegistryHintName)).IsFalse();
    }

    [Test]
    [Arguments("false")]
    [Arguments("False")]
    [Arguments("0")]
    public async Task Generator_SkipsRegistry_WhenOptedOut(string value)
    {
        var (sources, _) = Run(QuotingMib, referenceMib: true, embedProperty: value);

        await Assert.That(sources.ContainsKey(RegistryHintName)).IsFalse();
    }

    [Test]
    [Arguments("true")]
    [Arguments("")]
    public async Task Generator_EmitsRegistry_WhenPropertyEnabledOrEmpty(string value)
    {
        var (sources, _) = Run(QuotingMib, referenceMib: true, embedProperty: value);

        await Assert.That(sources.ContainsKey(RegistryHintName)).IsTrue();
    }

    private static (IReadOnlyDictionary<string, string> Sources, Compilation Compilation) Run(
        string mib, bool referenceMib, string? embedProperty = null)
    {
        AssemblyLoadContext.Default.LoadFromAssemblyPath(
            Path.Combine(AppContext.BaseDirectory, "SnmpSharpNet.Mib.dll"));

        // Only framework assemblies: the test output folder contains generated Snmp.* types and SnmpSharpNet.Mib.
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => !Path.GetFullPath(path).StartsWith(Path.GetFullPath(AppContext.BaseDirectory),
                StringComparison.OrdinalIgnoreCase))
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(Oid).Assembly.Location))
            .ToList();
        if (referenceMib)
        {
            references.Add(MetadataReference.CreateFromFile(typeof(MibParser).Assembly.Location));
        }

        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create(
            "RegistryTest",
            [CSharpSyntaxTree.ParseText("public class Test { }", parseOptions)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var globalOptions = embedProperty is null
            ? ImmutableDictionary<string, string>.Empty
            : ImmutableDictionary<string, string>.Empty.Add(
                $"build_property.{SnmpGenerator.EmbedMibSourcesProperty}", embedProperty);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new SnmpGenerator().AsSourceGenerator()],
            [new InMemoryAdditionalText("QUOTE-TEST-MIB.mib", mib)],
            parseOptions,
            new TestOptionsProvider(globalOptions));

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        var sources = driver.GetRunResult().Results.Single().GeneratedSources
            .ToDictionary(source => source.HintName, source => source.SourceText.ToString());
        return (sources, output);
    }

    private sealed class InMemoryAdditionalText(string path, string source) : AdditionalText
    {
        public override string Path => path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(source);
    }

    private sealed class TestOptionsProvider(ImmutableDictionary<string, string> globalOptions)
        : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new TestOptions(globalOptions);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new TestOptions(ImmutableDictionary<string, string>.Empty);

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new TestOptions(ImmutableDictionary<string, string>.Empty);
    }

    private sealed class TestOptions(ImmutableDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value!);
    }
}
