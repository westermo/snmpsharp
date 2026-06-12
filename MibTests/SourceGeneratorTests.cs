using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using WeConfig.Language.SourceGenerators;

namespace MibTests;

public class SourceGeneratorTests
{
    static SourceGeneratorTests()
    {
        // The generator depends on SnmpSharpNet.Mib.dll which isn't in the
        // driver's probing path. Resolve it from the test output directory.
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            var name = new AssemblyName(args.Name).Name;
            var path = Path.Combine(AppContext.BaseDirectory, name + ".dll");
            return File.Exists(path) ? System.Reflection.Assembly.LoadFrom(path) : null;
        };
    }

    private static GeneratorDriver CreateDriver(string source, params (string name, string content)[] additionalFiles)
    {
        var parseOptions = CSharpParseOptions.Default;
        
        // The attributes must be available in the compilation before the generator runs

        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var references = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trustedPath
            ? trustedPath.Split(Path.PathSeparator)
                .Select(p => MetadataReference.CreateFromFile(p))
                .ToArray()
            :
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            ];

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new SnmpGenerator();
        var driver = CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts([
                ..additionalFiles.Select(f => new InMemoryAdditionalText(f.name, f.content))
            ]);

        return driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
    }

    private static GeneratorRunResult GetResult(GeneratorDriver driver)
    {
        var result = driver.GetRunResult().Results[0];
        if (result.Exception is not null)
            throw new InvalidOperationException(
                $"Generator threw {result.Exception.GetType().Name}: {result.Exception.Message}",
                result.Exception);
        return result;
    }

    [Test]
    public async Task Emits_Attribute_Definitions()
    {
        var driver = CreateDriver(
            """
            using SnmpSharpNet.Mib.Attributes;

            [assembly: MibModules("WESTERMO-OID-MIB", "WESTERMO-INTERFACE-MIB")]

            [MibOids("WESTERMO-INTERFACE-MIB")]
            partial class WestermoOids {}
            """,
            ("WESTERMO-OID-MIB.mib", File.ReadAllText("WESTERMO-OID-MIB.mib")),
            ("WESTERMO-INTERFACE-MIB.mib", File.ReadAllText("WESTERMO-INTERFACE-MIB.mib"))
        );
        var result = GetResult(driver);

        var hintNames = result.GeneratedSources.Select(s => s.HintName).ToArray();
        await Assert.That(hintNames).Contains("SnmpAttributes.Generated.cs");
        await Assert.That(hintNames).Contains("SnmpMibData.Generated.cs");
    }

    [Test]
    public async Task Reports_SNMP002_When_Mib_File_Not_Found()
    {
        var driver = CreateDriver(
            """
            [assembly: SnmpSharpNet.Mib.Attributes.MibModules("NONEXISTENT-MIB")]
            """
        );
        var result = GetResult(driver);

        var diag = result.Diagnostics.FirstOrDefault(d => d.Id == "SNMP002");
        await Assert.That(diag).IsNotNull();
        await Assert.That(diag!.GetMessage()).Contains("NONEXISTENT-MIB");
    }

    [Test]
    public async Task Parses_Mib_Without_Errors()
    {

        var driver = CreateDriver(
            """
            [assembly: SnmpSharpNet.Mib.Attributes.MibModules("WESTERMO-OID-MIB")]
            """,
            ("WESTERMO-OID-MIB.mib", File.ReadAllText("WESTERMO-OID-MIB.mib"))
        );

        var result = GetResult(driver);

        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        await Assert.That(errors).IsEmpty();
    }
}

file class InMemoryAdditionalText(string path, string content) : AdditionalText
{
    public override string Path => path;
    public override SourceText? GetText(CancellationToken cancellationToken = default)
        => SourceText.From(content);
}