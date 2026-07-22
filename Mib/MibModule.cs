using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Parlot;
using SnmpSharpNet.Mib.Ast;

namespace SnmpSharpNet.Mib;

public interface IMibThatExports
{
    Dictionary<MibItemIdent, MibItem> Items { get; }
    public bool TryImport(string ident, out MibItem? item, out MibType? type);
}

public class MibModule : IMibThatExports
{
    public readonly string Identifier;
    public Dictionary<MibItemIdent, MibItem> Items { get; } = [];

    public MibModule(ModuleDefinition module, IReadOnlyDictionary<string, IMibThatExports> importables)
    {
        Identifier = module.Identifier.ToString();

        var importedItems = new Dictionary<string, MibItem>();
        var importedTypes = new Dictionary<string, MibType>();

        // Add imported names to oidCache
        foreach (var (symbols, fromModule) in module.Imports)
        {
            if (!importables.TryGetValue(fromModule.ToString(), out var importedModules))
            {
                throw new Exception($"Failed to import '{fromModule}', no such importable");
            }
            var importedModule = importables[fromModule.ToString()];
            foreach (var symbol in symbols)
            {
                var symbolName = symbol.ToString();
                if (!importedModule.TryImport(symbolName, out var item, out var importedType))
                {
                    continue; // compliance/group names may not be resolvable
                }
                if (item is not null)
                {
                    importedItems[symbolName] = item;
                }
                if (importedType is not null)
                {
                    importedTypes[symbolName] = importedType;
                }
            }
        }

        var externalOids = MibItemIdent.TopLevelArcs.ToDictionary(x => x.Key, x => x.Value);
        foreach (var item in importedItems) {
            externalOids.Add(item.Key, item.Value.Ident);
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
            if (externalOids.TryGetValue(name, out cached)) { return cached; }

            if (!currentlyResolving.Add(name))
            {
                throw new Exception($"Identifier '{name}' is recursive");
            }

            if (!assignersByName.TryGetValue(name, out var item))
            {
                throw new Exception($"Identifier '{name}' is missing");
            }

            var parent = GetOid(item.Oid.Parent.ToString());
            foreach (var (compName, compNumber) in item.Oid.Components)
            {
                parent = parent.Add((uint)compNumber, compName.ToString());
            }
            var resolved = parent.Add((uint)item.Oid.Oid, item.Name.ToString());
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
        var localTypesByName = module.Items
            .OfType<TextualConvention>()
            .ToDictionary(x => x.Name.ToString(), x => x.Syntax);

        var typeResolutionStack = new HashSet<string>();
        MibType ResolveType(AstType type)
        {
            if (type.Kind is { } kind)
            {
                return new MibType(
                    kind,
                    type.Refinement,
                    type.Values?.ToDictionary(v => v.Item1.ToString(), v => v.Item2),
                    type.Name?.ToString());
            }

            var typeName = type.Name?.ToString() ?? throw new Exception("Unresolved type missing name");
            if (!typeResolutionStack.Add(typeName))
            {
                throw new Exception($"Type '{typeName}' is recursive");
            }

            try
            {
                MibType resolved;
                if (localTypesByName.TryGetValue(typeName, out var localType))
                {
                    resolved = ResolveType(localType);
                }
                else if (importedTypes.TryGetValue(typeName, out var importedType))
                {
                    resolved = importedType;
                }
                else
                {
                    throw new Exception($"Type '{typeName}' is missing");
                }

                try
                {
                    return new MibType(
                        resolved.Kind,
                        type.Refinement is not null
                            ? Refinement.Merge(resolved.Refinement, type.Refinement)
                            : resolved.Refinement,
                        resolved.Values,
                        type.Name?.ToString());
                }
                catch (Exception e)
                {
                    throw new Exception($"When resolving '{typeName}' based on '{resolved}': {e.Message}");
                }
            }
            finally
            {
                typeResolutionStack.Remove(typeName);
            }
        }

        typedefsByName = module.Items
            .OfType<TextualConvention>()
            .ToDictionary(x => x.Name.ToString(), x => ResolveType(x.Syntax));

        var entryTypes = module.Items
            .OfType<EntryDef>()
            .ToDictionary(x => x.Name.ToString(), x => x);

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
            Items.Add(oid, new MibLeaf(oid, ResolveType(lo.Syntax)));
        }

        foreach (var table in module.Items.OfType<ConceptualTable>())
        {
            var oid = GetOid(table.Name.ToString());

            if (!entryTypes.TryGetValue(table.EntryType.ToString(), out var entryDef))
            {
                throw new Exception($"ConceptualTable({oid.OidNames}): No such EntryType '{table.EntryType}'");
            }

            var rowOid = oid.Add(1, null);
            if (!itemByOid.TryGetValue(rowOid, out var rowAssigner))
            {
                throw new Exception($"ConceptualTable({oid}): No item at .1");
            }

            IReadOnlyList<TextSpan>? rowIndex = null;
            MibLeaf[]? augmentsIndex = null;
            if (rowAssigner is ConceptualRow row)
            {
                if (table.EntryType.ToString() != row.EntryType.ToString() || table.Status != row.Status)
                    throw new Exception($"ConceptualTable({oid}) doesn't match ConceptualRow");
                rowIndex = row.Index;
            }
            else if (rowAssigner is AugmentingConceptualRow augRow)
            {
                if (table.EntryType.ToString() != augRow.EntryType.ToString() || table.Status != augRow.Status)
                    throw new Exception($"ConceptualTable({oid}) doesn't match AugmentingConceptualRow");
                var baseRowName = augRow.Augments.ToString();
                if (assignersByName.TryGetValue(baseRowName, out var _baseRow) && _baseRow is ConceptualRow baseRow)
                {
                    rowIndex = baseRow.Index;
                }
                else if (importedItems.TryGetValue(baseRowName, out var importedRow) && importedRow is MibTable importedTable)
                {
                    rowIndex = null;
                    augmentsIndex = importedTable.Index;
                }
                else
                {
                    throw new Exception($"ConceptualTable({oid}): AUGMENTS base row '{baseRowName}' not found");
                }
            }
            else
            {
                throw new Exception($"ConceptualTable({oid}): .1 is not a ConceptualRow or AugmentingConceptualRow");
            }

            MibLeaf[] index;
            if (augmentsIndex is not null)
            {
                index = augmentsIndex;
            }
            else
            {
                index = rowIndex!
                    .Select((name, idx) => {
                        if (Items.TryGetValue(GetOid(name.ToString()), out var _item) && _item is MibLeaf item)
                        {
                            return item;
                        }
                        if (importedItems.TryGetValue(name.ToString(), out _item) && _item is MibLeaf imported)
                        {
                            return imported;
                        }
                        throw new Exception($"ConceptualTable({oid}): INDEX '{name}' is not a LeafObject");
                    })
                    .ToArray();
            }

            var columns = entryDef.Fields
                .Select(field => {
                    var fieldName = field.name.ToString();
                    var colOid = GetOid(fieldName);
                    if (!Items.TryGetValue(colOid, out var _col) || _col is not MibLeaf col)
                    {
                        throw new Exception($"ConceptualTable({oid}): field '{field.name}' is not a LeafObject");
                    }
                    return col;
                });
            Items.Add(oid, new MibTable(oid, [..index], [..columns]));
        }

        // Snapshot all items before removing columns, so TryImport can still find them
        foreach (var kvp in Items)
        {
            allItems[kvp.Key] = kvp.Value;
        }

        foreach (var table in module.Items.OfType<ConceptualTable>())
        {
            var tableItem = (MibTable)Items[GetOid(table.Name.ToString())];
            foreach (var item in tableItem.Columns)
            {
                Items.Remove(item.Ident);
            }
        }
    }

