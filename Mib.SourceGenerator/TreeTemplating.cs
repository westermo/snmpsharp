using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace SnmpSharpNet.Mib.SourceGenerator;

internal sealed class GeneratedSource(string hintName, string source)
{
    public string HintName { get; } = hintName;
    public string Source { get; } = source;
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
                var typeName = $"{@namespace}.Id";
                if (emitted.Add(typeName) && compilation.GetTypeByMetadataName(typeName) is null)
                {
                    yield return new GeneratedSource(
                        HintName(@namespace, "Id", "Identifiers"),
                        string.Join("\n",
                            NamespaceConstants(@namespace,
                                OidExpression(node),
                                OidTreeNaming.TypeName(node))));
                }

                continue;
            }

            var parentNamespace = OidTreeNaming.NamespaceFor(node.Parent!);
            var name = OidTreeNaming.TypeName(node);
            var valueTypeName = $"{parentNamespace}.{name}";
            if (emitted.Add(valueTypeName) && compilation.GetTypeByMetadataName(valueTypeName) is null)
            {
                yield return new GeneratedSource(
                    HintName(parentNamespace, name, name.Contains("Table") ? "Tables" : "Leafs"),
                    string.Join("\n", ValueType(parentNamespace, name, node)));
            }
        }
    }

    private static string NodeName(OidTreeNode node)
    {
        return node.RawName ?? node.Arc.ToString();
    }

    private static IEnumerable<OidTreeNode> Descendants(OidTreeNode node) =>
        node.Children.Values
            .OrderBy(child => child.Arc)
            .SelectMany(child => new[] { child }.Concat(Descendants(child)));

    private static string[] NamespaceConstants(string @namespace, string oidExpression, string name) =>
    [
        "#nullable enable",
        "using SnmpSharpNet;",
        "",
        $"namespace {@namespace}",
        "{",
        "\t/// <summary>OID for this branch of the SNMP object tree.</summary>",
        "\tpublic class Id : IBranchIdentifier",
        "\t{",
        "\t\tprivate static Oid? _oid;",
        $"\t\tpublic static Oid Oid => _oid ??= {oidExpression};",
        $"\t\tpublic static string BranchName => \"{name}\";",
        $"\t\tpublic static string FullBranchName => \"{@namespace}\";",
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
            $"\tpublic class {name} : IBranchIdentifier",
            "\t{",
            $"\t\tpublic static Oid Oid => field ??= {OidExpression(node)};",
            $"\t\tpublic static string BranchName => \"{name}\";",
            $"\t\tpublic static string FullBranchName => \"{@namespace}\";"
        };

        if (!hasTableAncestor)
        {
            lines.Add("\t\tprivate static Oid? _instanceOid;");
            lines.Add("\t\tpublic static Oid InstanceOid => _instanceOid ??= Oid + 0;");
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
            $"\tpublic class {name} : ISnmpTable<{table.TableType()}>",
            "\t{",
            $"\t\tpublic static Oid Oid => field ??= {OidExpression(node)};",
            $"\t\tpublic static string BranchName => \"{name}\";",
            $"\t\tpublic static string FullBranchName => \"{@namespace}\";",
            "",
            $"\t\tpublic static {table.TableType()} FromValues(IReadOnlyDictionary<Oid, AsnType> values) => {table.EntryType()}.TableFromValues(values);",
            $"\t\tpublic static IDictionary<Oid, AsnType> ToValues({table.TableType()} entries) => {table.EntryType()}.TableToValues(entries);",
            ""
        };


        lines.Add("\t}");
        lines.AddRange(Templating.TableEntry("public", table));
        foreach (var child in node.Children.Values.OrderBy(child => child.Arc))
        {
            lines.Add("");
            lines.AddRange(NestedNode(child, $"{name}.Oid"));
        }

        lines.Add("}");
        return [.. lines];
    }

    private static IEnumerable<string> NestedNode(OidTreeNode node, string parentOid)
    {
        var name = OidTreeNaming.TypeName(node);
        var expression = $"{parentOid} + {node.Arc}u";
        var @interface = node.Item is MibLeaf l ? $"ISnmpLeaf<{l.Type.AsAsnType()}>" : "IBranchIdentifier";
        yield return $"public class {name} : {@interface}";
        yield return "{";
        yield return $"\tpublic static Oid Oid => field ??= {expression};";
        yield return $"\tpublic static string BranchName => \"{name}\";";
        yield return $"\tpublic static string FullBranchName => \"{OidTreeNaming.NamespaceFor(node)}\";";

        if (node.Item is MibLeaf leaf)
        {
            yield return
                $"\tpublic static {leaf.Type.AsAsnType()}? Get(AsnType value) => value as {leaf.Type.AsAsnType()};";
        }

        foreach (var child in node.Children.Values.OrderBy(child => child.Arc))
        {
            yield return "";
            foreach (var line in NestedNode(child, "Oid"))
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
        return $"global::{parentNamespace}.Id.Oid + {node.Arc}";
    }

    private static string HintName(string @namespace, string name, string folder)
    {
        var builder = new StringBuilder($"Snmp.{folder}/");
        builder.Append(@namespace.Replace("@", string.Empty))
            .Append('.')
            .Append(name.Replace("@", string.Empty))
            .Append(".generated.cs");
        return builder.ToString();
    }
}