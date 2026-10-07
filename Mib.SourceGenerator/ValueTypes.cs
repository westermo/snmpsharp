using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace SnmpSharpNet.Mib.SourceGenerator;

internal enum ValueKind
{
    Asn,
    Enum,
    Flags
}

/// <summary>Describes the C# type used to expose a MIB object and how to convert it to and from its ASN.1 type.</summary>
internal sealed class ValueTypeInfo(ValueKind kind, string asnType, string csType, int bitsOctets)
{
    public ValueKind Kind { get; } = kind;
    public string AsnType { get; } = asnType;
    public string CsType { get; } = csType;
    public int BitsOctets { get; } = bitsOctets;

    public bool IsConverted => Kind != ValueKind.Asn;

    /// <summary>Type argument for <c>ISnmpLeaf&lt;T&gt;</c>; value types are wrapped in <see cref="Nullable{T}"/>.</summary>
    public string LeafTypeArgument => IsConverted ? $"{CsType}?" : CsType;

    /// <summary>Converts an expression of type <see cref="AsnType"/> into <see cref="CsType"/>.</summary>
    public string FromAsn(string expression) => Kind switch
    {
        ValueKind.Enum => $"({CsType}){expression}.Value",
        ValueKind.Flags => $"({CsType})global::SnmpSharpNet.SnmpBits.ToMask({expression})",
        _ => expression
    };

    /// <summary>Converts an expression of type <see cref="CsType"/> into <see cref="AsnType"/>.</summary>
    public string ToAsn(string expression) => Kind switch
    {
        ValueKind.Enum => $"new Integer32((int){expression})",
        ValueKind.Flags => $"global::SnmpSharpNet.SnmpBits.FromMask((ulong){expression}, {BitsOctets})",
        _ => expression
    };

    public static ValueTypeInfo ForAsn(MibType type) => new(ValueKind.Asn, type.AsAsnType(), type.AsAsnType(), 0);
}

internal sealed class EnumMember(string name, string label, long value)
{
    public string Name { get; } = name;
    public string Label { get; } = label;
    public long Value { get; } = value;
}

internal static class ValueTypes
{
    public const string TextualConventionsNamespace = "Snmp.TextualConventions";

    public static bool IsEnum(MibType type) =>
        type.Kind == TypeKind.Integer32Enum
        && type.Values is { Count: > 0 } values
        && values.Values.All(value => value is >= int.MinValue and <= int.MaxValue);

    public static bool IsFlags(MibType type) =>
        type.Kind == TypeKind.Bits
        && type.Values is { Count: > 0 } values
        && values.Values.All(value => value is >= 0 and <= 63);

    public static ValueKind KindOf(MibType type) =>
        IsEnum(type) ? ValueKind.Enum : IsFlags(type) ? ValueKind.Flags : ValueKind.Asn;

    /// <summary>True when the type is an enum/BITS declared through a textual convention with a known module.</summary>
    public static bool IsTextualConventionEnum(MibType type) =>
        KindOf(type) != ValueKind.Asn && type.TextualConvention is { Module: not null };

    /// <summary>True when the type is an enum/BITS that has to be generated next to the object using it.</summary>
    public static bool IsInlineEnum(MibType type) =>
        KindOf(type) != ValueKind.Asn && type.TextualConvention is not { Module: not null };

    public static string TextualConventionNamespace(MibTextualConvention tc) =>
        $"{TextualConventionsNamespace}.{OidTreeNaming.ModuleName(tc.Module!)}";

    public static int BitsOctets(MibType type) =>
        type.Values is { Count: > 0 } values ? (int)(values.Values.Max() / 8) + 1 : 0;

    /// <summary>Name of the inline enum nested in a leaf class.</summary>
    public static string LeafEnumName(MibType type, string leafClassName)
    {
        var name = KindOf(type) == ValueKind.Flags ? "Bits" : "Values";
        return name == leafClassName ? $"{name}Enum" : name;
    }

    /// <summary>Name of the inline enum nested in a table entry class for the given column.</summary>
    public static string ColumnEnumName(MibType type, string columnName) =>
        columnName + (KindOf(type) == ValueKind.Flags ? "Bits" : "Values");

    public static IReadOnlyList<EnumMember> Members(MibType type, string typeName)
    {
        var used = new HashSet<string>(StringComparer.Ordinal) { typeName };
        // BITS enums always get a synthetic None = 0, so a MIB label formatting to None is suffixed.
        if (KindOf(type) == ValueKind.Flags) used.Add("None");
        var members = new List<EnumMember>();
        foreach (var pair in (type.Values ?? new Dictionary<string, long>()).OrderBy(pair => pair.Value))
        {
            var baseName = MemberName(pair.Key);
            var name = baseName;
            if (!used.Add(name))
            {
                var suffix = pair.Value < 0
                    ? $"Minus{(-pair.Value).ToString(CultureInfo.InvariantCulture)}"
                    : pair.Value.ToString(CultureInfo.InvariantCulture);
                name = $"{baseName}_{suffix}";
                var counter = 2;
                while (!used.Add(name))
                {
                    name = $"{baseName}_{suffix}_{counter++}";
                }
            }

            members.Add(new EnumMember(name, pair.Key, pair.Value));
        }

        return members;
    }

