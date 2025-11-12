using BenchmarkDotNet.Attributes;

namespace SnmpSharpNet.Benchmarks.Types;

[MemoryDiagnoser]
public class Counter64Benchmarks
{
    [Params(ulong.MinValue, ulong.MaxValue / 2, ulong.MaxValue)]
    public ulong Value { get; set; }

    private Counter64 initial = new();
    private Counter64 @new = new();

    [GlobalSetup]
    public void Setup()
    {
        initial = new Counter64(Value);
        @new = new Counter64();
    }

    [Benchmark]
    public Counter64 EncodedToDecode()
    {
        Span<byte> buffer = stackalloc byte[Integer32.MaxEncodedSize];
        initial.Encode(buffer);
        @new.Decode(buffer, 0);
        return @new;
    }
}