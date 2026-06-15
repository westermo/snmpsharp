using System;
using System.Linq;
using SnmpSharpNet.Mib.Ast;

namespace SnmpSharpNet.Mib;

public class MibItem(MibItemIdent ident)
{
    public readonly MibItemIdent Identifier = ident;
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
}

public class MibTable(MibItemIdent ident, MibLeaf[] index, MibLeaf[] columns) : MibItem(ident)
{
    public readonly MibLeaf[] Index = index;
    public readonly MibLeaf[] Columns = columns;
}

public class MibLeaf(MibItemIdent ident, string type) : MibItem(ident)
{
    public readonly string Type = type;
}
