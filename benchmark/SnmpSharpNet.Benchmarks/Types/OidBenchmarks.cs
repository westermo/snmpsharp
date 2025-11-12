using BenchmarkDotNet.Attributes;

namespace SnmpSharpNet.Benchmarks.Types;

[MemoryDiagnoser]
public class OidBenchmarks
{
    private Oid _initial = new();
    private Oid _new = new();
    private const string Value = "1.22.333.4444.55555.666666.7777777.88888888.999999999";

    [GlobalSetup]
    public void Setup()
    {
        _initial = new Oid();
        _new = new Oid();
    }

    [Benchmark]
    public Oid EncodedToDecode()
    {
        Span<byte> buffer = stackalloc byte[Value.Length * 2];
        _initial.Encode(buffer);
        _new.Decode(buffer, 0);
        return _new;
    }
}