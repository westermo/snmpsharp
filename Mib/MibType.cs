using System;
using System.Collections.Generic;
using System.Linq;
using SnmpSharpNet.Mib.Ast;

namespace SnmpSharpNet.Mib;

public class Refinement : IEquatable<Refinement>
{
    public IReadOnlyList<(long Min, long Max)> Constraints { get; }
    public bool IsSize { get; }

    public Refinement(IReadOnlyList<(long Min, long Max)> contraints, bool isSize = false)
    {
        var normalized = contraints
            .OrderBy(x => x.Min)
            .ThenBy(x => x.Max)
            .ToArray();

        Constraints = normalized;
        IsSize = isSize;
    }

    public static Refinement AllValues = new([]);
    public static Refinement AllSizes = new([], true);

    /// <summary>
    /// Union: accumulates constraints from both sides (value is in left OR right).
    /// Used by <see cref="MibType.WithConstraint"/> to build up allowed ranges.
    /// </summary>
    public static Refinement? Union(Refinement? left, Refinement? right)
    {
        if (left is null && right is null) return null;
        if (left is null || right is null)
        {
            throw new InvalidOperationException("Cannot merge refineable and non-refineable types.");
        }

        if (left.IsSize != right.IsSize)
        {
            throw new InvalidOperationException("Cannot merge size and value refinements.");
        }

        return new Refinement([
            ..left.Constraints,
            ..right.Constraints,
        ], left.IsSize);
    }

    /// <summary>
    /// Intersection: narrows constraints so that only values allowed by both sides remain.
    /// Per RFC 2578 §11, a subtype refinement must narrow the parent type's range.
    /// An empty constraint list means "all values allowed" (unconstrained).
    /// </summary>
    public static Refinement? Merge(Refinement? left, Refinement? right)
    {
        if (left is null && right is null) return null;
        if (left is null || right is null)
        {
            throw new InvalidOperationException("Cannot merge refineable and non-refineable types.");
        }

        if (left.IsSize != right.IsSize)
        {
            throw new InvalidOperationException("Cannot merge size and value refinements.");
        }

        // Unconstrained intersected with anything yields the other side
        if (left.Constraints.Count == 0) return right;
        if (right.Constraints.Count == 0) return left;

        // Compute pairwise range intersections
        var result = new List<(long Min, long Max)>();
        foreach (var l in left.Constraints)
        {
            foreach (var r in right.Constraints)
            {
                var min = Math.Max(l.Min, r.Min);
                var max = Math.Min(l.Max, r.Max);
                if (min <= max)
                {
                    result.Add((min, max));
                }
            }
        }

        return new Refinement(result, left.IsSize);
    }

    public bool Equals(Refinement? other) =>
        other is not null
        && IsSize == other.IsSize
        && Constraints.SequenceEqual(other.Constraints);

    public override bool Equals(object? obj) => Equals(obj as Refinement);

    public override int GetHashCode() =>
        HashCode.Combine(IsSize, Constraints.SequenceHash());
}

public enum TypeKind
{
    Integer32Enum,
    Integer32,
    Unsigned32,
    OctetString,
    IpAddress,
    ObjectIdentifier,
    Opaque,
    Bits,
    Counter32,
    Counter64,
    Gauge32,
    TimeTicks
}

public sealed class MibTextualConvention(
    string name,
    string? displayHint,
    SMIv2Status status,
    string description,
    string? reference)
{
    public string Name { get; } = name;
    public string? DisplayHint { get; } = displayHint;
    public SMIv2Status Status { get; } = status;
    public string Description { get; } = description;
    public string? Reference { get; } = reference;
}

