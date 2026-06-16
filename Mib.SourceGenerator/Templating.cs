
using System.Collections.Generic;
using System.Linq;

namespace SnmpSharpNet.Mib.SourceGenerator;

using PartialClass = (string Accessibility, string? Namespace, string TypeName);

public static class Templating
{
    public static string Attributes =
        """
        #pragma warning disable CS9113
        using System;

        namespace SnmpSharpNet.Mib.Attributes
        {
            [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
            [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
            public sealed class MibModulesAttribute(params string[] modules) : Attribute {}

            [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
            [AttributeUsage(AttributeTargets.Class)]
            public sealed class MibOidsAttribute(string module) : Attribute {}
        }
        #pragma warning restore CS9113
        """;

    public static string[] DataFile(IEnumerable<(PartialClass, MibModule)> decls)
    {
        return [
            $"#nullable enable",
            $"using System.Collections.Generic;",
            $"using SnmpSharpNet;",
            $"",
            ..decls.SelectMany(x => PartialClass(x.Item1, x.Item2))
        ];
    }

    public static string[] PartialClass(PartialClass decl, MibModule module)
    {
        return decl.AsLiteral($": IOidGroup<{decl.TypeName}>", [
            ..module.Items.Values.OfType<MibValuedItem>().SelectMany(OidField),
            $"",
            ..module.Items.Values.OfType<MibValuedItem>().SelectMany(ValueField),
            $"",
            $"",
            $"public static IEnumerable<Oid> SimpleOids {{ get; }} = [",
            ..module.Items.Values.OfType<MibLeaf>().Select(
                item => $"\t{item.Ident.Name}Oid,"
            ),
            $"];",
            $"",
            $"public static IEnumerable<Oid> BatchOids {{ get; }} = [",
            ..module.Items.Values.OfType<MibTable>().Select(
                item => $"\t{item.Ident.Name}Oid,"
            ),
            $"];",
            $"",
            ..FromValues(decl.TypeName, module)
        ]);
    }

    public static string[] OidField(MibValuedItem item)
    {
        return [
            $"",
            $"/// <summary>",
            $"/// From SNMP Mib",
            $"/// {item.Ident}",
            $"/// </summary>",
            $"public static readonly Oid {item.Ident.Name}Oid = {item.Ident.AsLiteral()};",
        ];
    }
    public static string[] ValueField(MibValuedItem item)
    {
        return [
            $"",
            $"/// <summary>",
            $"/// From SNMP Mib",
            $"/// {item.Ident}",
            $"/// </summary>",
            $"public required AsnType {item.Ident.Name};"
        ];
    }


    public static string[] FromValues(string type, MibModule module)
    {
        return [
            $"public static {type}? FromValues(IReadOnlyDictionary<Oid, AsnType> values)",
            $"{{",
            ..module.Items.Values.OfType<MibValuedItem>().Select(
                x => $"\tif(!values.TryGetValue({x.Ident.Name}Oid, out var _{x.Ident.Name})) {{ return null; }}"
            ),
            $"",
            $"\treturn new {type}()",
            $"\t{{",
            ..module.Items.Values.OfType<MibValuedItem>().Select(
                x => $"\t\t{x.Ident.Name} = _{x.Ident.Name},"
            ),
            $"\t}};",
            $"}}",
        ];
    }
    
}