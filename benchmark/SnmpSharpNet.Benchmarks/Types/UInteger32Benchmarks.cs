using BenchmarkDotNet.Attributes;

namespace SnmpSharpNet.Benchmarks.Types;

[MemoryDiagnoser]
public class UInteger32Benchmarks
{
    [Params(uint.MinValue, uint.MaxValue / 2, uint.MaxValue)]
    public uint Value { get; set; }

    private UInteger32 initial = new();
    private UInteger32 @new = new();

    [GlobalSetup]
    public void Setup()
    {
        initial = new UInteger32(Value);
        @new = new UInteger32();
    }

    [Benchmark]
    public UInteger32 EncodedToDecode()
    {
        Span<byte> buffer = stackalloc byte[Integer32.MaxEncodedSize];
        initial.Encode(buffer);
        @new.Decode(buffer, 0);
        return @new;
    }
}