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
    public static IEnumerable<GeneratedSource> Generate(OidTreeNode root, Compilation compilation) =>
        Generate(root, [], compilation);

    public static IEnumerable<GeneratedSource> Generate(
        OidTreeNode root,
        IEnumerable<MibModule> modules,
        Compilation compilation)
    {
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        var resolver = new ValueTypeResolver(root, compilation);

        var textualConventions = modules
            .SelectMany(module => module.TextualConventions.Values)
            .Concat(Descendants(root).Select(node => node.Item).OfType<MibLeaf>().Select(leaf => leaf.Type))
            .Where(ValueTypes.IsTextualConventionEnum);
        foreach (var type in textualConventions)
        {
            var tc = type.TextualConvention!;
            var @namespace = ValueTypes.TextualConventionNamespace(tc);
            var name = ValueTypes.TextualConventionTypeName(tc);
            var typeName = $"{@namespace}.{name}";
            if (!emitted.Add(typeName) || compilation.GetTypeByMetadataName(typeName) is not null) continue;
            yield return new GeneratedSource(
                HintName(@namespace, name, "TextualConventions"),
                string.Join("\n", TextualConventionEnum(@namespace, name, type, tc)));
        }

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
            if (!emitted.Add(valueTypeName) || compilation.GetTypeByMetadataName(valueTypeName) is not null) continue;
            var folder = node.Item switch
            {
                MibTable => "Tables",
                MibNotification => "Notifications",
                _ => "Leafs"
            };
            yield return new GeneratedSource(
                HintName(parentNamespace, name, folder),
                string.Join("\n", ValueType(parentNamespace, name, node, resolver)));
        }
    }

    private static string[] TextualConventionEnum(string @namespace, string name, MibType type,
        MibTextualConvention tc) =>
    [
        .. CommonUsings(),
        "",
        $"namespace {@namespace}",
        "{",
        .. ValueTypes.EnumDeclaration(name, type,
            tc.Description,
            $"Textual convention {tc.Name} defined in {tc.Module}.").Indent(),
        "}"
    ];

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
        yield return "using System;";
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

    private static string[] ValueType(string @namespace, string name, OidTreeNode node, ValueTypeResolver resolver)
    {
        return node.Item switch
        {
            MibLeaf leaf => LeafType(@namespace, name, node, leaf, resolver),
            MibTable table => TableType(@namespace, name, node, table, resolver),
            MibNotification notification => NotificationType(@namespace, name, node, notification, resolver),
            _ => throw new InvalidOperationException($"OID node '{name}' has no value type.")
        };
    }

    private static string[] LeafType(string @namespace, string name, OidTreeNode node, MibLeaf leaf,
        ValueTypeResolver resolver)
    {
        var oid = DotForm(node);
        var hasTableAncestor = node.AncestorsAndSelf().Any(ancestor => ancestor.Item is MibTable);
        var info = resolver.Resolve(node, leaf);
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
            $"\tpublic class {name} : ISnmpLeaf<{info.LeafTypeArgument}>",
            "\t{",
            .. BranchIdentifier(node, name, @namespace),
            .. LeafValueMembers(leaf, info, name).Indent().Indent()
        ];

        if (!hasTableAncestor)
        {
            lines.AddRange(Templating.DocComment(oid + ".0").Indent().Indent());
            lines.Add("\t\tpublic static Oid InstanceOid => field ??= Oid + 0;");
        }

        lines.Add($"\t\t{LeafParse(info, "InstanceOid")}");
        lines.Add("\t}");
        lines.Add("}");
        return [.. lines];
    }

    private static string LeafParse(ValueTypeInfo info, string oid) =>
        info.IsConverted
            ? $"public static {info.CsType}? Parse(IReadOnlyDictionary<Oid, AsnType> values) => values.TryGetValue({oid}, out var value) && value is {info.AsnType} asn ? {info.FromAsn("asn")} : null;"
            : $"public static {info.AsnType}? Parse(IReadOnlyDictionary<Oid, AsnType> values) => values.TryGetValue({oid}, out var value) ? value  as {info.AsnType} : null;";

    // Members shared by top-level and nested leaf classes: inline enum, DEFVAL factory, ASN conversion
    // and range/size constants.
    private static IEnumerable<string> LeafValueMembers(MibLeaf leaf, ValueTypeInfo info, string className,
        bool ownsInlineEnum = true)
    {
        if (ownsInlineEnum && info.IsConverted && ValueTypes.IsInlineEnum(leaf.Type))
        {
            yield return "";
            foreach (var line in ValueTypes.EnumDeclaration(
                         ValueTypes.LeafEnumName(leaf.Type, className), leaf.Type,
                         $"Named values of {leaf.Ident.Name}."))
            {
                yield return line;
            }

            yield return "";
        }

        var defaultFactory = ValueTypes.DefaultFactory(leaf, info);
        if (defaultFactory is not null)
        {
            yield return $"public static {info.CsType} CreateDefaultValue() => {defaultFactory};";
        }

        if (info.IsConverted)
        {
            yield return $"/// <summary>Encodes a value as the <see cref=\"{info.AsnType}\"/> used on the wire.</summary>";
            yield return $"public static {info.AsnType} ToAsn({info.CsType} value) => {info.ToAsn("value")};";
        }

        foreach (var line in ValueTypes.ConstraintMembers(leaf.Type))
        {
            yield return line;
        }
    }

    private static string DotForm(OidTreeNode skip)
    {
        return string.Join(".", skip.AncestorsAndSelf().Skip(1).Select(x => x.Arc));
    }

    private static string[] TableType(string @namespace, string name, OidTreeNode node, MibTable table,
        ValueTypeResolver resolver)
    {
        var oid = DotForm(node);
        var entryLeaves = node.Children.TryGetValue(1, out var entryNode)
            ? entryNode.Children.Values
                .OrderBy(child => child.Arc)
                .Where(child => child.Item is MibLeaf)
                .Select(child => (Leaf: (MibLeaf)child.Item!, Info: resolver.Resolve(child, (MibLeaf)child.Item!)))
                .ToArray()
            : [];
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
            .. Templating.TableEntry("public", table, @namespace, entryLeaves).Indent()
        ];


        foreach (var child in node.Children.Values
                     .Where(child => OidTreeNaming.TypeName(child) != table.EntryType())
                     .OrderBy(child => child.Arc))
        {
            lines.Add("");
            lines.AddRange(NestedNode(child, $"{name}.Oid", resolver).Indent());
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
        var columnArcs = string.Join(", ", table.Columns.Select(column => $"{column.Ident.Oid.Last()}u"));
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
            $"\tvar columns = new HashSet<uint> {{ {columnArcs} }};",
            "\tvar relevantValues = values",
            "\t\t.Where(kv => kv.Key.Length >= minDepth",
            "\t\t\t&& Oid.IsRootOf(kv.Key)",
            "\t\t\t&& columns.Contains(kv.Key.ToArray()[entryDepth]));",
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
            "public static object? ParseNotification(Pdu pdu)",
            "{",
            "\tif (pdu.Type is not (PduType.V2Trap or PduType.Inform)) return null;",
            "\tif (!Oid.IsRootOf(pdu.TrapObjectID)) return null;",
            .. notificationTypeNames.Select(child =>
                $"\t{{\tif({child}.Parse(pdu) is {{}} parsed) return parsed;}}"),
            .. namespaces.Select(ns =>
                $"\t{{\tif(global::{OidTreeNaming.NamespaceFor(ns)}.Id.ParseNotification(pdu) is {{}} parsed) return parsed;}}"),
            "",
            "\treturn null;",
            "}"
        ];
    }

    private static IEnumerable<string> NestedNode(OidTreeNode node, string parentOid, ValueTypeResolver resolver)
    {
        var name = OidTreeNaming.TypeName(node);
        var expression = $"{parentOid} + {node.Arc}u";
        var info = node.Item is MibLeaf l ? resolver.Resolve(node, l) : null;
        var @interface = info is not null ? $"ISnmpLeaf<{info.LeafTypeArgument}>" : "IBranchIdentifier";
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

        if (node.Item is MibLeaf leaf && info is not null)
        {
            foreach (var line in LeafValueMembers(leaf, info, name, ownsInlineEnum: false))
            {
                yield return $"\t{line}";
            }

            yield return $"\t{LeafParse(info, "Oid")}";
        }

        foreach (var child in node.Children.Values.OrderBy(child => child.Arc))
        {
            yield return "";
            foreach (var line in NestedNode(child, $"{name}.Oid", resolver))
            {
                yield return $"\t{line}";
            }
        }

        yield return "}";
    }

    private static string[] NotificationType(string @namespace, string name, OidTreeNode node,
        MibNotification notification, ValueTypeResolver resolver)
    {
        var oid = DotForm(node);
        var infos = notification.Objects.Select(resolver.Resolve).ToArray();
        var objects = notification.Objects
            .Select((obj, index) => (
                Object: obj,
                Name: NotificationObjectName(notification, obj, index, name),
                Table: ContainingTable(node, obj)))
            .ToArray();
        var tables = objects
            .Where(obj => obj.Table is not null)
            .Select(obj => obj.Table!)
            .Distinct()
            .ToArray();
        var tableObjects = objects
            .Select((obj, index) => (Object: obj, Index: index))
            .Where(obj => obj.Object.Table is not null)
            .ToArray();
        var tableGroups = tableObjects
            .GroupBy(obj => obj.Object.Table!)
            .ToArray();
        var typedIndexProperties = tables.SelectMany(indexTable => NotificationIndexProperties(
            indexTable,
            [.. objects.Select(obj => obj.Name), .. ReservedNotificationMemberNames]));
        var notificationIndexAssignment = tableGroups.Length switch
        {
            0 => Enumerable.Empty<string>(),
            1 =>
            [
                "\t\t\tif (trapIndexes.Length == 0)",
                "\t\t\t{",
                "\t\t\t\tnotificationIndexes = tableIndexes0;",
                "\t\t\t}",
                "\t\t\telse if (!tableIndexes0.SequenceEqual(notificationIndexes)) return null;"
            ],
            _ =>
            [
                "\t\t\tif (trapIndexes.Length == 0)",
                "\t\t\t{",
                .. tableGroups.Skip(1).Select((_, groupIndex) =>
                    $"\t\t\t\tif (!tableIndexes0.SequenceEqual(tableIndexes{groupIndex + 1})) return null;"),
                "\t\t\t\tnotificationIndexes = tableIndexes0;",
                "\t\t\t}",
                .. tableGroups.Select((_, groupIndex) =>
                    $"\t\t\telse if (!tableIndexes{groupIndex}.SequenceEqual(notificationIndexes)) return null;")
            ]
        };
        var tableIndexValidation = tableGroups.SelectMany((group, groupIndex) =>
            new[] { $"\t\t\tvar tableIndexes{groupIndex} = indexes{group.First().Index};" }
                .Concat(group.Skip(1).Select(obj =>
                    $"\t\t\tif (!tableIndexes{groupIndex}.SequenceEqual(indexes{obj.Index})) return null;"))
                .Append(
                    $"\t\t\tif (!{NotificationIndexValidatorName(group.Key)}(tableIndexes{groupIndex})) return null;"));

        List<string> lines =
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
            .. objects.Select((obj, index) =>
                $"\t\tpublic required {infos[index].CsType} {obj.Name}; "),
            .. objects.Select(obj =>
                $"\t\tprivate static Oid {obj.Name}Id = {obj.Object.Ident.AsLiteral()}; "),
            "",
            "\t\tpublic uint[] NotificationIndexes { get; }",
            "",
            "\t\t/// <summary>",
            "\t\t/// Variable bindings present in the notification that are not declared by the MIB OBJECTS clause,",
            "\t\t/// in their original order. They are appended after the declared objects by <see cref=\"Populate\"/>.",
            "\t\t/// </summary>",
            "\t\tpublic VbCollection AdditionalBindings { get; init; } = new();",
            "",
            "\t\tpublic Oid InstanceOid => new Oid((uint[])[.. Oid.ToArray(), .. NotificationIndexes]);",
            "",
            $"\t\tpublic {name}() : this([]) {{ }}",
            "",
            $"\t\tpublic {name}(ReadOnlySpan<uint> notificationIndexes)",
            "\t\t{",
            "\t\t\tNotificationIndexes = [.. notificationIndexes];",
            "\t\t}",
            "",
            .. tables.SelectMany(table => NotificationIndexValidator(table).Select(line => $"\t\t{line}")),
            .. typedIndexProperties.Select(line => $"\t\t{line}"),
            "",
            $"\t\tpublic static {name}? Parse(Pdu pdu)",
            "\t\t{",
            "\t\t\tif (pdu.Type is not (PduType.V2Trap or PduType.Inform) || !Oid.IsRootOf(pdu.TrapObjectID)) return null;",
            "\t\t\tvar trapIndexes = pdu.TrapObjectID[Oid.Length..];",
            "\t\t\tvar values = pdu.VbList;",
            $"\t\t\tif (values.Count < {objects.Length}) return null;",
            "\t\t\tvar additionalBindings = new VbCollection();",
            "\t\t\tvar cursor = 0;",
            .. objects.SelectMany((obj, index) => NotificationObjectScan(obj, index)),
            "\t\t\tfor (; cursor < values.Count; cursor++) additionalBindings.Add(values[cursor]);",
            "\t\t\tvar notificationIndexes = trapIndexes;",
            .. tableObjects.Select(obj =>
                $"\t\t\tvar indexes{obj.Index} = oid{obj.Index}[{obj.Object.Name}Id.Length..];"),
            .. tableIndexValidation,
            .. notificationIndexAssignment,
            $"\t\t\treturn new {name}(notificationIndexes){{",
            .. objects.Select((obj, index) => $"\t\t\t\t{obj.Name} = {infos[index].FromAsn($"value{index}")},"),
            "\t\t\t\tAdditionalBindings = additionalBindings,",
            "\t\t\t};",
            "\t\t}",
            "",
            "\t\tpublic void Populate(VbCollection values)",
            "\t\t{",
            .. tables.Select(table =>
                $"\t\t\tif (!{NotificationIndexValidatorName(table)}(NotificationIndexes)) throw new InvalidOperationException(\"NotificationIndexes does not encode a valid {table.EntryType()} index.\");"),
            .. objects.Select((obj, index) =>
                obj.Table is null
                    ? $"\t\t\tvalues.Add({obj.Name}Id + 0u, {infos[index].ToAsn(obj.Name)});"
                    : $"\t\t\tvalues.Add(new Oid((uint[])[.. {obj.Name}Id.ToArray(), .. NotificationIndexes]), {infos[index].ToAsn(obj.Name)});"),
            "\t\t\tvalues.Add(AdditionalBindings);",
            "\t\t}",
            "",
            "\t}",
            "}"
        ];

        return [.. lines];
    }

    private static MibTable? ContainingTable(OidTreeNode node, MibLeaf leaf)
    {
        var root = node;
        while (root.Parent is not null)
        {
            root = root.Parent;
        }

        var leafNode = root;
        foreach (var arc in leaf.Ident.Oid)
        {
            if (!leafNode.Children.TryGetValue(arc, out leafNode))
            {
                return null;
            }
        }

        return leafNode.AncestorsAndSelf()
            .Select(ancestor => ancestor.Item)
            .OfType<MibTable>()
            .FirstOrDefault();
    }

    private static string NotificationObjectOidMatch(
        (MibLeaf Object, string Name, MibTable? Table) obj,
        string oidName)
    {
        var id = $"{obj.Name}Id";
        if (obj.Table is null)
        {
            // Scalars are accepted both with the SMIv2 ".0" instance suffix and without it,
            // since some agents (e.g. for lldpRemTablesChange) omit the instance identifier.
            return
                $"({id}.IsRootOf({oidName}) && ({oidName}.Length == {id}.Length || ({oidName}.Length == {id}.Length + 1 && {oidName}[{id}.Length] == 0u)))";
        }

        return $"({id}.IsRootOf({oidName}) && {oidName}.Length > {id}.Length)";
    }

    private static readonly string[] ReservedNotificationMemberNames = ["NotificationIndexes", "AdditionalBindings"];

    // Scans forward from the cursor for the next binding matching the declared object; any bindings
    // skipped along the way are not declared by the MIB and are kept as additional bindings.
    private static IEnumerable<string> NotificationObjectScan(
        (MibLeaf Object, string Name, MibTable? Table) obj,
        int index)
    {
        var type = obj.Object.Type.AsAsnType();
        yield return $"\t\t\tOid? oid{index} = null;";
        yield return $"\t\t\t{type}? value{index} = null;";
        yield return "\t\t\tfor (; cursor < values.Count; cursor++)";
        yield return "\t\t\t{";
        yield return "\t\t\t\tvar vb = values[cursor];";
        yield return
            $"\t\t\t\tif (vb.Oid is {{ }} candidate{index} && {NotificationObjectOidMatch(obj, $"candidate{index}")} && vb.Value is {type} typed{index})";
        yield return "\t\t\t\t{";
        yield return $"\t\t\t\t\toid{index} = candidate{index};";
        yield return $"\t\t\t\t\tvalue{index} = typed{index};";
        yield return "\t\t\t\t\tcursor++;";
        yield return "\t\t\t\t\tbreak;";
        yield return "\t\t\t\t}";
        yield return "\t\t\t\tadditionalBindings.Add(vb);";
        yield return "\t\t\t}";
        yield return $"\t\t\tif (oid{index} is null || value{index} is null) return null;";
    }

    private static string NotificationIndexValidatorName(MibTable table) =>
        $"ValidateIndexesFor{table.Ident.Name}";

    private static IEnumerable<string> NotificationIndexValidator(MibTable table)
    {
        var strip = table.EntryType();
        yield return $"private static bool {NotificationIndexValidatorName(table)}(ReadOnlySpan<uint> index)";
        yield return "{";
        foreach (var line in Templating.IndexDecodeStatements(table, strip, "index", "return false;"))
        {
            yield return $"\t{line}";
        }

        yield return "\treturn index.IsEmpty;";
        yield return "}";
    }

    private static IEnumerable<string> NotificationIndexProperties(MibTable table, string[] objectNames)
    {
        var strip = table.CommonPrefix();
        var order = 0;
        foreach (var index in table.Index)
        {
            var name = index.Ident.CSharpName(strip);
            if (string.IsNullOrEmpty(name)) name = "Id";
            var propertyName = name;
            while (objectNames.Contains(propertyName, StringComparer.Ordinal))
            {
                propertyName = $"Index{propertyName}";
            }

            var indexor = index.Type.UintCount switch
            {
                1 => $"{order}",
                _ => $"{order}.."
            };

            yield return
                $"public {index.Type.AsUintType()}? {propertyName} => NotificationIndexes.Length > {order} ? NotificationIndexes[{indexor}] : default;";
            order++;
        }
    }

    private static string NotificationObjectName(MibNotification notification, MibLeaf obj, int index,
        string notificationName)
    {
        var baseName = obj.Ident.CSharpName(notificationName);
        var occurrence = notification.Objects
            .Take(index + 1)
            .Count(previous => previous.Ident.Equals(obj.Ident));
        var name = occurrence == 1 ? baseName : $"{baseName}{occurrence}";
        return ReservedNotificationMemberNames.Contains(name, StringComparer.Ordinal) ? $"Value{name}" : name;
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