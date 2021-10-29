![Nuget](https://img.shields.io/nuget/v/SnmpSharpNet)

# SnmpSharpNet
Simple Network Management Protocol (SNMP) .Net library written in C# (csharp). Implements protocol version 1, 2 and 3. Original site: https://www.snmpsharpnet.com.

Code was originally forked by https://github.com/rqx110 from https://sourceforge.net/projects/snmpsharpnet/, then forked from https://sourceforge.net/projects/snmpsharpnet/ to https://github.com/westermo/snmpsharp.

## Westermo
A product at Westermo was dependent on the original package authored by Milan Sinadinovic. As the product evolved, and was ported to .NET 6 (the continuation of .NET Core), the package had to be recompiled to not lock onto the .NET Framework. The result is a library that targets
.NET Standard 2.0. As such it may now be used both with .NET Framework and .NET Core (and its continuations .NET 5, 6, etc.).

Westermo does not actively develop this package, but does not oppose collaboration. Even though the package is fairly complete (SNMP v1 and v2 is not going to change!), PRs are welcome should they benefit any users of SnmpSharpNet.

The namespace has been prefixed with `Westermo`.

# Document
[see wiki](https://github.com/rqx110/SnmpSharpNet/wiki) 
