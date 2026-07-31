using System.Text.Json;

namespace SnmpSharpNet.Mib.Tests;

public class GoldenOidTests
{
    private static readonly string GoldenDir = Path.Combine(AppContext.BaseDirectory, "golden-oids");
    private static readonly string MibDir = AppContext.BaseDirectory;

    public static IEnumerable<(string MibName, string GoldenFile)> GoldenTestCases()
    {
        if (!Directory.Exists(GoldenDir))
            yield break;

        foreach (var file in Directory.GetFiles(GoldenDir, "*.json"))
        {
            var mibName = Path.GetFileNameWithoutExtension(file);
            yield return (mibName, file);
        }
    }

    [Test]
    [MethodDataSource(nameof(GoldenTestCases))]
    public async Task OidsMatchNetSnmp((string MibName, string GoldenFile) testCase)
    {
        var (mibName, goldenFile) = testCase;

        var goldenJson = await File.ReadAllTextAsync(goldenFile);
        var expected = JsonSerializer.Deserialize<Dictionary<string, string>>(goldenJson)!;

        // Parse all MIBs in dependency order
        var mibFiles = Directory.GetFiles(MibDir, "*.mib");
        var asts = new Dictionary<string, Ast.ModuleDefinition>();
        foreach (var mibFile in mibFiles)
        {
            var contents = await File.ReadAllTextAsync(mibFile);
            var ast = MibParser.ParseAst(contents, Path.GetFileName(mibFile));
            asts[ast.Identifier.ToString()] = ast;
        }

        var modules = MibParser.ParseModules(asts);

        await Assert.That(modules.ContainsKey(mibName)).IsTrue()
            .Because($"Module '{mibName}' should be parseable");

        var module = modules[mibName];

        // Build a lookup: name → dotted OID string
        var actual = new Dictionary<string, string>();
        foreach (var (ident, _) in module.Items)
        {
            if (ident.Name is not null)
            {
                actual[ident.Name] = string.Join(".", ident.Oid);
            }
        }

        // Compare against the golden file
        var mismatches = new List<string>();
        var missing = new List<string>();

        foreach (var (name, expectedOid) in expected)
        {
            if (!actual.TryGetValue(name, out var actualOid))
            {
                missing.Add(name);
                continue;
            }

            if (actualOid != expectedOid)
            {
                mismatches.Add($"  {name}: expected {expectedOid}, got {actualOid}");
            }
        }

        if (mismatches.Count > 0)
        {
            var message = $"OID mismatches in {mibName}:\n{string.Join("\n", mismatches)}";
            await Assert.That(mismatches).IsEmpty().Because(message);
        }
    }
}
