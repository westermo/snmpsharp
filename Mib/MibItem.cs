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
        [..mi.Revisions.Select((date, desc) => (date.ToString(), desc.ToString()))]
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

public class MibTable(MibItemIdent ident, MibLeaf[] index, MibLeaf[] columns) : MibValuedItem(ident)
{
    public readonly MibLeaf[] Index = index;
    public readonly MibLeaf[] Columns = columns;

    public override bool Equals(MibItem? other)
    {
        if (other is not MibTable o) { return false; }
        return Ident.Equals(o.Ident)
            && Index.SequenceEqual(o.Index)
            && Columns.SequenceEqual(o.Columns);
    }
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Index.SequenceHash(), Columns.SequenceHash());
}

public class MibLeaf(MibItemIdent ident, MibType type) : MibValuedItem(ident)
{
    public readonly MibType Type = type;

    public override bool Equals(MibItem? other)
    {
        if (other is not MibLeaf o) { return false; }
        return Ident.Equals(o.Ident)
            && Type.Equals(o.Type);
    }
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Type);
}
