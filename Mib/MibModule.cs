using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Parlot;
using SnmpSharpNet.Mib.Ast;

namespace SnmpSharpNet.Mib;

using NamedOid = (string key, uint[] oid);

class OidComparer : IEqualityComparer<uint[]>
{
    public static readonly OidComparer Instance = new();
    public bool Equals(uint[]? x, uint[]? y) => x != null && y != null && x.SequenceEqual(y);
    public int GetHashCode(uint[] obj)
    {
        unchecked
        {
            int hash = 17;
            foreach (var v in obj) { hash = hash * 31 + (int)v; }
            return hash;
        }
    }
}



public interface IMibThatExports
{
    public bool TryImport(string ident, out uint[]? oid, out string? syntax);
}



public static class Extensions
{
    extension(uint[] oid)
    {
        public uint[] Add(uint value)
        {
            var newOid = new uint[oid.Length+1];
            Array.Copy(oid, newOid, oid.Length);
            newOid[oid.Length] = value;
            return newOid;
        }
    }

    extension(NamedOid self)
    {
        public NamedOid Add(TextSpan ident, uint value)
        {
            return (self.key + "." + ident, self.oid.Add(value));
        }
    }
}

public class MibItem(string name)
{
    public readonly string name = name;
}

public class MibModuleInfo(
    string name,
    string lastUpdated, string organization,
    string contactInfo, string  description,
    (string, string)[] revisions
): MibItem(name) {
    public readonly string lastUpdated = lastUpdated;
    public readonly string organization = organization;
    public readonly string contactInfo = contactInfo;
    public readonly string  description = description;
    public readonly (string, string)[] revisions = revisions;

    public MibModuleInfo(string  name, ModuleIdentity mi) : this(
        name,
        mi.LastUpdated.ToString(), mi.Organization.ToString(),
        mi.ContactInfo.ToString(), mi.Description.ToString(),
        [..mi.Revisions.Select((date, desc) => (date.ToString(), desc.ToString()))]
    ) {}
}

public class MibTable(string name, MibLeaf[] index, MibLeaf[] columns) : MibItem(name)
{
    public readonly MibLeaf[] index = index;
    public readonly MibLeaf[] columns = columns;
}

public class MibLeaf(string name, string type) : MibItem(name)
{
    public readonly string type = type;
}

public class MibModule : IMibThatExports
{
    public readonly string identifier;
    public readonly Dictionary<uint[], MibItem> items = new(OidComparer.Instance);

