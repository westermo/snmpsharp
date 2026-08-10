namespace SnmpSharpNet.Mib.Tests;

/// <summary>
/// Verifies that MIB modules which import symbols from other modules (cross-project scenario)
/// are resolved correctly when all dependency MIBs are provided together to ParseModules.
///
/// This mirrors what the source generator does at build time: Roslyn propagates AdditionalFiles
/// from referenced projects, so all MIBs (dependency and consumer alike) end up in a single
/// AdditionalTextsProvider pipeline and ParseModules sees the full set.
/// </summary>
public class CrossModuleResolutionTests
{
    private static readonly string MibDir = AppContext.BaseDirectory;

    private static Dictionary<string, Ast.ModuleDefinition> LoadAll()
    {
        var asts = new Dictionary<string, Ast.ModuleDefinition>();
        foreach (var mibFile in Directory.GetFiles(MibDir, "*.mib"))
        {
            var contents = File.ReadAllText(mibFile);
            var ast = MibParser.ParseAst(contents, Path.GetFileName(mibFile));
            asts[ast.Identifier.ToString()] = ast;
        }
        return asts;
    }

    /// <summary>
    /// WESTERMO-INTERFACE-MIB imports the 'common' OID arc from WESTERMO-OID-MIB.
    /// This test simulates Project B (WESTERMO-INTERFACE-MIB) depending on Project A
    /// (WESTERMO-OID-MIB): both are passed together to ParseModules and the dependency
    /// must resolve successfully.
    /// </summary>
    [Test]
    public async Task WestermoInterfaceMib_ResolvesImportFrom_WestermoOidMib()
    {
        var asts = LoadAll();

        await Assert.That(asts.ContainsKey("WESTERMO-INTERFACE-MIB")).IsTrue();
        await Assert.That(asts.ContainsKey("WESTERMO-OID-MIB")).IsTrue();

        // ParseModules does topological sort and resolves cross-module imports
        var modules = MibParser.ParseModules(asts);

        await Assert.That(modules.ContainsKey("WESTERMO-INTERFACE-MIB")).IsTrue();

        var ifMib = modules["WESTERMO-INTERFACE-MIB"];
        await Assert.That(ifMib.Items.Count).IsGreaterThan(0);
    }

    /// <summary>
    /// Verifies that only providing the dependency MIB set without the consumer fails,
    /// while providing both together succeeds — confirming that the cross-project dependency
    /// is actually exercised.
    /// </summary>
    [Test]
    public async Task WestermoInterfaceMib_FailsWithout_WestermoOidMib()
    {
        var asts = LoadAll();
        // Remove the dependency (simulates Project B without Project A's MIBs)
        asts.Remove("WESTERMO-OID-MIB");

        // Topological sort throws KeyNotFoundException when a non-builtin dependency is missing
        await Assert.That(() => MibParser.ParseModules(asts))
            .Throws<KeyNotFoundException>();
    }

    /// <summary>
    /// Verifies that ParseModules handles the full set of test MIBs (which include several
    /// inter-dependent modules) correctly in a single pass — the expected behaviour when
    /// all projects contribute their MIBs via AdditionalFiles propagation.
    /// </summary>
    [Test]
    public async Task AllTestMibs_ResolveInSinglePass()
    {
        var asts = LoadAll();
        await Assert.That(asts.Count).IsGreaterThan(5);

        var modules = MibParser.ParseModules(asts);
        await Assert.That(modules.Count).IsEqualTo(asts.Count);
    }
}
