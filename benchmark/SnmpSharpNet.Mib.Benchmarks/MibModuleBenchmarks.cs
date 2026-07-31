using BenchmarkDotNet.Attributes;
using SnmpSharpNet.Mib;
using SnmpSharpNet.Mib.Ast;

namespace SnmpSharpNet.Mib.Benchmarks;

/// <summary>
/// Benchmarks for full MIB module construction (AST → resolved MibModule),
/// including OID resolution, type resolution, and table construction.
/// </summary>
[MemoryDiagnoser]
public class MibModuleBenchmarks
{
    private static readonly string MibDir = Path.Combine(AppContext.BaseDirectory, "mibs");

    // Pre-parsed ASTs
    private ModuleDefinition _ifMibAst = null!;
    private ModuleDefinition _lldpAst = null!;

    // All ASTs keyed by module name for multi-module resolution
    private Dictionary<string, ModuleDefinition> _allAsts = null!;

    // Pre-built import cache for single-module benchmarks
    private Dictionary<string, IMibThatExports> _ifMibImports = null!;
    private Dictionary<string, IMibThatExports> _lldpImports = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Parse all MIBs into ASTs
        _allAsts = new Dictionary<string, ModuleDefinition>();
        foreach (var file in Directory.GetFiles(MibDir, "*.mib"))
        {
            var text = File.ReadAllText(file);
            var name = Path.GetFileNameWithoutExtension(file);
            var ast = MibParser.ParseAst(text, name);
            _allAsts[ast.Identifier.ToString()] = ast;
        }

        // Build import caches for isolated benchmarks
        _ifMibAst = _allAsts["IF-MIB"];
        _lldpAst = _allAsts["LLDP-MIB"];

        _ifMibImports = BuildImportsFor("IF-MIB");
        _lldpImports = BuildImportsFor("LLDP-MIB");
    }

    private Dictionary<string, IMibThatExports> BuildImportsFor(string target)
    {
        // Build all modules, then return the cache minus the target itself
        var modules = MibParser.ParseModules(_allAsts);
        var cache = new Dictionary<string, IMibThatExports>(BuiltinMib.All);
        foreach (var kvp in modules)
        {
            if (kvp.Key != target)
                cache[kvp.Key] = kvp.Value;
        }
        return cache;
    }

    [Benchmark(Description = "Construct IF-MIB module")]
    public MibModule ConstructModule_IfMib() =>
        new MibModule(_ifMibAst, _ifMibImports);

    [Benchmark(Description = "Construct LLDP-MIB module")]
    public MibModule ConstructModule_LldpMib() =>
        new MibModule(_lldpAst, _lldpImports);

    [Benchmark(Description = "Resolve all 18 modules")]
    public Dictionary<string, MibModule> ResolveAllModules() =>
        MibParser.ParseModules(_allAsts);
}
