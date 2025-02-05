namespace SnmpSharpNet.Tests;

public class AsnTypeTests
{
    [Test]
    [Arguments(0)]
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
    [Arguments(4096)]
    [Arguments(8192)]
    [Arguments(16384)]
    [Arguments(32767)]
    [Arguments(32768)]
    [Arguments(32769)]
    [Arguments(65536)]
    [Arguments(131072)]
    [Arguments(262144)]
    [Arguments(524288)]
    [Arguments(1048576)]
    [Arguments(2097152)]
    [Arguments(int.MaxValue)]
    public async Task EnsureLengthCalculationWorks(int length)
    {
        var res = AsnType.BuildHeader(stackalloc byte[AsnType.MaxHeaderSize], AsnType.CONTEXT, length);
        await Assert.That(res).IsEqualTo(AsnType.HeaderSize(length));
    }
}