    /// <summary>Formats a MIB enumeration label as a PascalCase C# identifier, e.g. <c>transparent-only</c> → <c>TransparentOnly</c>.</summary>
    public static string MemberName(string label)
    {
        var builder = new StringBuilder();
        foreach (var part in label.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries))
        {
            var characters = part.Where(char.IsLetterOrDigit).ToArray();
            if (characters.Length == 0) continue;
            builder.Append(char.ToUpper(characters[0], CultureInfo.InvariantCulture));
            builder.Append(characters, 1, characters.Length - 1);
        }

        if (builder.Length == 0) return "Unnamed";
        if (char.IsDigit(builder[0])) builder.Insert(0, '_');
        return builder.ToString();
    }

    /// <summary>Emits an enum declaration (without indentation) for an INTEGER enumeration or BITS type.</summary>
    public static IEnumerable<string> EnumDeclaration(string name, MibType type, params string?[] description)
    {
        var flags = KindOf(type) == ValueKind.Flags;
        var members = Members(type, name);
        foreach (var line in Templating.DocComment(description)) yield return line;
        yield return Templating.Attribute;
        if (flags) yield return "[global::System.Flags]";
        yield return $"public enum {name} : {(flags ? "ulong" : "int")}";
        yield return "{";
        if (flags)
        {
            yield return "\t/// <summary>No bits set.</summary>";
            yield return "\tNone = 0,";
        }

        foreach (var member in members)
        {
            yield return $"\t/// <summary><c>{member.Label}({member.Value.ToString(CultureInfo.InvariantCulture)})</c></summary>";
            yield return flags
                ? $"\t{member.Name} = 1UL << {member.Value.ToString(CultureInfo.InvariantCulture)},"
                : $"\t{member.Name} = {member.Value.ToString(CultureInfo.InvariantCulture)},";
        }

        yield return "}";
    }

    /// <summary>Factory expression for the DEFVAL of a leaf, typed as <see cref="ValueTypeInfo.CsType"/>.</summary>
    public static string? DefaultFactory(MibLeaf leaf, ValueTypeInfo info)
    {
        if (leaf.DefaultValue is not { } value) return null;
        switch (info.Kind)
        {
            case ValueKind.Enum when value.Kind == MibDefaultValueKind.Number:
                return $"({info.CsType})({value.Number!.Value.ToString(CultureInfo.InvariantCulture)})";
            case ValueKind.Flags when value.Kind == MibDefaultValueKind.Bits:
            {
                ulong mask = 0;
                foreach (var bit in value.Names)
                {
                    if (leaf.Type.Values is { } values && values.TryGetValue(bit, out var position))
                    {
                        mask |= 1UL << (int)position;
                    }
                }

                return $"({info.CsType})0x{mask.ToString("X", CultureInfo.InvariantCulture)}UL";
            }
            case ValueKind.Asn:
                return Templating.DefaultFactory(leaf);
            default:
                return null;
        }
    }

    /// <summary>
    /// Emits range (<c>MinValue</c>/<c>MaxValue</c>/<c>IsValidValue</c>) or size
    /// (<c>MinSize</c>/<c>MaxSize</c>/<c>IsValidSize</c>) members for a refined type.
    /// </summary>
    public static IEnumerable<string> ConstraintMembers(MibType type, string prefix = "")
    {
        if (KindOf(type) != ValueKind.Asn) yield break;
        if (type.Refinement is not { Constraints.Count: > 0 } refinement) yield break;

        var min = refinement.Constraints.Min(range => range.Min);
        var max = refinement.Constraints.Max(range => range.Max);
        var ranges = string.Join(" | ", refinement.Constraints.Select(range => range.Min == range.Max
            ? Format(range.Min)
            : $"{Format(range.Min)}..{Format(range.Max)}"));
        if (refinement.IsSize)
        {
            yield return $"/// <summary>Smallest allowed size in octets. Allowed sizes: <c>SIZE ({ranges})</c>.</summary>";
            yield return $"public const int {prefix}MinSize = {Format(ClampToInt(min))};";
            yield return $"/// <summary>Largest allowed size in octets. Allowed sizes: <c>SIZE ({ranges})</c>.</summary>";
            yield return $"public const int {prefix}MaxSize = {Format(ClampToInt(max))};";
            yield return $"/// <summary>Checks a size against <c>SIZE ({ranges})</c>.</summary>";
            yield return $"public static bool IsValid{prefix}Size(int size) => {Condition("size", refinement, "", ClampToInt)};";
        }
        else
        {
            yield return $"/// <summary>Smallest allowed value. Allowed values: <c>({ranges})</c>.</summary>";
            yield return $"public const long {prefix}MinValue = {Format(min)}L;";
            yield return $"/// <summary>Largest allowed value. Allowed values: <c>({ranges})</c>.</summary>";
            yield return $"public const long {prefix}MaxValue = {Format(max)}L;";
            yield return $"/// <summary>Checks a value against <c>({ranges})</c>.</summary>";
            yield return $"public static bool IsValid{prefix}Value(long value) => {Condition("value", refinement, "L", value => value)};";
        }
    }

    private static string Condition(string variable, Refinement refinement, string suffix, Func<long, long> clamp) =>
        string.Join(" || ", refinement.Constraints.Select(range => range.Min == range.Max
            ? $"{variable} == {Format(clamp(range.Min))}{suffix}"
            : $"({variable} >= {Format(clamp(range.Min))}{suffix} && {variable} <= {Format(clamp(range.Max))}{suffix})"));

    private static long ClampToInt(long value) => Math.Max(int.MinValue, Math.Min(int.MaxValue, value));

    private static string Format(long value) => value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Assigns unique C# type names to textual convention enums. Distinct conventions whose names normalize to the
