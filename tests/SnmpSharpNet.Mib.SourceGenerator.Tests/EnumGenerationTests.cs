using Snmp.Iso.EnumTest;
using Snmp.Iso.EnumTest.Notifications;
using Snmp.Iso.EnumTest.Scalars;
using Color = Snmp.TextualConventions.EnumTest.EnumTestColor;
using Features = Snmp.TextualConventions.EnumTest.EnumTestFeatures;
using ShadeA = Snmp.TextualConventions.EnumTest.EnumTestShade;
using ShadeB = Snmp.TextualConventions.EnumTest.EnumTestShade_2;
using ColorLeaf = Snmp.Iso.EnumTest.Scalars.Color;
using FeaturesLeaf = Snmp.Iso.EnumTest.Scalars.Features;
using RangeLeaf = Snmp.Iso.EnumTest.Scalars.Range;
using TruthValue = Snmp.TextualConventions.SNMPv2Tc.TruthValue;

namespace SnmpSharpNet.Mib.SourceGenerator.IntegrationTests;

public class EnumGenerationTests
{
    private static Dictionary<Oid, AsnType> Single(Oid oid, AsnType value) => new() { [oid] = value };

    [Test]
    public async Task InlineEnum_FormatsLabelsAndKeepsNegativeValues()
    {
        await Assert.That((int)Mode.Values.Off).IsEqualTo(0);
        await Assert.That((int)Mode.Values.On).IsEqualTo(1);
        await Assert.That((int)Mode.Values.AutoDetect).IsEqualTo(2);
        await Assert.That((int)Mode.Values.Reserved).IsEqualTo(-2);
    }

    [Test]
    public async Task InlineEnum_DisambiguatesCollidingLabels()
    {
        await Assert.That((int)Collision.Values.FooBar).IsEqualTo(1);
        await Assert.That((int)Collision.Values.FooBar_2).IsEqualTo(2);
    }

    [Test]
    public async Task InlineEnum_AvoidsClashWithLeafClassName()
    {
        await Assert.That((int)Values.ValuesEnum.One).IsEqualTo(1);
        await Assert.That(Values.Parse(Single(Values.InstanceOid, new Integer32(2))))
            .IsEqualTo(Values.ValuesEnum.Two);
    }

    [Test]
    public async Task InlineBits_SuffixesLabelNamedNone()
    {
        await Assert.That((ulong)NoneBits.Bits.None).IsEqualTo(0UL);
        await Assert.That((ulong)NoneBits.Bits.None_3).IsEqualTo(1UL << 3);
        await Assert.That((ulong)NoneBits.Bits.Other).IsEqualTo(1UL << 4);
    }

    [Test]
    public async Task EnumLeaf_ParsesAndEncodesValues()
    {
        await Assert.That(Mode.Parse(Single(Mode.InstanceOid, new Integer32(2)))).IsEqualTo(Mode.Values.AutoDetect);
        await Assert.That(Mode.Parse(Single(Mode.InstanceOid, new Integer32(42)))).IsEqualTo((Mode.Values)42);
        await Assert.That(Mode.Parse(Single(Mode.InstanceOid, new OctetString("x")))).IsNull();
        await Assert.That(Mode.Parse(new Dictionary<Oid, AsnType>())).IsNull();
        await Assert.That(Mode.ToAsn(Mode.Values.Reserved)).IsEqualTo(new Integer32(-2));
        await Assert.That(Mode.CreateDefaultValue()).IsEqualTo(Mode.Values.On);
    }

    [Test]
    public async Task TextualConventionEnum_IsSharedAcrossLeavesAndColumns()
    {
        await Assert.That((int)Color.BlueGreen).IsEqualTo(3);
        await Assert.That(ColorLeaf.CreateDefaultValue()).IsEqualTo(Color.Green);
        await Assert.That(ColorLeaf.Parse(
                Single(ColorLeaf.InstanceOid, new Integer32(1))))
            .IsEqualTo(Color.Red);
        await Assert.That(RowEntry.RowColor.ToAsn(Color.BlueGreen)).IsEqualTo(new Integer32(3));
    }

    [Test]
    public async Task TextualConventionEnum_DisambiguatesCollidingNames()
    {
        await Assert.That((int)ShadeA.Dark).IsEqualTo(2);
        await Assert.That((int)ShadeB.Bright).IsEqualTo(6);
        await Assert.That(ShadeHyphen.Parse(Single(ShadeHyphen.InstanceOid, new Integer32(1))))
            .IsEqualTo(ShadeA.Light);
        await Assert.That(ShadePlain.Parse(Single(ShadePlain.InstanceOid, new Integer32(5))))
            .IsEqualTo(ShadeB.Dim);
    }

    [Test]
    public async Task BuiltinTextualConvention_TruthValueIsGenerated()
    {
        await Assert.That((int)TruthValue.True).IsEqualTo(1);
        await Assert.That((int)TruthValue.False).IsEqualTo(2);
        await Assert.That(Enabled.Parse(Single(Enabled.InstanceOid, new Integer32(1)))).IsEqualTo(TruthValue.True);
    }

    [Test]
    public async Task BitsLeaf_IsFlagsEnumWithRoundTrip()
    {
        await Assert.That(typeof(Flags.Bits).IsDefined(typeof(FlagsAttribute), false)).IsTrue();
        await Assert.That((ulong)Flags.Bits.Ninth).IsEqualTo(1UL << 9);
        await Assert.That(Flags.CreateDefaultValue()).IsEqualTo(Flags.Bits.First | Flags.Bits.Ninth);

        var encoded = Flags.ToAsn(Flags.Bits.First | Flags.Bits.Ninth);
        await Assert.That(encoded.ToArray()).IsEquivalentTo(new byte[] { 0x80, 0x40 });
        await Assert.That(Flags.Parse(Single(Flags.InstanceOid, encoded)))
            .IsEqualTo(Flags.Bits.First | Flags.Bits.Ninth);
        await Assert.That(Flags.Parse(Single(Flags.InstanceOid, new OctetString(new byte[] { 0x40 }))))
            .IsEqualTo(Flags.Bits.Second);
    }

