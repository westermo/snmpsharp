using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace SnmpSharpNet.Mib.SourceGenerator.IntegrationTests;

public class CompilationTypeAvailabilityTests
{
    [Test]
    public async Task UnrelatedCompilationChangesReuseTypeAvailability()
    {
        var frameworkDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.StartsWith(frameworkDirectory, StringComparison.OrdinalIgnoreCase))
            .Select(path => MetadataReference.CreateFromFile(path));
        var syntaxTree = CSharpSyntaxTree.ParseText("public class Test { }");
        var compilation = CSharpCompilation.Create(
            "IncrementalGeneratorTest",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var options = new GeneratorDriverOptions(
            IncrementalGeneratorOutputKind.None,
            trackIncrementalGeneratorSteps: true);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new SnmpGenerator().AsSourceGenerator()],
            [new InMemoryAdditionalText("TEST-MIB.mib", """
                TEST-MIB DEFINITIONS ::= BEGIN
                testRoot OBJECT IDENTIFIER ::= { iso 3 }
                END
                """)],
            driverOptions: options);

        driver = driver.RunGenerators(compilation);
        var updatedTree = CSharpSyntaxTree.ParseText("public class Test { public void Unrelated() { } }");
        driver = driver.RunGenerators(compilation.ReplaceSyntaxTree(syntaxTree, updatedTree));

        var steps = driver.GetRunResult().Results.Single().TrackedSteps;
        await Assert.That(steps.ContainsKey("CompilationTypeAvailability")).IsTrue();
        await Assert.That(steps.ContainsKey("MibGenerationInput")).IsTrue();
        await Assert.That(steps["MibGenerationInput"]
            .SelectMany(step => step.Outputs)
            .Any(output => output.Reason == IncrementalStepRunReason.Cached)).IsTrue();
    }

    private sealed class InMemoryAdditionalText(string path, string source) : AdditionalText
    {
        public override string Path => path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(source);
    }
}
