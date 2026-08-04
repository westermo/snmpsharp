using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace SnmpSharpNet.Mib.SourceGenerator;

internal sealed class GeneratedSource
{
    public GeneratedSource(string hintName, string source)
    {
        HintName = hintName;
        Source = source;
    }

    public string HintName { get; }
    public string Source { get; }
}

internal static class TreeTemplating
{
    public static IEnumerable<GeneratedSource> Generate(OidTreeNode root, Compilation compilation)
    {
        var emitted = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in Descendants(root).Where(node => !node.HasValueAncestor()))
        {
            if (node.IsNamespace)
            {
                var @namespace = OidTreeNaming.NamespaceFor(node);
                var typeName = $"{@namespace}.Constants";
                if (emitted.Add(typeName) && compilation.GetTypeByMetadataName(typeName) is null)
                {
                    yield return new GeneratedSource(
                        HintName(@namespace, "Constants", node),
                        string.Join("\n", NamespaceConstants(@namespace, OidExpression(node))));
                }

                continue;
            }

            var parentNamespace = OidTreeNaming.NamespaceFor(node.Parent!);
            var name = OidTreeNaming.TypeName(node);
            var valueTypeName = $"{parentNamespace}.{name}";
            if (emitted.Add(valueTypeName) && compilation.GetTypeByMetadataName(valueTypeName) is null)
            {
                yield return new GeneratedSource(
                    HintName(parentNamespace, name, node),
                    string.Join("\n", ValueType(parentNamespace, name, node)));
            }
        }
    }

    private static IEnumerable<OidTreeNode> Descendants(OidTreeNode node) =>
        node.Children.Values
            .OrderBy(child => child.Arc)
            .SelectMany(child => new[] { child }.Concat(Descendants(child)));

    private static string[] NamespaceConstants(string @namespace, string oidExpression) =>
    [
        "#nullable enable",
        "using SnmpSharpNet;",
        "",
        $"namespace {@namespace}",
        "{",
        "\t/// <summary>OID for this branch of the SNMP object tree.</summary>",
        "\tpublic static class Constants",
        "\t{",
        "\t\tprivate static Oid? _oid;",
        $"\t\tpublic static Oid Oid => _oid ??= {oidExpression};",
        "\t}",
        "}"
    ];

    private static string[] ValueType(string @namespace, string name, OidTreeNode node)
    {
        return node.Item switch
        {
            MibLeaf leaf => LeafType(@namespace, name, node, leaf),
            MibTable table => TableType(@namespace, name, node, table),
            _ => throw new InvalidOperationException($"OID node '{name}' has no value type.")
        };
    }

    private static string[] LeafType(string @namespace, string name, OidTreeNode node, MibLeaf leaf)
    {
        var hasTableAncestor = node.AncestorsAndSelf().Any(ancestor => ancestor.Item is MibTable);
        var lines = new List<string>
        {
            "#nullable enable",
            "using SnmpSharpNet;",
            "",
            $"namespace {@namespace}",
            "{",
            $"\t/// <summary>{string.Join(".", node.AncestorsAndSelf().Skip(1).Select(x => x.Arc))}</summary>",
            $"\tpublic static class {name}",
            "\t{",
            "\t\tprivate static Oid? _oid;",
            $"\t\tpublic static Oid Oid => _oid ??= {OidExpression(node)};"
        };

        if (!hasTableAncestor)
        {
            lines.Add("\t\tprivate static Oid? _instanceOid;");
            lines.Add("\t\tpublic static Oid InstanceOid => _instanceOid ??= Oid.Append(0);");
        }

        lines.Add(
            $"\t\tpublic static {leaf.Type.AsAsnType()}? Get(AsnType value) => value as {leaf.Type.AsAsnType()};");
        lines.Add("\t}");
        lines.Add("}");
        return [.. lines];
    }

    private static string[] TableType(string @namespace, string name, OidTreeNode node, MibTable table)
    {
        var lines = new List<string>
        {
            "#nullable enable",
            "using System;",
            "using System.Collections.Generic;",
            "using System.Linq;",
            "using SnmpSharpNet;",
            "",
            $"namespace {@namespace}",
            "{",
            $"\t/// <summary>{string.Join(".", node.AncestorsAndSelf().Skip(1).Select(x => x.Arc))}</summary>",
            $"\tpublic static class {name}",
            "\t{",
            "\t\tprivate static Oid? _oid;",
            $"\t\tpublic static Oid Oid => _oid ??= {OidExpression(node)};",
            "",
            $"\t\tpublic static {table.TableType()} FromValues(IReadOnlyDictionary<Oid, AsnType> values) => {table.EntryType()}.TableFromValues(values);",
            $"\t\tpublic static IDictionary<Oid, AsnType> ToValues({table.TableType()} entries) => {table.EntryType()}.TableToValues(entries);",
            ""
        };

        lines.AddRange(Templating.TableEntry("public", table).Select(line => $"\t\t{line}"));

        foreach (var child in node.Children.Values.OrderBy(child => child.Arc))
        {
            lines.Add("");
            lines.AddRange(NestedNode(child, "\t\t", "Oid"));
        }

        lines.Add("\t}");
        lines.Add("}");
        return [.. lines];
    }

    private static IEnumerable<string> NestedNode(OidTreeNode node, string indent, string parentOid)
    {
        var name = OidTreeNaming.TypeName(node);
        var expression = $"{parentOid}.Append({node.Arc}u)";
        yield return $"public static class {name}";
        yield return "{";
        yield return "\tprivate static Oid? _oid;";
        yield return $"\tpublic static Oid Oid => _oid ??= {expression};";

        if (node.Item is MibLeaf leaf)
        {
            yield return
                $"\tpublic static {leaf.Type.AsAsnType()}? Get(AsnType value) => value as {leaf.Type.AsAsnType()};";
        }

        foreach (var child in node.Children.Values.OrderBy(child => child.Arc))
        {
            yield return "";
            foreach (var line in NestedNode(child, indent + "\t", "Oid"))
            {
                yield return $"\t{line}";
            }
        }

        yield return "}";
    }

    private static string OidExpression(OidTreeNode node)
    {
        if (node.Parent?.Parent is null)
        {
            return $"new Oid(new uint[] {{ {node.Arc}u }})";
        }

        var parentNamespace = OidTreeNaming.NamespaceFor(node.Parent!);
        return $"global::{parentNamespace}.Constants.Oid.Append({node.Arc}u)";
    }

    private static string HintName(string @namespace, string name, OidTreeNode node)
    {
        var builder = new StringBuilder("SnmpSharpNet.Mib.SourceGenerator/");
        builder.Append(@namespace.Replace("@", string.Empty))
            .Append('.')
            .Append(name.Replace("@", string.Empty))
            .Append(".generated.cs");
        return builder.ToString();
    }
}