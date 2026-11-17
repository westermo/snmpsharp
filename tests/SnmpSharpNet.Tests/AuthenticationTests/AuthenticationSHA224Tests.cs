using System.Text;
using System.Security.Cryptography;

namespace SnmpSharpNet.Tests.AuthenticationTests;

public class AuthenticationSHA224Tests
{
    [Test]
    public async Task NameIdentifiesSha224()
    {
        await Assert.That(AuthenticationSHA224.Instance.Name).IsEqualTo("HMAC-SHA224");
    }

    [Test]
    public async Task AuthenticationSupportsKeysLongerThanTheHashBlock()
    {
        var key = RandomNumberGenerator.GetBytes(128);
        var message = "SNMP message"u8;
        var auth = AuthenticationSHA224.Instance;
        var digest = auth.Authenticate(key, message);

        await Assert.That(auth.AuthenticateIncomingMsg(key, digest, message)).IsTrue();
    }

    [Test]
    public async Task HmacSha224MatchesRfc4231TestCase1()
    {
        var key = Enumerable.Repeat((byte)0x0b, 20).ToArray();
        using var hmac = new HMACSHA224(key);
        var digest = hmac.ComputeHash("Hi There"u8.ToArray());
        var expected = Convert.FromHexString("896fb1128abbdf196832107cd49df33f47b4b1169912ba4f53684b22");

        await Assert.That(digest.Length).IsEqualTo(expected.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            await Assert.That(digest[i]).IsEqualTo(expected[i]);
        }
    }

    [Test]
    public async Task PasswordToKeyIsConsistent()
    {
        byte[] knownValue =
        [
            182, 254, 75, 99, 239,
            67, 36, 225, 119, 242,
            249, 74, 168, 94, 150,
            180, 170, 99, 225, 106,
            227, 211, 146, 83, 63,
            218, 49, 190
        ];
        var auth = AuthenticationSHA224.Instance;
        var password = "password"u8.ToArray();
        var engineId = new byte[] { 0x80, 0x00, 0x13, 0x70, 0x02, 0x01 };
        var key = auth.PasswordToKey(password, engineId);
        await Assert.That(key.Length).IsEqualTo(knownValue.Length);
        for (int i = 0; i < knownValue.Length; i++)
        {
            await Assert.That(key[i]).IsEqualTo(knownValue[i]);
        }
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(4)]
    [Arguments(8)]
    [Arguments(16)]
    [Arguments(32)]
    [Arguments(64)]
    [Arguments(128)]
    [Arguments(256)]
    [Arguments(512)]
    [Arguments(1024)]
    [Arguments(2048)]
    public async Task AnAuthenticatedBufferIsVerifiedByAuthenticateIncomingMessage(int packetLength)
    {
        var auth = AuthenticationSHA224.Instance;
        var password = "password"u8.ToArray();
        var engineId = new byte[] { 0x80, 0x00, 0x13, 0x70, 0x02, 0x01 };
        var packet = new byte[packetLength];
        Random.Shared.NextBytes(packet);
        var authenticate = auth.Authenticate(password, engineId, packet);
        var result = auth.AuthenticateIncomingMsg(password, engineId, authenticate, packet);
        await Assert.That(result).IsTrue();
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(4)]
    [Arguments(8)]
    [Arguments(16)]
    [Arguments(32)]
    [Arguments(64)]
    [Arguments(128)]
    [Arguments(256)]
    [Arguments(512)]
    [Arguments(1024)]
    [Arguments(2048)]
    public async Task AnAuthenticatedBufferWithTheWrongPasswordIsNotVerifiedByAuthenticateIncomingMessage(
        int packetLength)
    {
        var auth = AuthenticationSHA224.Instance;
        var password = "password"u8.ToArray();
        var password2 = "password2"u8.ToArray();
        var engineId = new byte[] { 0x80, 0x00, 0x13, 0x70, 0x02, 0x01 };
        var packet = new byte[packetLength];
        Random.Shared.NextBytes(packet);
        var authenticate = auth.Authenticate(password, engineId, packet);
        var result = auth.AuthenticateIncomingMsg(password2, engineId, authenticate, packet);
        await Assert.That(result).IsFalse();
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(4)]
    [Arguments(8)]
    [Arguments(16)]
    [Arguments(32)]
    [Arguments(64)]
    [Arguments(128)]
    [Arguments(256)]
    [Arguments(512)]
    [Arguments(1024)]
    [Arguments(2048)]
    public async Task AnAuthenticatedBufferWithTheWrongEngineIdIsNotVerifiedByAuthenticateIncomingMessage(
        int packetLength)
    {
        var auth = AuthenticationSHA224.Instance;
        var password = "password"u8.ToArray();
        var engineId = new byte[] { 0x80, 0x00, 0x13, 0x70, 0x02, 0x01 };
        var engineId2 = new byte[] { 0x80, 0x00, 0x13, 0x70, 0x02, 0x02 };
        var packet = new byte[packetLength];
        Random.Shared.NextBytes(packet);
        var authenticate = auth.Authenticate(password, engineId, packet);
        var result = auth.AuthenticateIncomingMsg(password, engineId2, authenticate, packet);
        await Assert.That(result).IsFalse();
    }
}