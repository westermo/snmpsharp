using BenchmarkDotNet.Attributes;

namespace SnmpSharpNet.Benchmarks.Types;

[MemoryDiagnoser]
public class Integer32Benchmarks
{
    [Params(0, 1, 30000000, int.MaxValue, -2, -1, -22, int.MinValue)]
    public int Value { get; set; }

    private Integer32 initial = new();
    private Integer32 @new = new();

    [GlobalSetup]
    public void Setup()
    {
        initial = new Integer32(Value);
        @new = new Integer32();
    }

    [Benchmark]
    public Integer32 EncodedToDecode()
    {
        Span<byte> buffer = stackalloc byte[Integer32.MaxEncodedSize];
        initial.Encode(buffer);
        @new.Decode(buffer, 0);
        return @new;
    }
}