    [Test]
    public async Task TextualConventionBits_IsFlagsEnum()
    {
        await Assert.That(Features.None).IsEqualTo((Features)0);
        await Assert.That((ulong)Features.GammaRay).IsEqualTo(1UL << 9);
        await Assert.That(FeaturesLeaf.ToAsn(Features.Alpha | Features.Beta).ToArray())
            .IsEquivalentTo(new byte[] { 0xC0, 0x00 });
    }

    [Test]
    public async Task BitsBeyondSixtyFourBits_StayOctetString()
    {
        var value = new OctetString(new byte[9]);
        await Assert.That(WideBits.Parse(Single(WideBits.InstanceOid, value))).IsEqualTo(value);
        await Assert.That(typeof(WideBits).GetInterfaces()).Contains(typeof(ISnmpLeaf<OctetString>));
    }

    [Test]
    public async Task RangeConstraints_AreExposedAsConstants()
    {
        await Assert.That(RangeLeaf.MinValue).IsEqualTo(-5L);
        await Assert.That(RangeLeaf.MaxValue).IsEqualTo(10L);
        await Assert.That(RangeLeaf.IsValidValue(-3)).IsTrue();
        await Assert.That(RangeLeaf.IsValidValue(0)).IsFalse();
        await Assert.That(RangeLeaf.IsValidValue(11)).IsFalse();
        await Assert.That(Percent.MinValue).IsEqualTo(0L);
        await Assert.That(Percent.MaxValue).IsEqualTo(100L);
    }

    [Test]
    public async Task SizeConstraints_AreExposedAsConstants()
    {
        await Assert.That(Name.MinSize).IsEqualTo(0);
        await Assert.That(Name.MaxSize).IsEqualTo(8);
        await Assert.That(Name.IsValidSize(0)).IsTrue();
        await Assert.That(Name.IsValidSize(2)).IsFalse();
        await Assert.That(Name.IsValidSize(6)).IsTrue();
        await Assert.That(Name.IsValidSize(9)).IsFalse();
    }

    [Test]
    public async Task TableEntry_UsesEnumColumnsAndConstraints()
    {
        await Assert.That(EnumTestRowTableEntry.LabelMinSize).IsEqualTo(1);
        await Assert.That(EnumTestRowTableEntry.LabelMaxSize).IsEqualTo(16);
        await Assert.That(EnumTestRowTableEntry.IsValidLabelSize(17)).IsFalse();
        await Assert.That((int)EnumTestRowTableEntry.StateValues.BusyWait).IsEqualTo(2);

        var values = new Dictionary<Oid, AsnType>
        {
            [new Oid("1.9997.2.1.2.7")] = new Integer32(2),
            [new Oid("1.9997.2.1.3.7")] = new Integer32(3),
            [new Oid("1.9997.2.1.4.7")] = new OctetString(new byte[] { 0x40 }),
            [new Oid("1.9997.2.1.5.7")] = new OctetString("row"),
        };

        var table = RowTable.Parse(values);
        await Assert.That(table).IsNotNull();
        await Assert.That(table!).HasSingleItem();
        var entry = table![0];
        await Assert.That(entry.IndexIndex).IsEqualTo(7u);
        await Assert.That(entry.State).IsEqualTo(EnumTestRowTableEntry.StateValues.BusyWait);
        await Assert.That(entry.Color).IsEqualTo(Color.BlueGreen);
        await Assert.That(entry.Flags).IsEqualTo(EnumTestRowTableEntry.FlagsBits.B);

        var roundTripped = new Dictionary<Oid, AsnType>();
        table.Populate(roundTripped);
        await Assert.That(roundTripped.Count).IsEqualTo(values.Count);
        foreach (var (oid, value) in values)
        {
            await Assert.That(roundTripped[oid].ToString()).IsEqualTo(value.ToString());
            await Assert.That(roundTripped[oid].Type).IsEqualTo(value.Type);
        }
    }

    [Test]
    public async Task TableColumnLeaf_ReusesEntryEnum()
    {
        var values = Single(RowEntry.RowState.Oid, new Integer32(1));
        await Assert.That(RowEntry.RowState.Parse(values)).IsEqualTo(EnumTestRowTableEntry.StateValues.Idle);
    }

    [Test]
    public async Task Notification_RoundTripsEnumAndBitsObjects()
    {
        var notification = new Event([7])
        {
            EnumTestMode = Mode.Values.AutoDetect,
            EnumTestFeatures = Features.Alpha | Features.GammaRay,
            EnumTestFlags = Flags.Bits.Second,
            EnumTestRowState = EnumTestRowTableEntry.StateValues.Idle,
            EnumTestRowNotifyState = EnumTestRowTableEntry.NotifyStateValues.Y
        };
        var bindings = new VbCollection();
        notification.Populate(bindings);

        await Assert.That(bindings[0].Value).IsEqualTo(new Integer32(2));
        await Assert.That(((OctetString)bindings[1].Value!).ToArray()).IsEquivalentTo(new byte[] { 0x80, 0x40 });

        var pdu = new Pdu(PduType.V2Trap) { TrapObjectID = Event.Oid };
        foreach (var vb in bindings) pdu.VbList.Add(vb);
        var parsed = Event.Parse(pdu);

        await Assert.That(parsed).IsEquivalentTo(notification);
    }
}
