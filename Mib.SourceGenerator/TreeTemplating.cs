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
                    yield return new GeneratedSource(HintName(@namespace, "Id", "Identifiers"),
                        string.Join("\n", NamespaceConstants(@namespace, node, OidTreeNaming.TypeName(node))));
                }

                continue;
            }

            var parentNamespace = OidTreeNaming.NamespaceFor(node.Parent!);
            var name = OidTreeNaming.TypeName(node);
            var valueTypeName = $"{parentNamespace}.{name}";
            if (emitted.Add(valueTypeName) && compilation.GetTypeByMetadataName(valueTypeName) is null)
            {
                var folder = node.Item switch
                {
                    MibTable => "Tables",
                    MibNotification => "Notifications",
                    _ => "Leafs"
                };
                yield return new GeneratedSource(
                    HintName(parentNamespace, name, folder),
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

    private static IEnumerable<string> BranchIdentifier(OidTreeNode node, string name, string @namespace)
    {
        foreach (var comment in Templating.DocComment(DotForm(node)).Indent().Indent())
            yield return comment;
        yield return $"\t\tpublic static Oid Oid => field ??= {OidExpression(node)};";
        yield return $"\t\tpublic static string BranchName => \"{name}\";";
        yield return $"\t\tpublic static string FullBranchName => \"{@namespace}\";";
    }

    private static IEnumerable<string> CommonUsings()
    {
        yield return "#nullable enable";
        yield return "using SnmpSharpNet;";
        yield return "using System.Collections.Generic;";
        yield return "using System.CodeDom.Compiler;";
    }

    private static string[] NamespaceConstants(string @namespace, OidTreeNode node, string name) =>
    [
        .. CommonUsings(),
        "",
        $"namespace {@namespace}",
        "{",
        "\t/// <summary>OID for this branch of the SNMP object tree.</summary>",
        $"\t{Templating.Attribute}",
        "\tpublic class Id : IBranchIdentifier",
        "\t{",
        .. BranchIdentifier(node, name, @namespace),
        .. NotificationHelpers(node).SelectMany(static lines => lines.Prepend(string.Empty)).Indent().Indent(),
        "\t}",
        "}"
    ];

    private static string[] ValueType(string @namespace, string name, OidTreeNode node)
    {
        return node.Item switch
        {
            MibLeaf leaf => LeafType(@namespace, name, node, leaf),
            MibTable table => TableType(@namespace, name, node, table),
            MibNotification notification => NotificationType(@namespace, name, node, notification),
            _ => throw new InvalidOperationException($"OID node '{name}' has no value type.")
        };
    }

    private static string[] LeafType(string @namespace, string name, OidTreeNode node, MibLeaf leaf)
    {
        var oid = DotForm(node);
        var hasTableAncestor = node.AncestorsAndSelf().Any(ancestor => ancestor.Item is MibTable);
        List<string> lines =
        [
            .. CommonUsings(),
            "",
            $"namespace {@namespace}",
            "{",
            .. Templating.DocComment(
                oid,
                leaf.Description).Indent(),
            $"\t{Templating.Attribute}",
            $"\tpublic class {name} : ISnmpLeaf<{leaf.Type.AsAsnType()}>",
            "\t{",
            .. BranchIdentifier(node, name, @namespace),
        ];

        if (!hasTableAncestor)
        {
            lines.AddRange(Templating.DocComment(oid + ".0").Indent().Indent());
            lines.Add("\t\tpublic static Oid InstanceOid => field ??= Oid + 0;");
        }

        lines.Add(
            $"\t\tpublic static {leaf.Type.AsAsnType()}? Parse(IReadOnlyDictionary<Oid, AsnType> values) => values.TryGetValue(InstanceOid, out var value) ? value  as {leaf.Type.AsAsnType()} : null;");
        lines.Add("\t}");
        lines.Add("}");
        return [.. lines];
    }

    private static string DotForm(OidTreeNode skip)
    {
        return string.Join(".", skip.AncestorsAndSelf().Skip(1).Select(x => x.Arc));
    }

    private static string[] TableType(string @namespace, string name, OidTreeNode node, MibTable table)
    {
        var oid = DotForm(node);
        List<string> lines =
        [
            .. CommonUsings(),
            "using System;",
            "using System.Collections.Generic;",
            "using System.Linq;",
            "",
            $"namespace {@namespace}",
            "{",
            .. Templating.DocComment(
                oid,
                table.Description).Indent(),
            $"\t{Templating.Attribute}",
            $"\tpublic class {name} : List<{table.EntryType()}>, ISnmpTable<{name},{table.EntryType()}>",
            "\t{",
            .. BranchIdentifier(node, name, @namespace),
            "",
            .. Parse(table, name).Indent().Indent(),
            .. Populate().Indent().Indent(),
            "",
            "\t}",
            .. Templating.TableEntry("public", table, @namespace).Indent()
        ];


        foreach (var child in node.Children.Values
                     .Where(child => OidTreeNaming.TypeName(child) != table.EntryType())
                     .OrderBy(child => child.Arc))
        {
            lines.Add("");
            lines.AddRange(NestedNode(child, $"{name}.Oid").Indent());
        }

        lines.Add("}");
        return [.. lines];
    }

    private static string[] Populate()
    {
        return
        [
            $"public void Populate(IDictionary<Oid, AsnType> result)",
            "{",
            "\tforeach (var entry in this)",
            "\t{",
            "\t\tentry.Populate(result);",
            "\t}",
            "}"
        ];
    }

    public static string[] Parse(MibTable table, string name)
    {
        var typeName = table.EntryType();
        var hasVariableLengthIndex = table.Index.Any(x => x.Type.UintCount is null);
        // For fixed-length indexes we can compute the exact expected OID depth.
        // For variable-length indexes (OctetString, OID, etc.) the depth varies
        // per row, so we only require that the OID is longer than entry+column.
        var depthCheck = hasVariableLengthIndex
            ? "\tvar minDepth = entryDepth + 2;" // at least column + 1 index component
            : $"\tvar minDepth = entryDepth + 1 + {table.Index.Sum(x => x.Type.UintCount!.Value)};";

        return
        [
            $"public static {name}? Parse(IReadOnlyDictionary<Oid, AsnType> values)",
            "{",
            "\t// OID layout: Oid.1.<column>.<index...>",
            "\tvar entryDepth = Oid.Length + 1;",
            depthCheck,
            "\tvar relevantValues = values",
            "\t\t.Where(kv => kv.Key.Length >= minDepth && Oid.IsRootOf(kv.Key));",
            "",
            "\tvar entries = new Dictionary<Oid, Dictionary<uint, AsnType>>();",
            "",
            "\tforeach (var kv in relevantValues)",
            "\t{",
            "\t\tvar path = kv.Key.ToArray();",
            "\t\tvar column = path[entryDepth];",
            "\t\tvar index = new Oid(path[(entryDepth + 1)..]);",
            "\t\tif (!entries.ContainsKey(index))",
            "\t\t{",
            "\t\t\tentries[index] = new();",
            "\t\t}",
            "\t\tentries[index].Add(column, kv.Value);",
            "\t}",
            "",
            $"\treturn [..",
            $"\t\tentries.Select(x => {typeName}.Parse((uint[])x.Key, x.Value))",
            $"\t\t.OfType<{typeName}>()",
            "\t\t];",
            "}"
        ];
    }


    private static IEnumerable<string[]> NotificationHelpers(OidTreeNode node)
    {
        var directDescendants = node.Children.Select(s => s.Value).ToArray();
        var notifications = directDescendants
            .Where(child => child.IsNotification)
            .OrderBy(DotForm, StringComparer.Ordinal)
            .ToArray();
        var namespaces = directDescendants.Where(child => child.IsNamespace)
            .Where(child => Descendants(child).Any(s => s.IsNotification))
            .OrderBy(DotForm, StringComparer.Ordinal)
            .ToArray();
        var notificationTypeNames = notifications.Select(child =>
            $"global::{OidTreeNaming.NamespaceFor(child.Parent!)}.{OidTreeNaming.TypeName(child)}").ToArray();
        yield return
        [
            "public static IEnumerable<Oid> EnumerateNotifications()",
            "{",
            .. notificationTypeNames.Select(type =>
                $"\tyield return {type}.Oid;"),
            .. namespaces.Select(ns =>
                $"\tforeach(var oid in global::{OidTreeNaming.NamespaceFor(ns)}.Id.EnumerateNotifications()) yield return oid;"),
            "\tyield break;",
            "}"
        ];

        yield return
        [
            "public static IEnumerable<object> ParseNotifications(IReadOnlyDictionary<Oid, AsnType> values)",
            "{",
            .. notificationTypeNames.SelectMany(child => new[]
            {
                $"if(values.ContainsKey({child}.Oid))",
                "{",
                $"\t if({child}.Parse(values) is {{}} parsed) yield return parsed;",
                "}"
            }.Indent()),
            .. namespaces.SelectMany(ns => new[]
            {
                $"foreach(var parsed in global::{OidTreeNaming.NamespaceFor(ns)}.Id.ParseNotifications(values))",
                "{",
                "\tyield return parsed;",
                "}"
            }.Indent()),
            "",
            "\tyield break;",
            "}"
        ];
    }

    private static IEnumerable<string> NestedNode(OidTreeNode node, string parentOid)
    {
        var name = OidTreeNaming.TypeName(node);
        var expression = $"{parentOid} + {node.Arc}u";
        var @interface = node.Item is MibLeaf l ? $"ISnmpLeaf<{l.Type.AsAsnType()}>" : "IBranchIdentifier";
        if (node.Item is MibLeaf { Description: { } description } && !string.IsNullOrWhiteSpace(description))
        {
            foreach (var line in Templating.DocComment(description))
            {
                yield return line;
            }
        }

        yield return Templating.Attribute;
        yield return $"public class {name} : {@interface}";
        yield return "{";
        foreach (var comment in Templating.DocComment(DotForm(node))
                     .Indent())
            yield return comment;
        yield return $"\tpublic static Oid Oid => field ??= {expression};";
        yield return $"\tpublic static string BranchName => \"{name}\";";
        yield return $"\tpublic static string FullBranchName => \"{OidTreeNaming.NamespaceFor(node)}\";";

        if (node.Item is MibLeaf leaf)
        {
            yield return
                $"\tpublic static {leaf.Type.AsAsnType()}? Parse(IReadOnlyDictionary<Oid, AsnType> values) => values.TryGetValue(Oid, out var value) ? value  as {leaf.Type.AsAsnType()} : null;";
        }

        foreach (var child in node.Children.Values.OrderBy(child => child.Arc))
        {
            yield return "";
            foreach (var line in NestedNode(child, $"{name}.Oid"))
            {
                yield return $"\t{line}";
            }
        }

        yield return "}";
    }

    private static string[] NotificationType(string @namespace, string name, OidTreeNode node,
        MibNotification notification)
    {
        var oid = DotForm(node);


        return
        [
            .. CommonUsings(),
            "",
            $"namespace {@namespace}",
            "{",
            .. Templating.DocComment(
                oid,
                notification.Description).Indent(),
            $"\t{Templating.Attribute}",
            $"\tpublic class {name} : ISnmpNotification<{name}>",
            "\t{",
            .. BranchIdentifier(node, name, @namespace),
            .. notification.Objects.Select(obj =>
                $"\t\tpublic required {obj.Type.AsAsnType()} {obj.Ident.CSharpName(name)}; "),
            .. notification.Objects.Select(obj =>
                $"\t\tprivate static Oid {obj.Ident.CSharpName(name)}Id = {obj.Ident.AsLiteral()}; "),

            $"\t\tpublic static {name}? Parse(IReadOnlyDictionary<Oid, AsnType> values)",
            "\t\t{",
            .. notification.Objects.Select(obj =>
            {
                var objName = obj.Ident.CSharpName(name);
                var lName = objName.Replace(objName[0], char.ToLower(objName[0]));
                return
                    $"\t\t\tif(!values.TryGetValue({objName}Id, out var _{lName}) || _{lName} is not {obj.Type.AsAsnType()} {lName}) return null;";
            }),
            $"\t\t\treturn new {name}(){{",
            .. notification.Objects.Select(obj =>
            {
                var objName = obj.Ident.CSharpName(name);
                var lName = objName.Replace(objName[0], char.ToLower(objName[0]));
                return
                    $"\t\t\t\t{objName} = {lName},";
            }),
            "\t\t\t};",
            "\t\t}",
            "\t\tpublic void Populate(IDictionary<Oid, AsnType> values)",
            "\t\t{",
            "\t\t\tvalues[Oid] = new Integer32(0);",
            .. notification.Objects.Select(obj =>
            {
                var objName = obj.Ident.CSharpName(name);
                return $"\t\t\tvalues[{objName}Id] = {objName};";
            }),
            "\t\t}",
            "",
            "\t}",
            "}"
        ];
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