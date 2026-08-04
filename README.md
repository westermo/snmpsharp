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

Every named OID arc is generated automatically. Branches are namespaces with a `Constants.Oid` property; scalar leaves and tables are static classes in their parent namespace. Names are PascalCased, the redundant module prefix is removed from descendants, and C# keywords are escaped.

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

## Cross-project MIB dependencies

When a MIB in **Project B** imports symbols from a MIB that lives in **Project A**, all dependency MIBs must be visible to the generator as `AdditionalFiles` during Project B's build. The generator combines all added MIBs into a single absolute OID tree. Namespace declarations compose safely, and it does not re-emit a `Constants` or value type already supplied by a referenced assembly.

### Step 1 — Expose MIBs from the dependency project

In **Project A** (the project that owns the shared MIB files), add a `<Target>` that registers the MIBs for propagation to referencing projects:

```xml
<!-- Project A: exposes its MIBs to any project that references it -->
<ItemGroup>
  <AdditionalFiles Include="mibs/*.mib"/>
</ItemGroup>

<Target Name="PropagateAdditionalFiles" AfterTargets="ResolveReferences">
  <ItemGroup>
    <TargetPathWithTargetPlatformMoniker
      Include="@(AdditionalFiles)"
      IncludeRuntimeDependency="false"/>
  </ItemGroup>
</Target>
```

### Step 2 — Reference Project A from Project B normally

```xml
<!-- Project B: reference Project A and add its own MIBs -->
<ItemGroup>
  <ProjectReference Include="../ProjectA/ProjectA.csproj"/>
</ItemGroup>

<ItemGroup>
  <!-- Only Project B's own MIBs; Project A's MIBs arrive automatically -->
  <AdditionalFiles Include="mibs/*.mib"/>
</ItemGroup>
```

Roslyn propagates the `AdditionalFiles` from Project A into Project B's generator context. The generator performs topological sort across the full combined MIB set and resolves all cross-module imports in one pass.

### NuGet packages

If Project A is distributed as a NuGet package, place the MIBs in a `build/` sub-folder and ship a `.targets` file that registers them:

```xml
<!-- build/ProjectA.targets (included in the NuGet package) -->
<Project>
  <ItemGroup>
    <AdditionalFiles Include="$(MSBuildThisFileDirectory)mibs/*.mib"/>
  </ItemGroup>
</Project>
```

### Filename vs. module identifier

The generator matches MIB files by module identifier (the name declared inside the `.mib` file), not by filename. When a file's name does not match its internal module identifier, the generator emits a **SNMP004 warning** and uses the parsed identifier as the canonical key, so the import still resolves. Hard failures only occur when the MIB content cannot be parsed at all (SNMP001).