    //
    // IMibThatExports
    //
    private readonly Dictionary<string, MibItemIdent> oidByName = [];
    private readonly Dictionary<string, MibType> typedefsByName = [];
    private readonly Dictionary<MibItemIdent, MibItem> allItems = [];

    public bool TryImport(string name, out MibItem? item, out MibType? type)
    {
        item = null;
        type = null;
        var valued = oidByName.TryGetValue(name, out var ident);
        if (valued && !allItems.TryGetValue(ident, out item))
        {
            // Row entries (ConceptualRow) aren't in Items, but their parent table is at oid minus last arc
            var parentOid = new uint[ident.Oid.Length - 1];
            Array.Copy(ident.Oid, parentOid, parentOid.Length);
            var parentNames = new string?[ident.OidNames.Length - 1];
            Array.Copy(ident.OidNames, parentNames, parentNames.Length);
            var parentIdent = new MibItemIdent(parentOid, parentNames);
            if (allItems.TryGetValue(parentIdent, out var parent) && parent is MibTable)
            {
                item = parent;
            }
            else
            {
                item = new MibItem(ident);
            }
        }
        var typed = typedefsByName.TryGetValue(name, out type);
        return valued || typed;
    }

    public bool TryImportObject(string name, [MaybeNullWhen(false)] out MibItem item)
    {
        item = null;
        return oidByName.TryGetValue(name, out var ident) && Items.TryGetValue(ident, out item);
    }

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var kv in Items)
        {
            var item = kv.Value;
            sb.AppendLine($"\n{item.Ident}:");
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
                    sb.AppendLine($"  {string.Join(",\n  ", table.Columns.Select(x => $"{x.Ident}: {x.Type}"))}");
                    sb.AppendLine($"}}");
                    break;
            }
        }
        return sb.ToString();
    }
}
