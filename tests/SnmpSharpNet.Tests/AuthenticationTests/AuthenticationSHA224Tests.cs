using System.Text;

namespace SnmpSharpNet.Tests.AuthenticationTests;

public class AuthenticationSHA224Tests
{
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
        var auth = new AuthenticationSHA224();
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
        var auth = new AuthenticationSHA224();
        var password = "password"u8.ToArray();
        var engineId = new byte[] { 0x80, 0x00, 0x13, 0x70, 0x02, 0x01 };
        var packet = new byte[packetLength];
        Random.Shared.NextBytes(packet);
        var authenticate = auth.authenticate(password, engineId, packet);
        var result = auth.authenticateIncomingMsg(password, engineId, authenticate, packet);
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
        var auth = new AuthenticationSHA224();
        var password = "password"u8.ToArray();
        var password2 = "password2"u8.ToArray();
        var engineId = new byte[] { 0x80, 0x00, 0x13, 0x70, 0x02, 0x01 };
        var packet = new byte[packetLength];
        Random.Shared.NextBytes(packet);
        var authenticate = auth.authenticate(password, engineId, packet);
        var result = auth.authenticateIncomingMsg(password2, engineId, authenticate, packet);
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
        var auth = new AuthenticationSHA224();
        var password = "password"u8.ToArray();
        var engineId = new byte[] { 0x80, 0x00, 0x13, 0x70, 0x02, 0x01 };
        var engineId2 = new byte[] { 0x80, 0x00, 0x13, 0x70, 0x02, 0x02 };
        var packet = new byte[packetLength];
        Random.Shared.NextBytes(packet);
        var authenticate = auth.authenticate(password, engineId, packet);
        var result = auth.authenticateIncomingMsg(password, engineId2, authenticate, packet);
        await Assert.That(result).IsFalse();
    }
}