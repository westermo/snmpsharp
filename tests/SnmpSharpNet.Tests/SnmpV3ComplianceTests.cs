namespace SnmpSharpNet.Tests
{
    public class SnmpV3ComplianceTests
    {
        [Test]
        public void GeneratesReportPduOnAuthFailure()
        {
            var packet = new SnmpV3Packet();
            packet.AuthNoPriv("user"u8.ToArray(), "badPassword"u8.ToArray(), AuthenticationDigests.MD5);
            packet.SetEngineId(new OctetString("8000000001020304"));
            packet.Pdu.Set(new Pdu());
            var encoded = packet.Encode();
            Assert.Throws<SnmpAuthenticationException>(() =>
            {
                var snmpV3Packet =
                    new SnmpV3Packet(encoded, "wrongKey"u8, ReadOnlySpan<byte>.Empty);
            });
        }

        [Test]
        public void GeneratesReportPduOnPrivFailure()
        {
            var packet = new SnmpV3Packet();
            packet.AuthPriv("user"u8.ToArray(), "authPassword"u8.ToArray(), AuthenticationDigests.MD5,
                "badPrivPassword"u8.ToArray(), PrivacyProtocols.DES);
            packet.SetEngineId(new OctetString("8000000001020304"));
            packet.Pdu.Set(new Pdu());
            var encoded = packet.Encode();
            Assert.Throws<SnmpAuthenticationException>(() =>
            {
                var snmpV3Packet = new SnmpV3Packet(encoded, ReadOnlySpan<byte>.Empty, "wrongPrivKey"u8);
            });
        }

        [Test]
        public async Task HandlesEmptyAndLargePdu()
        {
            var packet = new SnmpV3Packet();
            packet.SetEngineId(new OctetString("8000000001020304"));
            var pdu = new Pdu();
            packet.Pdu.Set(pdu);
            var encoded = packet.Encode();
            var decoded = new SnmpV3Packet(encoded);
            for (int i = 0; i < 1000; i++)
                pdu.VbList.Add($"1.3.6.1.2.1.1.{i}");
            packet.Pdu.Set(pdu);
            encoded = packet.Encode();
            decoded = new SnmpV3Packet(encoded);
            await Assert.That(decoded.Pdu.VbList.Count).IsEqualTo(1000);
        }
    }
}