using BenchmarkDotNet.Attributes;

namespace SnmpSharpNet.Benchmarks.Types;

[MemoryDiagnoser]
public class SequenceBenchmarks
{
    [Params(4, 64, 1024)] public int Value { get; set; }

    private Sequence initial = new();
    private Sequence @new = new();

    [GlobalSetup]
    public void Setup()
    {
        var buffer = new byte[Value];
        Random.Shared.NextBytes(buffer);
        initial = new Sequence(buffer);
        @new = new Sequence();
    }

    [Benchmark]
    public Sequence EncodedToDecode()
    {
        Span<byte> buffer = stackalloc byte[Value + 10];
        initial.Encode(buffer);
        @new.Decode(buffer, 0);
        return @new;
    }
}