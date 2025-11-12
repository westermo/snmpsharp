namespace SnmpSharpNet.Tests.Security;

public class UserSecurityModelTests
{
    [Test]
    [Arguments(AuthenticationDigests.None)]
    [Arguments(AuthenticationDigests.SHA1)]
    [Arguments(AuthenticationDigests.SHA256)]
    [Arguments(AuthenticationDigests.SHA384)]
    [Arguments(AuthenticationDigests.SHA512)]
    [Arguments(AuthenticationDigests.MD5)]
    public async Task EncodedToDecode(AuthenticationDigests digests)
    {
        Span<byte> authKey = stackalloc byte[48];
        Random.Shared.NextBytes(authKey);
        Span<byte> wholePacket =
        [
            4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31,
            32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58,
            59, 60, 61, 62, 63, 64
        ];
        var initial = MakePacket(digests, authKey, wholePacket);
        Span<byte> buffer = stackalloc byte[initial.ByteLength];

        initial.Encode(buffer);
        var @new = new UserSecurityModel
        {
            Authentication = digests,
            Privacy = PrivacyProtocols.AES192
        };
        @new.Decode(buffer, 0);
        var authentic = @new.IsAuthentic(authKey, wholePacket);
        await Assert.That(@new.EngineTime).IsEqualTo(initial.EngineTime);
        await Assert.That(@new.EngineBoots).IsEqualTo(initial.EngineBoots);
        await Assert.That(@new.EngineId).IsEqualTo(initial.EngineId);
        await Assert.That(@new.SecurityName).IsEqualTo(initial.SecurityName);
        await Assert.That(@new.AuthenticationParameters).IsEqualTo(initial.AuthenticationParameters);
        await Assert.That(authentic).IsEqualTo(digests != AuthenticationDigests.None);
    }

    private static UserSecurityModel MakePacket(AuthenticationDigests digests, Span<byte> authKey,
        Span<byte> wholePacket)
    {
        var initial = new UserSecurityModel
        {
            EngineTime = 2,
            EngineBoots = 3,
            Authentication = digests,
            Privacy = PrivacyProtocols.AES192
        };
        initial.EngineId.Set("123");
        initial.SecurityName.Set("user");
        initial.AuthenticationSecret = "auth"u8.ToArray();
        initial.PrivacySecret = "priv"u8.ToArray();
        initial.Authenticate(authKey, wholePacket);
        return initial;
    }
}