using System.Text;
using BenchmarkDotNet.Attributes;

namespace SnmpSharpNet.Tests.AuthenticationTests;

[MemoryDiagnoser]
public class AuthenticationBenchmarks
{
    private AuthenticationMD5? _md5;
    private AuthenticationSHA1? _sha1;
    private AuthenticationSHA256? _sha256;
    private AuthenticationSHA384? _sha384;
    private AuthenticationSHA512? _sha512;
    private byte[]? _password;
    private byte[]? _engineId;
    private AuthenticationSHA224? _sha224;

    [Params(111)] public int passwordSize { get; set; }

    [GlobalSetup]
    public void PasswordToKeyIsConsistent()
    {
        _md5 = new AuthenticationMD5();
        _sha1 = new AuthenticationSHA1();
        _sha256 = new AuthenticationSHA256();
        _sha224 = new AuthenticationSHA224();
        _sha384 = new AuthenticationSHA384();
        _sha512 = new AuthenticationSHA512();

        _password = new byte[passwordSize];
        Random.Shared.NextBytes(_password);
        _engineId = [0x80, 0x00, 0x13, 0x70, 0x02, 0x01];
    }

    // [Benchmark(Baseline = true)]
    // public byte[] MD5() => _md5!.PasswordToKey(_password, _engineId);
    //
    // [Benchmark]
    // public byte[] SHA1() => _sha1.PasswordToKey(_password, _engineId);

    [Benchmark]
    public byte[] SHA224() => _sha224!.PasswordToKey(_password!, _engineId!);
    //
    // [Benchmark]
    // public byte[] SHA256() => _sha256.PasswordToKey(_password, _engineId);
    //
    // [Benchmark]
    // public byte[] SHA384() => _sha384.PasswordToKey(_password, _engineId);
    //
    // [Benchmark]
    // public byte[] SHA512() => _sha512.PasswordToKey(_password, _engineId);
}