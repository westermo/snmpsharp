using System;
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
    string? description = null,
    string? entryDescription = null) : MibValuedItem(ident)
{
    public readonly MibTableIndex[] Index = index;
    public readonly MibLeaf[] Columns = columns;
    public readonly string? Description = description;
    public readonly string? EntryDescription = entryDescription;

    public override bool Equals(MibItem? other)
    {
        if (other is not MibTable o) { return false; }
        return Ident.Equals(o.Ident)
            && Index.SequenceEqual(o.Index)
            && Columns.SequenceEqual(o.Columns)
            && Description == o.Description
            && EntryDescription == o.EntryDescription;
    }
    public override int GetHashCode() => HashCode.Combine(
        base.GetHashCode(),
        Index.SequenceHash(),
        Columns.SequenceHash(),
        HashCode.Combine(Description, EntryDescription));
}

public class MibLeaf(MibItemIdent ident, MibType type, SMIv2Accessibility accessibility, string? description = null) : MibValuedItem(ident)
{
    public readonly MibType Type = type;
    public readonly SMIv2Accessibility Accessibility = accessibility;
    public readonly string? Description = description;

    public override bool Equals(MibItem? other)
    {
        if (other is not MibLeaf o) { return false; }
        return Ident.Equals(o.Ident)
            && Type.Equals(o.Type)
            && Accessibility == o.Accessibility
            && Description == o.Description;
    }
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Type, Accessibility, Description);
}

public class MibNotification(
    MibItemIdent ident,
    SMIv2Status status,
    MibLeaf[] objects,
    string? description = null) : MibItem(ident)
{
    public readonly SMIv2Status Status = status;
    public readonly MibLeaf[] Objects = objects;
    public readonly string? Description = description;

    public override bool Equals(MibItem? other)
    {
        if (other is not MibNotification o) { return false; }
        return Ident.Equals(o.Ident)
            && Status == o.Status
            && Objects.SequenceEqual(o.Objects)
            && Description == o.Description;
    }

    public override int GetHashCode() => HashCode.Combine(
        base.GetHashCode(),
        Status,
        Objects.SequenceHash(),
        Description);
}