    public MibModule(ModuleDefinition module, IDictionary<string, IMibThatExports> importables)
    {
        identifier = module.Identifier.ToString();

        var oidCache = new Dictionary<TextSpan, NamedOid>();

        // Add imported names to oidCache
        foreach (var (symbols, fromModule) in module.Imports)
        {
            var imported = importables[fromModule.ToString()];
            foreach (var symbol in symbols)
            {
                if (imported.TryImport(symbol.ToString(), out var oid, out _) && oid != null)
                {
                    oidCache.Add(symbol, ($"<{fromModule}>.{symbol}", oid));
                }
            }
        }

        // Add all typedefs        
        var typedefs = module.Items
            .OfType<TextualConvention>()
            .ToDictionary(x => x.Name, x => x.Syntax);
        typedefsByName = typedefs.ToDictionary(x => x.ToString(), x => x.ToString());
        
        // Resolve all oids 
        var itemsToAdd = module.Items.OfType<OidAssigner>().ToList();
        while (itemsToAdd.Count > 0)
        {
            var addedSomething = false;
            for (var i = 0; i < itemsToAdd.Count; i++)
            {
                var item = itemsToAdd[i];
                if (!oidCache.TryGetValue(item.Oid.parent, out var parentOid)) { continue; }
                oidCache.Add(item.Name, parentOid.Add(item.Name, (uint)item.Oid.oid));
                addedSomething = true;

                itemsToAdd.RemoveAt(i);
                i--;
            }

            if (!addedSomething)
            {
                throw new Exception("We never added something :(");
            }
        }

        // Add table entry defintions
        var fieldCache = module.Items
            .OfType<EntryDef>()
            .ToDictionary(x => x.Name, x => x.Fields);


        // Add items
        foreach (var item in module.Items.OfType<ObjectIdentifier>())
        {
            oidsByName[item.Name.ToString()] = oidCache[item.Name].oid;
        }

        foreach (var item in module.Items.OfType<ModuleIdentity>())
        {
            var (key, oid) = oidCache[item.Name];
            oidsByName[item.Name.ToString()] = oid;
            items.Add(oid, new MibModuleInfo(key, item));
        }

        foreach (var item in module.Items.OfType<LeafObject>())
        {
            if (item.Status != SMIv2Status.Current) { continue; }

            var (key, oid) = oidCache[item.Name];
            oidsByName[item.Name.ToString()] = oid;
            items.Add(oid, new MibLeaf(key, item.Syntax.ToString().Trim()));
        }

        // Add tables
        var entryTypes = module.Items.OfType<EntryDef>().ToDictionary(x => x.Name, x => x.Fields);
        var tables = module.Items.OfType<ConceptualTable>().ToDictionary(x => x.Name, x => x);

        foreach (var item in module.Items.OfType<ConceptualRow>())
        {
            if (item.Status != SMIv2Status.Current) { continue; }

            var table = tables[item.Oid.parent];
            var (rowKey, rowOid) = oidCache[item.Name];
            var (tableKey, tableOid) = oidCache[table.Name];

            if (table.EntryType != item.EntryType || table.Status != item.Status || !tableOid.Add(1).SequenceEqual(rowOid))
            {
                throw new Exception($"ConceptualTable({table.Name}) doesn't match ConceptualRow({table.Name})");
            }

            var index = item.Index
                .Select((x, idx) => {
                    var indexItem = items[oidCache[x].oid];
                    if (indexItem is not MibLeaf indexLeaf)
                    {
                        throw new Exception("Non leaf object as index");
                    }

                    return new MibLeaf($"{rowKey}[{idx}]", indexLeaf.type);
                });

            var columns = entryTypes[item.EntryType]
                .Select((row, idx) => {
                    var colOid = rowOid.Add((uint)(idx + 1));
                    var colItem = items[colOid];

                    if (colItem is not MibLeaf colLeaf)
                    {
                        throw new Exception($"Non-leaf column at index {idx + 1}");
                    }
                    return colLeaf;
                });

            oidsByName[table.Name.ToString()] = tableOid;
            items[tableOid] = new MibTable(tableKey, [..index], [..columns]);
        }

    }

    //
    // IMibThatExports
    //
    private readonly Dictionary<string, uint[]> oidsByName = [];
    private readonly Dictionary<string, string> typedefsByName = [];

    public bool TryImport(string ident, out uint[]? oid, out string? syntax)
    {
        if (oidsByName.TryGetValue(ident, out oid))
        {
            syntax = null;
            return true;
        }
        if (typedefsByName.TryGetValue(ident, out syntax))
        {
            oid = null;
            return true;
        }
        oid = null;
        syntax = null;
        return false;
    }

    public static MibModule Parse(string text, IDictionary<string, IMibThatExports> importables)
    {
        return new MibModule(ModuleDefinition.Parse(text), importables);
    }

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var kv in items)
        {
            var item = kv.Value;
            sb.AppendLine($"\n{item.name}:");
            sb.Append($"{string.Join(".", kv.Key)}");
            switch (item)
            {
                case MibModuleInfo mi:
                    sb.AppendLine($" ::= ModuleInfo {{");
                    sb.AppendLine($"  LastUpdated: {mi.lastUpdated}");
                    sb.AppendLine($"  Organization: {mi.organization}");
                    sb.AppendLine($"  ContactInfo: {mi.contactInfo}");
                    sb.AppendLine($"  Description: {mi.description}");
                    foreach (var (date, desc) in mi.revisions)
                    {
                        sb.AppendLine($"  Revision: {date} - {desc}");
                    }
                    sb.AppendLine($"}}");
                    break;
                case MibLeaf leaf:
                    sb.AppendLine($" ::= {leaf.type}");
                    break;
                case MibTable table:
                    sb.AppendLine($"[");
                    sb.AppendLine($"  {string.Join(",\n  ", table.index.Select(x => x.type))}");
                    sb.AppendLine($"] {{");
                    sb.AppendLine($"  {string.Join(",\n  ", table.columns.Select(x => $"{x.name}: {x.type}"))}");
                    sb.AppendLine($"}}");
                    break;
            }
        }
        return sb.ToString();
    }
}
