using System;
using System.Collections.Generic;
using System.Linq;
using SnmpSharpNet.Mib.Ast;

namespace SnmpSharpNet.Mib;

public class MibItem(MibItemIdent ident) : IEquatable<MibItem>
{
    public readonly MibItemIdent Ident = ident;

    public virtual bool Equals(MibItem? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        return Ident.Equals(other.Ident);
    }

    public override bool Equals(object? obj) => Equals(obj as MibItem);
    public override int GetHashCode() => Ident.GetHashCode();
}

public class MibModuleInfo(
    MibItemIdent ident,
    string lastUpdated, string organization,
    string contactInfo, string  description,
    (string, string)[] revisions
): MibItem(ident) {
    public readonly string LastUpdated = lastUpdated;
    public readonly string Organization = organization;
    public readonly string ContactInfo = contactInfo;
    public readonly string  Description = description;
    public readonly (string, string)[] Revisions = revisions;

    public MibModuleInfo(MibItemIdent identifier, ModuleIdentity mi) : this(
        identifier,
        mi.LastUpdated.ToString(), mi.Organization.ToString(),
        mi.ContactInfo.ToString(), mi.Description.ToString(),
        [..mi.Revisions.Select(x => (x.Item1.ToString(), x.Item2.ToString()))]
    ) {}

    public override bool Equals(MibItem? other)
    {
        if (other is not MibModuleInfo o) { return false; }
        return Ident.Equals(o.Ident)
            && LastUpdated == o.LastUpdated
            && Organization == o.Organization
            && ContactInfo == o.ContactInfo
            && Description == o.Description
            && Revisions.SequenceEqual(o.Revisions);
    }
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), LastUpdated);
}

public class MibValuedItem(MibItemIdent ident) : MibItem(ident) {}

public sealed class MibObjectMetadata(
    SMIv2Accessibility accessibility,
    SMIv2Status status,
    string? units,
    string? description,
    string? reference)
{
    public SMIv2Accessibility Accessibility { get; } = accessibility;
    public SMIv2Status Status { get; } = status;
    public string? Units { get; } = units;
    public string? Description { get; } = description;
    public string? Reference { get; } = reference;
}

public enum MibDefaultValueKind
{
    Number,
    Octets,
    ObjectIdentifier,
    Bits
}

/// <summary>A validated DEFVAL expressed independently of mutable ASN.1 runtime values.</summary>
public sealed class MibDefaultValue(
    MibDefaultValueKind kind,
    long? number = null,
    IReadOnlyList<byte>? octets = null,
    IReadOnlyList<uint>? objectIdentifier = null,
    IReadOnlyList<string>? names = null)
{
    public MibDefaultValueKind Kind { get; } = kind;
    public long? Number { get; } = number;
    public IReadOnlyList<byte> Octets { get; } = octets ?? [];
    public IReadOnlyList<uint> ObjectIdentifier { get; } = objectIdentifier ?? [];
    public IReadOnlyList<string> Names { get; } = names ?? [];
}

public readonly struct MibTableIndex(MibLeaf leaf, bool isImplied = false) : IEquatable<MibTableIndex>
{
    public readonly MibLeaf Leaf = leaf;
    public readonly bool IsImplied = isImplied;

    // Convenience accessors — keeps downstream code concise
    public MibItemIdent Ident => Leaf.Ident;
    public MibType Type => Leaf.Type;

    public bool Equals(MibTableIndex other) =>
        Leaf.Equals(other.Leaf) && IsImplied == other.IsImplied;

    public override bool Equals(object obj) =>
        obj is MibTableIndex other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Leaf, IsImplied);
}