/// same identifier (e.g. <c>Foo-Bar</c> and <c>FooBar</c>) get deterministic <c>_2</c>, <c>_3</c>... suffixes.
/// </summary>
internal sealed class TextualConventionNames
{
    private readonly Dictionary<(string Module, string Name), string> names = new();

    public TextualConventionNames(IEnumerable<MibTextualConvention> conventions)
    {
        var keys = conventions
            .Where(tc => tc.Module is not null)
            .Select(tc => (Module: tc.Module!, tc.Name))
            .Distinct()
            .OrderBy(key => key.Module, StringComparer.Ordinal)
            .ThenBy(key => key.Name, StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            var @namespace = $"{ValueTypes.TextualConventionsNamespace}.{OidTreeNaming.ModuleName(key.Module)}";
            var baseName = OidTreeNaming.FormatIdentifier(key.Name);
            var name = baseName;
            for (var i = 2; !used.Add($"{@namespace}.{name}"); i++)
            {
                name = $"{baseName.TrimStart('@')}_{i}";
            }

            names[key] = name;
        }
    }

    public string TypeName(MibTextualConvention tc) =>
        tc.Module is not null && names.TryGetValue((tc.Module, tc.Name), out var name)
            ? name
            : OidTreeNaming.FormatIdentifier(tc.Name);
}

/// <summary>Maps MIB objects onto the generated C# types that expose their values.</summary>
internal sealed class ValueTypeResolver(OidTreeNode root, Compilation compilation, TextualConventionNames tcNames)
{
    public ValueTypeInfo Resolve(MibLeaf leaf)
    {
        var node = Find(leaf.Ident);
        return Resolve(node, node?.Item as MibLeaf ?? leaf);
    }

    public ValueTypeInfo Resolve(OidTreeNode? node, MibLeaf leaf)
    {
        var type = leaf.Type;
        var kind = ValueTypes.KindOf(type);
        if (kind == ValueKind.Asn) return ValueTypeInfo.ForAsn(type);

        var csType = type.TextualConvention is { Module: not null } tc
            ? $"global::{ValueTypes.TextualConventionNamespace(tc)}.{tcNames.TypeName(tc)}"
            : InlineEnumType(node, leaf);
        return csType is null
            ? ValueTypeInfo.ForAsn(type)
            : new ValueTypeInfo(kind, type.AsAsnType(), csType, ValueTypes.BitsOctets(type));
    }

    private string? InlineEnumType(OidTreeNode? node, MibLeaf leaf)
    {
        if (node is null) return null;

        if (!node.HasValueAncestor())
        {
            var @namespace = OidTreeNaming.NamespaceFor(node.Parent!);
            var className = OidTreeNaming.TypeName(node);
            return Owned($"{@namespace}.{className}", $"{@namespace}.{className}",
                ValueTypes.LeafEnumName(leaf.Type, className));
        }

        if (node.Parent is { Arc: 1, Parent: { Item: MibTable table } tableNode } && !tableNode.HasValueAncestor())
        {
            var @namespace = OidTreeNaming.NamespaceFor(tableNode.Parent!);
            var column = leaf.Ident.CSharpName(table.CommonPrefix());
            return Owned($"{@namespace}.{OidTreeNaming.TypeName(tableNode)}", $"{@namespace}.{table.EntryType()}",
                ValueTypes.ColumnEnumName(leaf.Type, column));
        }

        return null;
    }

    // Types that already exist in the compilation are not generated; only reference the nested
    // enum when it is either going to be generated or has been declared by the user.
    private string? Owned(string generatedType, string owner, string enumName)
    {
        if (compilation.GetTypeByMetadataName(generatedType) is not null
            && compilation.GetTypeByMetadataName($"{owner}+{enumName}") is null)
        {
            return null;
        }

        return $"global::{owner}.{enumName}";
    }

    private OidTreeNode? Find(MibItemIdent ident)
    {
        var node = root;
        foreach (var arc in ident.Oid)
        {
            if (!node.Children.TryGetValue(arc, out node)) return null;
        }

        return node;
    }
}
