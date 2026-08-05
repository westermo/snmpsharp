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
