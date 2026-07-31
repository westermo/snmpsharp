using BenchmarkDotNet.Attributes;
using SnmpSharpNet.Mib;
using SnmpSharpNet.Mib.Ast;

namespace SnmpSharpNet.Mib.Benchmarks;

/// <summary>
/// Benchmarks for MIB AST parsing (text → ModuleDefinition).
/// </summary>
[MemoryDiagnoser]
public class MibParsingBenchmarks
{
    private static readonly string MibDir = Path.Combine(AppContext.BaseDirectory, "mibs");

    private string _smallMibText = null!;   // IANAifType-MIB  (~25 KB, mostly enums)
    private string _mediumMibText = null!;  // IF-MIB          (~45 KB, tables + scalars)
    private string _largeMibText = null!;   // LLDP-MIB        (~75 KB, complex tables)

    [GlobalSetup]
    public void Setup()
    {
        _smallMibText = File.ReadAllText(Path.Combine(MibDir, "IANAifType-MIB.mib"));
        _mediumMibText = File.ReadAllText(Path.Combine(MibDir, "IF-MIB.mib"));
        _largeMibText = File.ReadAllText(Path.Combine(MibDir, "LLDP-MIB.mib"));
    }

    [Benchmark]
    public ModuleDefinition ParseAst_Small() =>
        MibParser.ParseAst(_smallMibText, "IANAifType-MIB");

    [Benchmark]
    public ModuleDefinition ParseAst_Medium() =>
        MibParser.ParseAst(_mediumMibText, "IF-MIB");

    [Benchmark]
    public ModuleDefinition ParseAst_Large() =>
        MibParser.ParseAst(_largeMibText, "LLDP-MIB");
}
