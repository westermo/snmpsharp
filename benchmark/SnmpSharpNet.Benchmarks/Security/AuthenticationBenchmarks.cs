using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace SnmpSharpNet.Benchmarks.Security;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[SimpleJob(RuntimeMoniker.NativeAot90)]
[SimpleJob(RuntimeMoniker.NativeAot10_0)]
public class AuthenticationBenchmarks
{
    private byte[]? _password;
    private byte[]? _engineId;

    [Params(8, 13, 101, 8000)] public int PasswordSize { get; set; }

    [GlobalSetup]
    public void PasswordToKeyIsConsistent()
    {
        _password = new byte[PasswordSize];
        Random.Shared.NextBytes(_password);
        _engineId = [0x80, 0x00, 0x13, 0x70, 0x02, 0x01];
    }

    [Benchmark(Baseline = true)]
    public byte[] Md5() => AuthenticationMD5.Instance.PasswordToKey(_password, _engineId);

    [Benchmark]
    public byte[] Sha1() => AuthenticationSHA1.Instance.PasswordToKey(_password, _engineId);

    [Benchmark]
    public byte[] Sha224() => AuthenticationSHA224.Instance.PasswordToKey(_password!, _engineId!);

    [Benchmark]
    public byte[] Sha256() => AuthenticationSHA256.Instance.PasswordToKey(_password, _engineId);

    [Benchmark]
    public byte[] Sha384() => AuthenticationSHA384.Instance.PasswordToKey(_password, _engineId);

    [Benchmark]
    public byte[] Sha512() => AuthenticationSHA512.Instance.PasswordToKey(_password, _engineId);
}