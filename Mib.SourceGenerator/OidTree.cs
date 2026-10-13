using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SnmpSharpNet.Mib.SourceGenerator;

internal static class MibTreeNodeExtensions
{
    public static bool IsNamespace(this MibTreeNode node) => !node.IsValue;

    public static bool HasValueAncestor(this MibTreeNode node) =>
        node.Parent is not null && node.Parent.AncestorsAndSelf().Any(ancestor => ancestor.IsValue);
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

    public static string NamespaceFor(MibTreeNode node) =>
        string.Join(".", node.AncestorsAndSelf().Skip(1).Select(NamespaceSegment).Prepend("Snmp"));

    public static string NamespaceSegment(MibTreeNode node)
    {
        if (node is { Item: MibModuleInfo, ModuleName: not null })
        {
            return ModuleName(node.ModuleName);
        }

        var oid = string.Join(".", node.AncestorsAndSelf().Skip(1).Select(x => x.Arc));
        return CanonicalArcs.TryGetValue(oid, out var canonical)
            ? canonical
            : FormatIdentifier(TrimModulePrefix(node.Name, node.ModuleName) ?? $"Arc{node.Arc}");
    }

    public static string TypeName(MibTreeNode node)
    {
        var oid = string.Join(".", node.AncestorsAndSelf().Skip(1).Select(x => x.Arc));
        return CanonicalArcs.TryGetValue(oid, out var canonical)
            ? canonical
            : FormatIdentifier(TrimModulePrefix(node.Name, node.ModuleName) ?? $"Arc{node.Arc}");
    }

    public static string ModuleName(string moduleName)
    {
        const string suffix = "-MIB";
        var name = moduleName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? moduleName.Substring(0, moduleName.Length - suffix.Length)
            : moduleName;
        return string.Concat(name
            .Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries)
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