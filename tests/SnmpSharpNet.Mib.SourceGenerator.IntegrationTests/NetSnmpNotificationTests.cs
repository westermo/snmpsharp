using System.Net;
using System.Net.Sockets;
using Snmp.Iso.Org.Dod.Internet.SnmpV2.SnmpModules.SNMPv2.SnmpMIBObjects.SnmpTraps;

namespace SnmpSharpNet.Mib.SourceGenerator.IntegrationTests;

public class NetSnmpNotificationTests
{
    [Test]
    public async Task NetSnmpV2LinkUpTrap_DecodesAndParsesGeneratedNotification()
    {
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Any, 1162));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var datagram = await receiver.ReceiveAsync(timeout.Token);
        var packet = new SnmpV2Packet(datagram.Buffer);

        await Assert.That(packet.Community.ToString()).IsEqualTo("public");
        await Assert.That(packet.Pdu.Type).IsEqualTo(PduType.V2Trap);
        await Assert.That(packet.Pdu.TrapSysUpTime.Value).IsEqualTo(12345u);
        await Assert.That(packet.Pdu.TrapObjectID.Equals(LinkUp.Oid)).IsTrue();
        await Assert.That(packet.Pdu.VbList.Count).IsEqualTo(3);
        await Assert.That(packet.Pdu.VbList[0].Oid!.ToString()).IsEqualTo("1.3.6.1.2.1.2.2.1.1.99");
        await Assert.That(packet.Pdu.VbList[1].Oid!.ToString()).IsEqualTo("1.3.6.1.2.1.2.2.1.7.99");
        await Assert.That(packet.Pdu.VbList[2].Oid!.ToString()).IsEqualTo("1.3.6.1.2.1.2.2.1.8.99");

        var notification = global::Snmp.Iso.Id.ParseNotification(packet.Pdu) as LinkUp;
        await Assert.That(notification).IsNotNull();
        await Assert.That(notification.NotificationIndexes[0]).IsEqualTo(99u);
        await Assert.That(notification.Index).IsEqualTo(99u);
        await Assert.That(notification.IfIndex).IsEqualTo(new Integer32(99));
        await Assert.That(notification.IfAdminStatus).IsEqualTo(new Integer32(1));
        await Assert.That(notification.IfOperStatus).IsEqualTo(new Integer32(1));
    }
}