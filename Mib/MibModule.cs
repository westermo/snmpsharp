using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Parlot;
using SnmpSharpNet.Mib.Ast;

namespace SnmpSharpNet.Mib;

public interface IMibThatExports
{
    public bool TryImport(string ident, out uint[]? oid, out MibType? type);
}

public class MibModule : IMibThatExports
{
    public readonly string Identifier;
    public readonly Dictionary<MibItemIdent, MibItem> Items = [];

    public MibModule(ModuleDefinition module, IReadOnlyDictionary<string, IMibThatExports> importables)
    {
        Identifier = module.Identifier.ToString();

        var externalOids = MibItemIdent.TopLevelArcs.ToDictionary(x => x.Key, x => x.Value);

        // Add imported names to oidCache
        foreach (var (symbols, fromModule) in module.Imports)
        {
            var importedModule = importables[fromModule.ToString()];
            foreach (var symbol in symbols)
            {
                if (importedModule.TryImport(symbol.ToString(), out var oid, out _) && oid != null)
                {
                    externalOids.Add(
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

        var importedTypesByName = new Dictionary<string, MibType>();
        foreach (var (symbols, fromModule) in module.Imports)
        {
            var importedModule = importables[fromModule.ToString()];
            foreach (var symbol in symbols)
            {
                var symbolName = symbol.ToString();
                if (importedTypesByName.ContainsKey(symbolName))
                {
                    continue;
                }

                if (importedModule.TryImport(symbolName, out _, out var importedType) && importedType is not null)
                {
                    importedTypesByName[symbolName] = importedType;
                }
            }
        }

        var typeResolutionStack = new HashSet<string>();
        MibType ResolveType(AstType type)
        {
            if (type.Kind is { } kind)
            {
                return new MibType(
                    kind,
                    type.Refinement,
                    type.Values?.ToDictionary(v => v.Item1.ToString(), v => checked((int)v.Item2)),
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
                else if (importedTypesByName.TryGetValue(typeName, out var importedType))
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

            IReadOnlyList<TextSpan> rowIndex;
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
                if (!assignersByName.TryGetValue(baseRowName, out var _baseRow) || _baseRow is not ConceptualRow baseRow)
                    throw new Exception($"ConceptualTable({oid}): AUGMENTS base row '{baseRowName}' not found in this module");
                rowIndex = baseRow.Index;
            }
            else
            {
                throw new Exception($"ConceptualTable({oid}): .1 is not a ConceptualRow or AugmentingConceptualRow");
            }

            var index = rowIndex
                .Select((name, idx) => {
                    if (Items[GetOid(name.ToString())] is not MibLeaf item)
                    {
                        throw new Exception($"ConceptualTable({oid}): INDEX '{name}' is not a LeafObject");
                    }
                    return item;
                });

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

    public bool TryImport(string name, out uint[]? oid, out MibType? type)
    {
        oid = null;
        type = null;
        if (oidByName.TryGetValue(name, out var ident))
        {
            oid = ident.Oid;
            return true;
        }
        if (typedefsByName.TryGetValue(name, out type))
        {
            return true;
        }
        return false;
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