public class MibTable(
    MibItemIdent ident,
    MibTableIndex[] index,
    MibLeaf[] columns,
    MibObjectMetadata metadata,
    MibObjectMetadata entryMetadata) : MibValuedItem(ident)
{
    public readonly MibTableIndex[] Index = index;
    public readonly MibLeaf[] Columns = columns;
    public readonly MibObjectMetadata Metadata = metadata;
    public readonly MibObjectMetadata EntryMetadata = entryMetadata;
    public string? Description => Metadata.Description;
    public string? EntryDescription => EntryMetadata.Description;
    public SMIv2Accessibility Accessibility => Metadata.Accessibility;
    public SMIv2Status Status => Metadata.Status;
    public string? Units => Metadata.Units;
    public string? Reference => Metadata.Reference;

    public MibTable(
        MibItemIdent ident,
        MibTableIndex[] index,
        MibLeaf[] columns,
        string? description = null,
        string? entryDescription = null)
        : this(
            ident,
            index,
            columns,
            new MibObjectMetadata(
                SMIv2Accessibility.NotAccessible,
                SMIv2Status.Current,
                null,
                description,
                null),
            new MibObjectMetadata(
                SMIv2Accessibility.NotAccessible,
                SMIv2Status.Current,
                null,
                entryDescription,
                null))
    {
    }

    public override bool Equals(MibItem? other)
    {
        if (other is not MibTable o) { return false; }
        return Ident.Equals(o.Ident)
            && Index.SequenceEqual(o.Index)
            && Columns.SequenceEqual(o.Columns)
            && MetadataEquals(Metadata, o.Metadata)
            && MetadataEquals(EntryMetadata, o.EntryMetadata);
    }
    public override int GetHashCode() => HashCode.Combine(
        base.GetHashCode(),
        Index.SequenceHash(),
        Columns.SequenceHash(),
        HashCode.Combine(Description, EntryDescription, Reference, EntryMetadata.Reference));

    private static bool MetadataEquals(MibObjectMetadata left, MibObjectMetadata right) =>
        left.Accessibility == right.Accessibility &&
        left.Status == right.Status &&
        left.Units == right.Units &&
        left.Description == right.Description &&
        left.Reference == right.Reference;
}

public class MibLeaf(
    MibItemIdent ident,
    MibType type,
    MibObjectMetadata metadata,
    MibDefaultValue? defaultValue = null) : MibValuedItem(ident)
{
    public readonly MibType Type = type;
    public readonly MibObjectMetadata Metadata = metadata;
    public readonly MibDefaultValue? DefaultValue = defaultValue;
    public SMIv2Accessibility Accessibility => Metadata.Accessibility;
    public SMIv2Status Status => Metadata.Status;
    public string? Units => Metadata.Units;
    public string? Description => Metadata.Description;
    public string? Reference => Metadata.Reference;

    public MibLeaf(
        MibItemIdent ident,
        MibType type,
        SMIv2Accessibility accessibility,
        string? description = null)
        : this(
            ident,
            type,
            new MibObjectMetadata(
                accessibility,
                SMIv2Status.Current,
                null,
                description,
                null))
    {
    }

    public override bool Equals(MibItem? other)
    {
        if (other is not MibLeaf o) { return false; }
        return Ident.Equals(o.Ident)
            && Type.Equals(o.Type)
            && Accessibility == o.Accessibility
            && Status == o.Status
            && Units == o.Units
            && Description == o.Description
            && Reference == o.Reference
            && DefaultValueEquals(DefaultValue, o.DefaultValue);
    }
    public override int GetHashCode() => HashCode.Combine(
        HashCode.Combine(base.GetHashCode(), Type, Accessibility, Status),
        HashCode.Combine(Units, Description, Reference, DefaultValue?.Number));

    private static bool DefaultValueEquals(MibDefaultValue? left, MibDefaultValue? right)
    {
        if (left is null || right is null) return left is null && right is null;
        return left.Kind == right.Kind &&
               left.Number == right.Number &&
               left.Octets.SequenceEqual(right.Octets) &&
               left.ObjectIdentifier.SequenceEqual(right.ObjectIdentifier) &&
               left.Names.SequenceEqual(right.Names);
    }
}

public class MibNotification(
    MibItemIdent ident,
    SMIv2Status status,
    MibLeaf[] objects,
    string? description = null,
    string? reference = null) : MibItem(ident)
{
    public readonly SMIv2Status Status = status;
    public readonly MibLeaf[] Objects = objects;
    public readonly string? Description = description;
    public readonly string? Reference = reference;

    public override bool Equals(MibItem? other)
    {
        if (other is not MibNotification o) { return false; }
        return Ident.Equals(o.Ident)
            && Status == o.Status
            && Objects.SequenceEqual(o.Objects)
            && Description == o.Description
            && Reference == o.Reference;
    }

    public override int GetHashCode() => HashCode.Combine(
        HashCode.Combine(base.GetHashCode(), Status, Objects.SequenceHash(), Description),
        Reference);
}
