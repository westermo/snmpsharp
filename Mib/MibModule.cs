using System;
using System.Collections.Generic;
using System.Linq;
using Parlot;
using SnmpSharpNet.Mib.Ast;

namespace SnmpSharpNet.Mib;

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

    extension(MibItemIdent self)
    {
        public MibItemIdent Add(TextSpan ident, uint value)
        {
            return self.Add(value, ident.ToString());
        }
    }
}

public class MibModule : IMibThatExports
{
    public readonly string Identifier;
    public readonly Dictionary<MibItemIdent, MibItem> Items = [];

    public MibModule(ModuleDefinition module, IDictionary<string, IMibThatExports> importables)
    {
        Identifier = module.Identifier.ToString();

        var importedOids = new Dictionary<string, MibItemIdent>();

        // Add imported names to oidCache
        foreach (var (symbols, fromModule) in module.Imports)
        {
            var importedModule = importables[fromModule.ToString()];
            foreach (var symbol in symbols)
            {
                if (importedModule.TryImport(symbol.ToString(), out var oid, out _) && oid != null)
                {
                    importedOids.Add(
                        symbol.ToString(),
                        new MibItemIdent(oid, [$"<{fromModule}>", symbol.ToString()])
                    );
                }
            }
        }

        //
        // First pass:
        // Resolve all oids
        //
        var itemByOid = new Dictionary<MibItemIdent, OidAssigner>();

        var assignersByName = module.Items
            .OfType<OidAssigner>()
            .ToDictionary(x => x.Name.ToString(), x => x);

        var currentlyResolving = new HashSet<string>();
        MibItemIdent GetOid(string name)
        {
            if (oidByName.TryGetValue(name, out var cached)) { return cached; }
            if (importedOids.TryGetValue(name, out cached)) { return cached; }

            if (!currentlyResolving.Add(name))
            {
                throw new Exception($"Identifier '{name}' is recursive");
            }

            if (!assignersByName.TryGetValue(name, out var item))
            {
                throw new Exception($"Identifier '{name}' is missing");
            }

            var parent = GetOid(item.Oid.parent.ToString());
            var resolved = parent.Add(item.Name, (uint)item.Oid.oid);
            oidByName[name] = resolved;
            itemByOid[resolved] = item;
            return resolved;
        }

        foreach (var item in assignersByName.Keys)
        {
            GetOid(item);
        }

        //
        // Second pass:
        // Resolve all types
        // 
        var typedefsByName = module.Items
            .OfType<TextualConvention>()
            .ToDictionary(x => x.Name.ToString(), x => x.Syntax.ToString());

        var entryTypes = module.Items
            .OfType<EntryDef>()
            .ToDictionary(x => x.Name, x => x);

        //
        // Second pass:
        // Resolve all items
        //
        foreach (var mi in module.Items.OfType<ModuleIdentity>())
        {
            var oid = GetOid(mi.Name.ToString());
            Items.Add(oid, new MibModuleInfo(oid, mi));
        }

        foreach (var lo in module.Items.OfType<LeafObject>())
        {
            var oid = GetOid(lo.Name.ToString());
            Items.Add(oid, new MibLeaf(oid, lo.Syntax.ToString()));
        }

        foreach (var table in module.Items.OfType<ConceptualTable>())
        {
            var oid = GetOid(table.Name.ToString());

            if (!entryTypes.TryGetValue(table.EntryType, out var entryDef))
            {
                throw new Exception($"ConceptualTable({oid.Path}): No such EntryType '{table.EntryType}'");
            }

            var rowOid = oid.Add(1, "");
            if (!itemByOid.TryGetValue(rowOid, out var _row) || _row is not ConceptualRow row)
            {
                throw new Exception($"ConceptualTable({oid.Path}): No ConceptualRow at .1");
            }

            if (table.EntryType != row.EntryType || table.Status != row.Status)
            {
                throw new Exception($"ConceptualTable({oid.Path}) doesn't match ConceptualRow");
            }

            var index = row.Index
                .Select((name, idx) => {
                    if (Items[GetOid(name.ToString())] is not MibLeaf item)
                    {
                        throw new Exception($"ConceptualTable({oid.Path}): INDEX '{name}' is not a LeafObject");
                    }
                    return item;
                });

            var columns = entryDef.Fields
                .Select((a, i) => {
                    var colOid = rowOid.Add((uint)(i + 1), "");
                    if (!Items.TryGetValue(colOid, out var _col) || _col is not MibLeaf col)
                    {
                        throw new Exception($"ConceptualTable({oid.Path}): .1.{i + 1} is not a LeafObject");
                    }
                    return col;
                });

            Items.Add(oid, new MibTable(oid, [..index], [..columns]));
        }
    }

    //
    // IMibThatExports
    //
    private readonly Dictionary<string, MibItemIdent> oidByName = [];
    private readonly Dictionary<string, string> typedefsByName = [];

    public bool TryImport(string name, out uint[]? oid, out string? syntax)
    {
        oid = null;
        syntax = null;
        if (oidByName.TryGetValue(name, out var ident))
        {
            oid = ident.Oid;
            return true;
        }
        if (typedefsByName.TryGetValue(name, out syntax))
        {
            return true;
        }
        return false;
    }

    public static MibModule Parse(string text, IDictionary<string, IMibThatExports> importables)
    {
        return new MibModule(ModuleDefinition.Parse(text), importables);
    }

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var kv in Items)
        {
            var item = kv.Value;
            sb.AppendLine($"\n{item.Identifier}:");
            sb.Append($"{string.Join(".", kv.Key.Oid)}");
            switch (item)
            {
                case MibModuleInfo mi:
                    sb.AppendLine($" ::= ModuleInfo {{");
                    sb.AppendLine($"  LastUpdated: {mi.LastUpdated}");
                    sb.AppendLine($"  Organization: {mi.Organization}");
                    sb.AppendLine($"  ContactInfo: {mi.ContactInfo}");
                    sb.AppendLine($"  Description: {mi.Description}");
                    foreach (var (date, desc) in mi.Revisions)
                    {
                        sb.AppendLine($"  Revision: {date} - {desc}");
                    }
                    sb.AppendLine($"}}");
                    break;
                case MibLeaf leaf:
                    sb.AppendLine($" ::= {leaf.Type}");
                    break;
                case MibTable table:
                    sb.AppendLine($"[");
                    sb.AppendLine($"  {string.Join(",\n  ", table.Index.Select(x => x.Type))}");
                    sb.AppendLine($"] {{");
                    sb.AppendLine($"  {string.Join(",\n  ", table.Columns.Select(x => $"{x.Identifier}: {x.Type}"))}");
                    sb.AppendLine($"}}");
                    break;
            }
        }
        return sb.ToString();
    }
}
