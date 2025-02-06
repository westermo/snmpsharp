namespace SnmpSharpNet.Tests.AuthenticationTests;

public class AuthenticationSHA348Tests
{
    [Test]
    public async Task PasswordToKeyIsConsistent()
    {
        byte[] knownValue =
        [
            34, 83, 246, 66, 112, 242, 143, 0, 36, 57, 152, 177, 140, 125, 140, 48, 191, 57, 227, 38, 102, 59, 7, 135,
            67, 183, 51, 4, 188, 182, 144, 145, 44, 3, 95, 238, 232, 62, 158, 49, 41, 5, 165, 255, 136, 111, 183, 120
        ];
        var auth = new AuthenticationSHA384();
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
        var auth = new AuthenticationSHA384();
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
        var auth = new AuthenticationSHA384();
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
        var auth = new AuthenticationSHA384();
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