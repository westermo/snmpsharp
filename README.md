# SnmpSharpNet
Simple Network Management Protocol (SNMP) .Net library written in C# (csharp). Implements protocol version 1, 2 and 3. Original site: https://www.snmpsharpnet.com.

Code was originally forked by https://github.com/rqx110 from https://sourceforge.net/projects/snmpsharpnet/, then forked from https://sourceforge.net/projects/snmpsharpnet/ to https://github.com/westermo/snmpsharp.

## Westermo
A product at Westermo was dependent on the original package authored by Milan Sinadinovic. As the product evolved, and was ported to .NET 9, the package had to be recompiled to not lock onto the .NET Framework. The result is a library that targets .NET 9.0. 

Westermo does not actively develop this package, but does not oppose collaboration. Even though the package is fairly complete (SNMP v1 and v2 is not going to change!), PRs are welcome should they benefit any users of SnmpSharpNet.

The namespace has been prefixed with `Westermo`.

# Document
[see wiki](https://github.com/rqx110/SnmpSharpNet/wiki) 

# Pipeline statuses
[![Docker Image CI](https://github.com/westermo/snmpsharp/actions/workflows/docker-image.yml/badge.svg)](https://github.com/westermo/snmpsharp/actions/workflows/docker-image.yml)
[![CodeQL](https://github.com/westermo/snmpsharp/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/westermo/snmpsharp/actions/workflows/codeql-analysis.yml)

# MIB Source Generator

`SnmpSharpNet.Mib.SourceGenerator` is a Roslyn incremental source generator that reads `.mib` files at compile time and emits a strongly typed, global C# representation of the complete OID tree. No attributes or hand-written partial classes are required.

## Basic usage

Add the generator as an `Analyzer` reference and list your MIB files as `AdditionalFiles`:

```xml
<ItemGroup>
  <ProjectReference Include="../Mib.SourceGenerator/SnmpSharpNet.Mib.SourceGenerator.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="true"/>
</ItemGroup>

<ItemGroup>
  <AdditionalFiles Include="mibs/*.mib"/>
</ItemGroup>
```

Every named OID arc is generated automatically. Branches are namespaces with a `Id.Oid` property; scalar leaves and tables are static classes in their parent namespace. Names are PascalCased, the redundant module prefix is removed from descendants, and C# keywords are escaped.

```csharp
// LLDP-MIB's lldpObjects branch:
Snmp.Iso.Std.Iso8802.Ieee802dot1.Ieee802dot1mibs.Lldp.Objects.Constants.Oid

// A scalar leaf:
Snmp.Iso.Std.Iso8802.Ieee802dot1.Ieee802dot1mibs.Lldp.Objects.LocalSystemData
    .LocChassisIdSubtype.InstanceOid;

// A table:
Snmp.Iso.Org.Dod.Internet.Private.Enterprises.WestermoOid.Common
    .WestermoInterface.WmoInterfaceObjects.IfRefTable.FromValues(values);
```

## Generated binding mechanics

Generated table entries preserve the distinction between an SNMP row's identity and the bindings returned for that row:

- `INDEX` members are emitted as C# `required` fields. They are decoded from the instance OID and are needed to recreate the row's OIDs.
- Non-index table columns are nullable. An agent or a retrieval operation is not required to provide every column, so `Table.Parse(...)` retains partial rows and assigns only bindings that are present with the expected ASN type.
- `Populate(IDictionary<Oid, AsnType>)` writes only non-null table columns. This makes partial rows safe to round-trip without inventing values for absent bindings.

`NOTIFICATION-TYPE` objects have different semantics. Every object occurrence in its MIB `OBJECTS` clause is required and ordered, including repeated occurrences of the same object. Generated notification members therefore remain `required`, and parsing rejects a missing object or an object with the wrong ASN type.

Use the `VbCollection` overloads to preserve notification order and duplicates:

```csharp
var bindings = new VbCollection();
linkUp.Populate(bindings); // Adds MIB OBJECTS in declaration order.

LinkUp? parsed = LinkUp.Parse(bindings);
```

## Interoperability tests

The source-generator integration tests receive an SNMPv2 `linkUp` trap emitted by
Net-SNMP in Docker, decode its BER payload, and parse it through the generated
IF-MIB notification type. Its table-column varbinds use instance OIDs with the
row index suffix (for example, `ifIndex.99`), as required by SMIv2. Run them with:

```powershell
.\tests\SnmpSharpNet.Mib.SourceGenerator.IntegrationTests\run-integration-tests.ps1
```

These overloads operate on the MIB-defined notification-object sequence. For a decoded SNMPv2 Trap or Inform, use the generated namespace helper with the decoded `Pdu`: `Id.ParseNotifications(pdu)`. It identifies the notification from `pdu.TrapObjectID` and binds its remaining `pdu.VbList` in order; `Pdu.Decode` has already separated the protocol-defined `sysUpTime.0` and `snmpTrapOID.0` varbinds. The older dictionary notification-discovery overload is obsolete because it requires a non-standard synthetic notification-OID dictionary key and cannot preserve ordering or duplicate OIDs.

Generated notifications retain their instance-OID suffix in `NotificationIndexes`
and expose it as `InstanceOid`. `ToTrapPdu()` uses that instance OID as the
`snmpTrapOID`, allowing generated notifications to round-trip. If the MIB resolves
all notification columns to one table, the generator also emits typed properties
for that table's `INDEX` members. A name that would collide with a
notification-object property is prefixed with `Index`, so IF-MIB `linkUp` exposes
`IndexIfIndex` alongside its `IfIndex` value.

Notification parsing accepts a complete `Pdu`. It reads indexes from an instance
`snmpTrapOID`; for interoperating with senders such as Net-SNMP that use a bare
notification OID, it falls back to a common validated table-varbind suffix.

For interoperability, parsing is lenient in two further ways:

- Scalar notification objects are accepted both with the SMIv2 `.0` instance suffix
  and without it (some agents send e.g. `lldpStatsRemTablesInserts` for
  `lldpRemTablesChange`). `Populate` always emits the `.0` instance.
- Declared objects are matched in MIB order, but unrelated varbinds before, between
  or after them (e.g. `sysUpTime.0`, `sysDescr.0`) are tolerated. They are kept in
  original order in the notification's `AdditionalBindings` and appended after the
  declared objects by `Populate`.

`DEFVAL` is parsed as structured data and validated against its resolved syntax, including integer ranges, named enum/BITS values, OCTET STRING sizes, and object identifiers. `Counter32` and `Counter64` defaults are rejected. When a validated default can be represented by an SNMP runtime type, the generated leaf includes an explicit `CreateDefaultValue()` factory. It always returns a fresh mutable ASN.1 value:

```csharp
Integer32 first = MyMib.SomeLeaf.CreateDefaultValue();
Integer32 second = MyMib.SomeLeaf.CreateDefaultValue();
// first and second have the same value but are different instances.
```

For enumerated and BITS leaves `CreateDefaultValue()` returns the generated enum value instead (for example `Mode.Values.On` or `Flags.Bits.First | Flags.Bits.Ninth`).

## Enumerations, BITS and constraints

Named values are generated as C# enums, and leaves, table columns and notification objects use them instead of the raw ASN.1 type:

| MIB syntax | Generated type | Location |
| --- | --- | --- |
| Inline `INTEGER { ... }` on a scalar | `enum Values : int` | Nested in the leaf class, e.g. `Mode.Values.AutoDetect` |
| Inline `BITS { ... }` on a scalar | `[Flags] enum Bits : ulong` | Nested in the leaf class, e.g. `Flags.Bits.Ninth` |
| Inline enum/BITS on a table column | `{Column}Values` / `{Column}Bits` | Nested in the table entry, e.g. `IfTableEntry.AdminStatusValues.Up` |
| `TEXTUAL-CONVENTION` with enum/BITS syntax | `enum {Name}` | `Snmp.TextualConventions.{Module}`, e.g. `Snmp.TextualConventions.SNMPv2Tc.TruthValue` |

Labels are PascalCased (`auto-detect` becomes `AutoDetect`), and labels that format to the same identifier get a numeric suffix (`FooBar`, `FooBar_2`). If the nested name would clash with its leaf class, `Enum` is appended (`Values.ValuesEnum`). BITS enums always include `None = 0`, and bit `n` maps to `1UL << n`.

Enumerated leaves parse to `TEnum?` and expose a `ToAsn(TEnum)` helper that produces the wire value. Values that are not named in the MIB still round-trip as unnamed enum values. On the wire, BITS remain an `OctetString` (first octet, most significant bit = bit 0). The runtime `SnmpBits.ToMask(...)` and `SnmpBits.FromMask(...)` helpers convert between the two forms. A BITS type with a named bit above 63 cannot fit in a `ulong`, so it keeps the `OctetString` type.

```csharp
Mode.Values? mode = Mode.Parse(values);
values[Mode.InstanceOid] = Mode.ToAsn(Mode.Values.AutoDetect);

Flags.Bits? flags = Flags.Parse(values);
OctetString wire = Flags.ToAsn(Flags.Bits.First | Flags.Bits.Ninth);
```

Range and size refinements on leaves that keep their ASN.1 type are exposed as constants and validators:

```csharp
long min = Percent.MinValue;          // INTEGER (0..100)
bool ok = Percent.IsValidValue(42);
int maxSize = Name.MaxSize;           // OCTET STRING (SIZE (0 | 4..8))
bool valid = Name.IsValidSize(2);     // false: the validator honours every range
```

Table entries expose the same members prefixed with the column name, for example `LabelMinSize`, `LabelMaxSize` and `IsValidLabelSize(int)`.

> **Breaking change:** leaves, table-entry fields and notification fields with enumerated or BITS syntax were previously typed as `Integer32` / `OctetString`. They now use the generated enums. To get the ASN.1 value, call the leaf's `ToAsn(...)` (or `new Integer32((int)value)` / `SnmpBits.FromMask((ulong)value)`).