public class MibType(
    TypeKind kind,
    Refinement? refinement = null,
    IReadOnlyDictionary<string, long>? values = null,
    string? name = null,
    MibTextualConvention? textualConvention = null
) : IEquatable<MibType> {
    public TypeKind Kind { get; } = kind;
    public Refinement? Refinement { get; } = refinement;
    public IReadOnlyDictionary<string, long>? Values { get; } = values;
    public string? Name { get; } = name;
    public MibTextualConvention? TextualConvention { get; } = textualConvention;

    public static MibType FromAst(Ast.AstType type)
    {
        return new MibType(
            type.Kind ?? throw new ArgumentException($"Type '{type}' is unresolved", nameof(type)),
            type.Refinement,
            type.Values?.ToDictionary(v => v.Item1.ToString(), v => v.Item2),
            type.Name?.ToString(),
            null);
    }

    public static readonly MibType Integer32 = new(TypeKind.Integer32, Refinement.AllValues);
    public static readonly MibType Integer32Enum = new(TypeKind.Integer32Enum);

    public MibType WithNamedValue(string name, long value)
    {
        if (Kind != TypeKind.Integer32Enum)
        {
            throw new InvalidOperationException("WithNamedValue is only valid for Integer32Enum.");
        }

        var values = Values is null
            ? new Dictionary<string, long>()
            : Values.ToDictionary(kv => kv.Key, kv => kv.Value);
        values[name] = value;

        return new MibType(TypeKind.Integer32Enum, values: values);
    }

    public static readonly MibType Unsigned32 = new(TypeKind.Unsigned32, Refinement.AllValues);
    public static readonly MibType OctetString = new(TypeKind.OctetString, Refinement.AllSizes);
    public static readonly MibType IpAddress = new(TypeKind.IpAddress);
    public static readonly MibType ObjectIdentifier = new(TypeKind.ObjectIdentifier);
    public static readonly MibType Opaque = new(TypeKind.Opaque);
    public static readonly MibType Counter32 = new(TypeKind.Counter32);
    public static readonly MibType Counter64 = new(TypeKind.Counter64);
    public static readonly MibType Gauge32 = new(TypeKind.Gauge32, Refinement.AllValues);
    public static readonly MibType TimeTicks = new(TypeKind.TimeTicks);

    public int? UintCount => Kind switch
    {
        TypeKind.OctetString => null,
        TypeKind.Opaque => null,
        TypeKind.Bits => null,
        TypeKind.ObjectIdentifier => null,
        TypeKind.IpAddress => 4,
        _ => 1,
    };

    public bool IsSequence()
    {
        return UintCount != 1;
    }

    public MibType WithRange(long exact)
    {
        return WithRange(exact, exact);
    }

    public MibType WithRange(long min, long max)
    {
        return WithConstraint(min, max, isSize: false);
    }

    public MibType WithSize(long exact)
    {
        return WithSize(exact, exact);
    }

    public MibType WithSize(long min, long max)
    {
        return WithConstraint(min, max, isSize: true);
    }

    private MibType WithConstraint(long min, long max, bool isSize)
    {
        var current = Refinement ?? throw new InvalidOperationException("Type is non-refineable.");
        if (current.IsSize != isSize)
        {
            throw new InvalidOperationException("Cannot mix size and value constraints on same MibType instance.");
        }

        var next = new Refinement([
            (min, max)
        ], isSize);
        var merged = Refinement.Union(current, next);
        return new MibType(Kind, merged, Values, Name, TextualConvention);
    }

    public override string ToString() =>
        Kind switch
        {
            TypeKind.Integer32Enum => $"INTEGER {{{string.Join(", ", Values?.Select(v => $"{v.Key}({v.Value})") ?? [])}}}",
            TypeKind.Integer32 => Refinement?.Constraints.Count > 0 ? $"INTEGER {Refinement}" : "INTEGER",
            TypeKind.Unsigned32 => Refinement?.Constraints.Count > 0 ? $"Unsigned32 {Refinement}" : "Unsigned32",
            TypeKind.OctetString => Refinement?.IsSize == true ? $"OCTET STRING {Refinement}" : "OCTET STRING",
            TypeKind.IpAddress => "IpAddress",
            TypeKind.ObjectIdentifier => "OBJECT IDENTIFIER",
            TypeKind.Opaque => "Opaque",
            TypeKind.Bits => $"BITS {{{string.Join(", ", Values?.Select(v => $"{v.Key}({v.Value})") ?? [])}}}",
            TypeKind.Counter32 => "Counter32",
            TypeKind.Counter64 => "Counter64",
            TypeKind.Gauge32 => Refinement?.Constraints.Count > 0 ? $"Gauge32 {Refinement}" : "Gauge32",
            TypeKind.TimeTicks => "TimeTicks",
            _ => "Unknown"
        };

    public bool Equals(MibType? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Kind == other.Kind
            && Equals(Refinement, other.Refinement)
            && Values.DictEquals(other.Values)
            && Name == other.Name
            && TextualConventionEquals(TextualConvention, other.TextualConvention);
    }

    public override bool Equals(object? obj) => Equals(obj as MibType);

    public override int GetHashCode() => HashCode.Combine(
        HashCode.Combine(Kind, Refinement, Values?.Count ?? 0, Name),
        TextualConvention?.Name);

    private static bool TextualConventionEquals(MibTextualConvention? left, MibTextualConvention? right) =>
        left is null
            ? right is null
            : right is not null &&
              left.Name == right.Name &&
              left.DisplayHint == right.DisplayHint &&
              left.Status == right.Status &&
              left.Description == right.Description &&
              left.Reference == right.Reference;
}
