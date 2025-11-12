using BenchmarkDotNet.Attributes;

namespace SnmpSharpNet.Benchmarks.Types;

[MemoryDiagnoser]
public class SnmpPacketBenchmarks
{
    private SnmpV1Packet _1Initial = new();
    private byte[] _1Bytes = [];
    private SnmpV2Packet _2Initial = new();
    private byte[] _2Bytes = [];
    private SnmpV3Packet _3Initial = new();
    private byte[] _3Bytes = [];
    private SecureAgentParameters _parameters = null!;

    private bool Private => Auth && Protocol != PrivacyProtocols.None;
    private bool Auth => Digest != AuthenticationDigests.None;
    [Params(true, false)] public bool Cached { get; set; }

    [Params(AuthenticationDigests.None, AuthenticationDigests.MD5, AuthenticationDigests.SHA1,
        AuthenticationDigests.SHA224, AuthenticationDigests.SHA256, AuthenticationDigests.SHA384,
        AuthenticationDigests.SHA512)]
    public AuthenticationDigests Digest { get; set; }

    [Params(PrivacyProtocols.None, PrivacyProtocols.AES192)]
    public PrivacyProtocols Protocol { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        SetupV1();
        SetupV2();
        SetupV3();
    }

    private void SetupV1()
    {
        var a = 123;
        var b = 234;
        var c = 345;
        var packet = new SnmpV1Packet("public");
        VbCollection vbs =
        [
            new Vb(new Oid("1.3.2"), new Integer32(a)),
            new Vb(new Oid("1.3.3"), new Integer32(b)),
            new Vb(new Oid("1.3.4"), new Integer32(c))
        ];
        packet.Pdu.SetVbList(vbs);
        packet.Pdu.RequestId = a * b * c;
        packet.Pdu.ErrorIndex = a + b + c;
        packet.Pdu.ErrorStatus = a - b + c;
        _1Initial = packet;
        _1Bytes = packet.Encode();
    }

    private void SetupV2()
    {
        var a = 123;
        var b = 234;
        var c = 345;
        var packet = new SnmpV2Packet("public");
        VbCollection vbs =
        [
            new Vb(new Oid("1.3.2"), new Integer32(a)),
            new Vb(new Oid("1.3.3"), new Integer32(b)),
            new Vb(new Oid("1.3.4"), new Integer32(c))
        ];
        packet.Pdu.SetVbList(vbs);
        packet.Pdu.RequestId = a * b * c;
        packet.Pdu.ErrorIndex = a + b + c;
        packet.Pdu.ErrorStatus = a - b + c;
        _2Initial = packet;
        _2Bytes = packet.Encode();
    }

    private void SetupV3()
    {
        var pdu = new ScopedPdu(PduType.GetNext, 123);
        _parameters = new SecureAgentParameters();
        SetAuth(Private, Auth, _parameters, Digest, Protocol);
        var packet = new SnmpV3Packet(_parameters, pdu);
        VbCollection vbs =
        [
            new Vb(new Oid("1.3.2"), new Integer32(123)),
            new Vb(new Oid("1.3.3"), new Integer32(234)),
            new Vb(new Oid("1.3.4"), new Integer32(345))
        ];

        packet.Pdu.SetVbList(vbs);
        packet.Pdu.RequestId = 123;
        packet.Pdu.ErrorIndex = 567;
        packet.Pdu.ErrorStatus = 6879;
        _3Initial = packet;
        _3Bytes = packet.Encode();
    }

    private void SetAuth(bool @private, bool auth, SecureAgentParameters parameters, AuthenticationDigests digests,
        PrivacyProtocols protocols)
    {
        @private &= protocols != PrivacyProtocols.None;
        auth &= digests != AuthenticationDigests.None;
        var username = "admin";
        var authenticationPassword = "someFakePassword";
        var privacyPassword = "someFakePassword";
        var engineId = "TheEngineID";

        switch (auth)
        {
            case true when @private:
                parameters.authPriv(username, digests, authenticationPassword, protocols, privacyPassword);
                break;
            case true:
                parameters.authNoPriv(username, digests, authenticationPassword);
                break;
            default:
                parameters.noAuthNoPriv(username);
                break;
        }

        parameters.EngineId.Set(engineId);
        parameters.EngineTime.Set("123");
        parameters.EngineBoots.Set("234");
        if (Cached) parameters.BuildCachedSecurityKeys();
    }

    // [Benchmark]
    // public int EncodeV1()
    // {
    //     Span<byte> bytes = stackalloc byte[_1Initial.ByteLength];
    //     return _1Initial.Encode(bytes);
    // }
    //
    // [Benchmark]
    // public SnmpV1Packet DecodeV1()
    // {
    //     return new SnmpV1Packet(_1Bytes);
    // }
    //
    // [Benchmark]
    // public int EncodeV2()
    // {
    //     Span<byte> bytes = stackalloc byte[_2Initial.ByteLength];
    //     return _2Initial.Encode(bytes);
    // }
    //
    // [Benchmark]
    // public SnmpV2Packet DecodeV2()
    // {
    //     return new SnmpV2Packet(_2Bytes);
    // }

    [Benchmark]
    public int EncodeV3()
    {
        Span<byte> bytes = stackalloc byte[_3Initial.ByteLength];
        return _3Initial.Encode(bytes);
    }

    [Benchmark]
    public SnmpV3Packet DecodeV3()
    {
        return new SnmpV3Packet(_3Bytes, _parameters);
    }
}