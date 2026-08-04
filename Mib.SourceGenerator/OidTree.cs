using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SnmpSharpNet.Mib.SourceGenerator;

internal sealed class OidTreeNode
{
    public OidTreeNode(uint arc, OidTreeNode? parent)
    {
        Arc = arc;
        Parent = parent;
    }

    public uint Arc { get; }
    public OidTreeNode? Parent { get; }
    public string? RawName { get; private set; }
    public string? ModuleName { get; private set; }
    public MibItem? Item { get; private set; }
    public Dictionary<uint, OidTreeNode> Children { get; } = [];

    public bool IsValue => Item is MibLeaf or MibTable;
    public bool IsNamespace => !IsValue;

    public void Define(string? rawName, string moduleName, MibItem? item)
    {
        if (RawName is null && !string.IsNullOrWhiteSpace(rawName))
        {
            RawName = rawName;
        }

        if (ModuleName is null)
        {
            ModuleName = moduleName;
        }

        if (item is not null)
        {
            Item = item;
        }
    }

    public IEnumerable<OidTreeNode> AncestorsAndSelf()
    {
        var stack = new Stack<OidTreeNode>();
        for (var node = this; node is not null; node = node.Parent)
        {
            stack.Push(node);
        }

        return stack;
    }

    public bool HasValueAncestor() =>
        Parent is not null && Parent.AncestorsAndSelf().Any(node => node.IsValue);
}

internal static class OidTreeBuilder
{
    public static OidTreeNode Build(IEnumerable<KeyValuePair<string, MibModule>> modules)
    {
        var root = new OidTreeNode(0, null);

        foreach (var moduleEntry in modules)
        {
            var moduleName = moduleEntry.Key;
            var module = moduleEntry.Value;
            foreach (var oidEntry in module.AllOids)
            {
                var name = oidEntry.Key;
                var ident = oidEntry.Value;
                module.AllItems.TryGetValue(ident, out var item);
                Insert(root, ident, name, moduleName, item);
            }

            // Table columns are removed from MibModule.Items, but they still belong in the tree.
            foreach (var itemEntry in module.AllItems)
            {
                var ident = itemEntry.Key;
                var item = itemEntry.Value;
                Insert(root, ident, item.Ident.Name, moduleName, item);
            }
        }

        return root;
    }

    private static void Insert(
        OidTreeNode root,
        MibItemIdent ident,
        string? finalName,
        string moduleName,
        MibItem? item)
    {
        var namesOffset = ident.Oid.Length - ident.OidNames.Length;
        var node = root;

        for (var index = 0; index < ident.Oid.Length; index++)
        {
            var arc = ident.Oid[index];
            if (!node.Children.TryGetValue(arc, out var child))
            {
                child = new OidTreeNode(arc, node);
                node.Children.Add(arc, child);
            }

            node = child;
            var rawName = index >= namesOffset ? ident.OidNames[index - namesOffset] : null;
            if (index == ident.Oid.Length - 1 && !string.IsNullOrWhiteSpace(finalName))
            {
                rawName = finalName;
            }

            node.Define(rawName, moduleName, index == ident.Oid.Length - 1 ? item : null);
        }
    }
}

internal static class OidTreeNaming
{
    private static readonly IReadOnlyDictionary<string, string> CanonicalArcs =
        new Dictionary<string, string>
        {
            ["0"] = "Itu",
            ["1"] = "Iso",
            ["2"] = "JointIsoItu",
            ["1.3"] = "Org",
            ["1.3.6"] = "Dod",
            ["1.3.6.1"] = "Internet",
            ["1.3.6.1.1"] = "Directory",
            ["1.3.6.1.2"] = "Mgmt",
            ["1.3.6.1.2.1"] = "Mib2",
            ["1.3.6.1.3"] = "Experimental",
            ["1.3.6.1.4"] = "Private",
            ["1.3.6.1.4.1"] = "Enterprises",
            ["1.3.6.1.5"] = "Security",
            ["1.3.6.1.6"] = "SnmpV2",
        };

    private static readonly HashSet<string> Keywords =
    [
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while"
    ];

    public static string NamespaceFor(OidTreeNode node) =>
        string.Join(".", node.AncestorsAndSelf().Skip(1).Select(NamespaceSegment).Prepend("Snmp"));

    public static string NamespaceSegment(OidTreeNode node)
    {
        if (node is { Item: MibModuleInfo, ModuleName: not null })
        {
            return ModuleName(node.ModuleName);
        }

        var oid = string.Join(".", node.AncestorsAndSelf().Skip(1).Select(x => x.Arc));
        if (CanonicalArcs.TryGetValue(oid, out var canonical))
        {
            return canonical;
        }

        return FormatIdentifier(TrimModulePrefix(node.RawName, node.ModuleName) ?? $"Arc{node.Arc}");
    }

    public static string TypeName(OidTreeNode node) =>
        FormatIdentifier(TrimModulePrefix(node.RawName, node.ModuleName) ?? $"Arc{node.Arc}");

    public static string ModuleName(string moduleName)
    {
        const string suffix = "-MIB";
        var name = moduleName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? moduleName.Substring(0, moduleName.Length - suffix.Length)
            : moduleName;
        return string.Concat(name
            .Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(FormatIdentifier));
    }

    private static string? TrimModulePrefix(string? name, string? moduleName)
    {
        if (name is null || string.IsNullOrWhiteSpace(name) || name.StartsWith("<", StringComparison.Ordinal))
        {
            return null;
        }

        if (moduleName is null || string.IsNullOrWhiteSpace(moduleName))
        {
            return name;
        }

        var modulePrefix = moduleName
            .Replace("-", string.Empty)
            .Replace("_", string.Empty);
        modulePrefix = RemoveIgnoreCase(modulePrefix, "MIB");

        if (name.StartsWith(modulePrefix, StringComparison.OrdinalIgnoreCase)
            && name.Length > modulePrefix.Length)
        {
            return name.Substring(modulePrefix.Length);
        }

        return name;
    }

    public static string FormatIdentifier(string value)
    {
        var characters = value
            .Where(char.IsLetterOrDigit)
            .ToArray();

        if (characters.Length == 0)
        {
            return "Unnamed";
        }

        var normalized = new string(characters);
        if (char.IsDigit(normalized[0]))
        {
            normalized = $"Arc{normalized}";
        }

        if (normalized.Equals(normalized.ToUpperInvariant(), StringComparison.Ordinal))
        {
            normalized = normalized.ToLowerInvariant();
        }

        normalized = char.ToUpper(normalized[0], CultureInfo.InvariantCulture)
                     + normalized.Substring(1);
        return Keywords.Contains(normalized) ? $"@{normalized}" : normalized;
    }

    private static string RemoveIgnoreCase(string source, string value)
    {
        var index = source.IndexOf(value, StringComparison.OrdinalIgnoreCase);
        return index < 0
            ? source
            : source.Remove(index, value.Length);
    }
}