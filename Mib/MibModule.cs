using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Parlot;
using SnmpSharpNet.Mib.Ast;

namespace SnmpSharpNet.Mib;

public interface IMibThatExports
{
    Dictionary<MibItemIdent, MibItem> Items { get; }
    public bool TryImport(string ident, out MibItem? item, out MibType? type);
}

public class MibModule : IMibThatExports, IEquatable<MibModule>
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
        foreach (var item in importedItems)
        {
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
            .ToDictionary(x => x.Name.ToString());

        var typeResolutionStack = new HashSet<string>();

        typedefsByName = module.Items
            .OfType<TextualConvention>()
            .ToDictionary(x => x.Name.ToString(), ResolveTextualConvention);

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
            var type = ResolveType(lo.Syntax);
            Items.Add(oid, new MibLeaf(
                oid,
                type,
                ResolveMetadata(lo.Metadata),
                ResolveDefaultValue(lo.DefaultValue, type)));
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

            IReadOnlyList<(TextSpan Name, bool IsImplied)>? rowIndex;
            MibTableIndex[]? augmentsIndex = null;
            MibObjectMetadata entryMetadata;
            switch (rowAssigner)
            {
                case ConceptualRow row when table.EntryType.ToString() != row.EntryType.ToString() ||
                                            table.Status != row.Status:
                    throw new Exception($"ConceptualTable({oid}) doesn't match ConceptualRow");
                case ConceptualRow row:
                    rowIndex = row.Index;
                    entryMetadata = ResolveMetadata(row.Metadata);
                    break;
                case AugmentingConceptualRow augRow when table.EntryType.ToString() != augRow.EntryType.ToString() ||
                                                         table.Status != augRow.Status:
                    throw new Exception($"ConceptualTable({oid}) doesn't match AugmentingConceptualRow");
                case AugmentingConceptualRow augRow:
                {
                    entryMetadata = ResolveMetadata(augRow.Metadata);
                    var baseRowName = augRow.Augments.ToString();
                    if (assignersByName.TryGetValue(baseRowName, out var assigner) && assigner is ConceptualRow baseRow)
                    {
                        rowIndex = baseRow.Index;
                    }
                    else if (importedItems.TryGetValue(baseRowName, out var importedRow) &&
                             importedRow is MibTable importedTable)
                    {
                        rowIndex = null;
                        augmentsIndex = importedTable.Index;
                    }
                    else
                    {
                        throw new Exception($"ConceptualTable({oid}): AUGMENTS base row '{baseRowName}' not found");
                    }

                    break;
                }
                default:
                    throw new Exception(
                        $"ConceptualTable({oid}): .1 is not a ConceptualRow or AugmentingConceptualRow");
            }

            MibTableIndex[] index;
            if (augmentsIndex is not null)
            {
                index = augmentsIndex;
            }
            else
            {
                index =
                [
                    .. rowIndex!
                        .Select((entry, _) =>
                        {
                            if (Items.TryGetValue(GetOid(entry.Name.ToString()), out var mibItem) &&
                                mibItem is MibLeaf item)
                            {
                                return new MibTableIndex(item, entry.IsImplied);
                            }

                            if (importedItems.TryGetValue(entry.Name.ToString(), out mibItem) &&
                                mibItem is MibLeaf imported)
                            {
                                return new MibTableIndex(imported, entry.IsImplied);
                            }

                            throw new Exception($"ConceptualTable({oid}): INDEX '{entry.Name}' is not a LeafObject");
                        })
                ];
            }

            var columns = entryDef.Fields
                .Select(field =>
                {
                    var fieldName = field.name.ToString();
                    var colOid = GetOid(fieldName);
                    if (!Items.TryGetValue(colOid, out var _col) || _col is not MibLeaf col)
                    {
                        throw new Exception($"ConceptualTable({oid}): field '{field.name}' is not a LeafObject");
                    }

                    return col;
                }).Where(c => c.Accessibility.CanRead());
            Items.Add(oid,
                new MibTable(
                    oid,
                    [.. index],
                    [.. columns],
                    ResolveMetadata(table.Metadata),
                    entryMetadata));
        }

        foreach (var notification in module.Items.OfType<NotificationType>())
        {
            var oid = GetOid(notification.Name.ToString());
            var objects = notification.Objects
                .Select(obj =>
                {
                    var objectName = obj.ToString();
                    if (Items.TryGetValue(GetOid(objectName), out var mibItem) && mibItem is MibLeaf item)
                    {
                        return item;
                    }

                    if (importedItems.TryGetValue(objectName, out mibItem) && mibItem is MibLeaf imported)
                    {
                        return imported;
                    }

                    throw new Exception(
                        $"NotificationType({oid}): OBJECTS entry '{obj}' is not a LeafObject");
                });

            Items.Add(oid, new MibNotification(
                oid,
                notification.Status,
                [.. objects],
                ToOptionalString(notification.Description),
                ToOptionalString(notification.Reference)));
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

        return;

        MibType ResolveTextualConvention(TextualConvention textualConvention)
        {
            var resolved = ResolveType(textualConvention.Syntax);
            var metadata = textualConvention.Metadata;
            return new MibType(
                resolved.Kind,
                resolved.Refinement,
                resolved.Values,
                textualConvention.Name.ToString(),
                new MibTextualConvention(
                    textualConvention.Name.ToString(),
                    ToOptionalString(metadata.DisplayHint),
                    metadata.Status,
                    metadata.Description.ToString(),
                    ToOptionalString(metadata.Reference)));
        }

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
                    resolved = ResolveTextualConvention(localType);
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
                        type.Name?.ToString(),
                        resolved.TextualConvention);
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

        static MibObjectMetadata ResolveMetadata(ObjectTypeMetadata metadata) => new(
            metadata.Accessibility,
            metadata.Status,
            ToOptionalString(metadata.Units),
            ToOptionalString(metadata.Description),
            ToOptionalString(metadata.Reference));

        static string? ToOptionalString(TextSpan? text)
        {
            var value = text?.ToString();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        MibDefaultValue? ResolveDefaultValue(AstDefaultValue? value, MibType type)
        {
            if (value is null)
            {
                return null;
            }

            if (type.Kind is TypeKind.Counter32 or TypeKind.Counter64)
            {
                throw new InvalidOperationException(
                    $"DEFVAL is not permitted for {type.Kind} syntax.");
            }

            return type.Kind switch
            {
                TypeKind.Integer32 or TypeKind.Integer32Enum or TypeKind.Unsigned32 or TypeKind.Gauge32 or
                    TypeKind.TimeTicks => ResolveNumericDefault(value, type),
                TypeKind.OctetString or TypeKind.Opaque => ResolveOctetStringDefault(value, type),
                TypeKind.Bits => ResolveBitsDefault(value, type),
                TypeKind.ObjectIdentifier => ResolveObjectIdentifierDefault(value),
                TypeKind.IpAddress => ResolveIpAddressDefault(value),
                _ => throw new InvalidOperationException(
                    $"DEFVAL is not supported for {type.Kind} syntax.")
            };
        }

        static MibDefaultValue ResolveNumericDefault(AstDefaultValue value, MibType type)
        {
            long number;
            switch (value.Kind)
            {
                case AstDefaultValueKind.Number when value.Number is { } numeric:
                    number = numeric;
                    break;
                case AstDefaultValueKind.Identifier when value.Text is { } identifier &&
                                                         type.Values is { } values &&
                                                         values.TryGetValue(identifier.ToString(), out var namedValue):
                    number = namedValue;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"DEFVAL '{Describe(value)}' is not a numeric value permitted by {type}.");
            }

            if (type.Kind is TypeKind.Integer32 or TypeKind.Integer32Enum &&
                number is < int.MinValue or > int.MaxValue)
            {
                throw new InvalidOperationException($"DEFVAL '{number}' is outside the Integer32 range.");
            }

            if (type.Kind is TypeKind.Unsigned32 or TypeKind.Gauge32 or TypeKind.TimeTicks &&
                number is < 0 or > uint.MaxValue)
            {
                throw new InvalidOperationException($"DEFVAL '{number}' is outside the Unsigned32 range.");
            }

            if (type.Kind == TypeKind.Integer32Enum &&
                type.Values is { Count: > 0 } enumValues &&
                !enumValues.Values.Contains(number))
            {
                throw new InvalidOperationException(
                    $"DEFVAL '{number}' is not a named value in {type}.");
            }

            if (type.Refinement is { IsSize: false, Constraints.Count: > 0 } refinement &&
                !refinement.Constraints.Any(constraint => number >= constraint.Min && number <= constraint.Max))
            {
                throw new InvalidOperationException(
                    $"DEFVAL '{number}' is outside the allowed range for {type}.");
            }

            return new MibDefaultValue(MibDefaultValueKind.Number, number: number);
        }

        static MibDefaultValue ResolveOctetStringDefault(AstDefaultValue value, MibType type)
        {
            byte[] octets = value.Kind switch
            {
                AstDefaultValueKind.String when value.Text is { } text => Encoding.UTF8.GetBytes(text.ToString()),
                AstDefaultValueKind.HexString when value.Text is { } text => DecodeHex(text.ToString()),
                AstDefaultValueKind.BinaryString when value.Text is { } text => DecodeBinary(text.ToString()),
                _ => throw new InvalidOperationException(
                    $"DEFVAL '{Describe(value)}' is not an OCTET STRING value.")
            };
            ValidateSize(octets.Length, type);
            return new MibDefaultValue(MibDefaultValueKind.Octets, octets: octets);
        }

        static MibDefaultValue ResolveBitsDefault(AstDefaultValue value, MibType type)
        {
            if (value.Kind != AstDefaultValueKind.Bits)
            {
                throw new InvalidOperationException(
                    $"DEFVAL '{Describe(value)}' is not a BITS value.");
            }

            var values = type.Values ?? throw new InvalidOperationException(
                $"BITS syntax '{type}' is missing named bit values.");
            var names = value.Names.Select(name => name.ToString()).ToArray();
            foreach (var name in names)
            {
                if (!values.ContainsKey(name))
                {
                    throw new InvalidOperationException(
                        $"DEFVAL bit '{name}' is not declared by {type}.");
                }
            }

            return new MibDefaultValue(MibDefaultValueKind.Bits, names: names);
        }

        MibDefaultValue ResolveObjectIdentifierDefault(AstDefaultValue value)
        {
            if (value.Kind == AstDefaultValueKind.Identifier && value.Text is { } identifier)
            {
                return new MibDefaultValue(
                    MibDefaultValueKind.ObjectIdentifier,
                    objectIdentifier: GetOid(identifier.ToString()).Oid);
            }

            if (value.Kind != AstDefaultValueKind.ObjectIdentifier || value.ObjectIdentifier.Count == 0)
            {
                throw new InvalidOperationException(
                    $"DEFVAL '{Describe(value)}' is not an OBJECT IDENTIFIER value.");
            }

            var arcs = new List<uint>();
            foreach (var component in value.ObjectIdentifier)
            {
                if (component.Name is { } name)
                {
                    if (arcs.Count == 0)
                    {
                        var resolved = GetOid(name.ToString()).Oid;
                        arcs.AddRange(resolved);
                        if (component.Number is { } namedRootNumber &&
                            (resolved.Length == 0 ||
                             resolved[resolved.Length - 1] != ToOidArc(namedRootNumber, "DEFVAL")))
                        {
                            throw new InvalidOperationException(
                                $"DEFVAL component '{name}({namedRootNumber})' does not match its named OBJECT IDENTIFIER.");
                        }

                        continue;
                    }

                    var namedResolved = GetOid(name.ToString()).Oid;
                    if (component.Number is null)
                    {
                        if (namedResolved.Length < arcs.Count ||
                            !arcs.SequenceEqual(namedResolved.Take(arcs.Count)))
                        {
                            throw new InvalidOperationException(
                                $"DEFVAL component '{name}' is not below the preceding OBJECT IDENTIFIER.");
                        }

                        arcs = [.. namedResolved];
                        continue;
                    }

                    var namedNumber = ToOidArc(component.Number.Value, "DEFVAL");
                    if (namedResolved.Length != arcs.Count + 1 ||
                        !arcs.SequenceEqual(namedResolved.Take(arcs.Count)) ||
                        namedResolved[namedResolved.Length - 1] != namedNumber)
                    {
                        throw new InvalidOperationException(
                            $"DEFVAL component '{name}({component.Number})' does not match its named OBJECT IDENTIFIER.");
                    }

                    arcs = [.. namedResolved];
                    continue;
                }

                if (component.Number is { } number)
                {
                    arcs.Add(ToOidArc(number, "DEFVAL"));
                }
            }

            if (arcs.Count == 0)
            {
                throw new InvalidOperationException(
                    $"DEFVAL '{Describe(value)}' is not an OBJECT IDENTIFIER value.");
            }

            return new MibDefaultValue(MibDefaultValueKind.ObjectIdentifier, objectIdentifier: arcs);
        }

        static MibDefaultValue ResolveIpAddressDefault(AstDefaultValue value)
        {
            if (value.Kind != AstDefaultValueKind.String || value.Text is not { } text ||
                !System.Net.IPAddress.TryParse(text.ToString(), out var address) ||
                address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                throw new InvalidOperationException(
                    $"DEFVAL '{Describe(value)}' is not an IPv4 address.");
            }

            return new MibDefaultValue(MibDefaultValueKind.Octets, octets: address.GetAddressBytes());
        }

        static void ValidateSize(int size, MibType type)
        {
            if (type.Refinement is not { IsSize: true, Constraints.Count: > 0 } refinement ||
                refinement.Constraints.Any(constraint => size >= constraint.Min && size <= constraint.Max))
            {
                return;
            }

            throw new InvalidOperationException(
                $"DEFVAL length '{size}' is outside the allowed size for {type}.");
        }

        static byte[] DecodeHex(string source)
        {
            var hex = RemoveWhitespace(source);
            if (hex.Length % 2 != 0 || hex.Any(character => !Uri.IsHexDigit(character)))
            {
                throw new InvalidOperationException($"DEFVAL '{source}' is not valid hexadecimal data.");
            }

            var value = new byte[hex.Length / 2];
            for (var index = 0; index < value.Length; index++)
            {
                value[index] = Convert.ToByte(hex.Substring(index * 2, 2), 16);
            }

            return value;
        }

        static byte[] DecodeBinary(string source)
        {
            var binary = RemoveWhitespace(source);
            if (binary.Any(character => character is not ('0' or '1')) || binary.Length % 8 != 0)
            {
                throw new InvalidOperationException($"DEFVAL '{source}' is not valid binary data.");
            }

            var value = new byte[binary.Length / 8];
            for (var index = 0; index < value.Length; index++)
            {
                value[index] = Convert.ToByte(binary.Substring(index * 8, 8), 2);
            }

            return value;
        }

        static string RemoveWhitespace(string source) =>
            new(source.Where(character => !char.IsWhiteSpace(character)).ToArray());

        static string Describe(AstDefaultValue value) =>
            value.Text?.ToString() ??
            value.Number?.ToString() ??
            string.Join(", ", value.Names.Select(name => name.ToString()));

        MibItemIdent GetOid(string name)
        {
            if (oidByName.TryGetValue(name, out var cached) || externalOids.TryGetValue(name, out cached))
            {
                return cached;
            }

            if (!currentlyResolving.Add(name))
            {
                throw new Exception($"Identifier '{name}' is recursive");
            }

            if (!assignersByName.TryGetValue(name, out var item))
            {
                throw new Exception($"Identifier '{name}' is missing");
            }

            var parent = item.Oid.NumericArcs is { } numericArcs
                ? CreateNumericParent(numericArcs, item.Name.ToString())
                : GetOid(item.Oid.Parent.ToString());
            foreach (var (compName, compNumber) in item.Oid.Components)
            {
                parent = parent.Add(ToOidArc(compNumber, item.Name.ToString()), compName.ToString());
            }

            var resolved = parent.Add(ToOidArc(item.Oid.Oid, item.Name.ToString()), item.Name.ToString());
            oidByName[name] = resolved;
            itemByOid[resolved] = item;
            return resolved;
        }

        static MibItemIdent CreateNumericParent(IReadOnlyList<long> numericArcs, string itemName)
        {
            var parentLength = numericArcs.Count - 1;
            var oid = new uint[parentLength];
            var names = new string?[parentLength];
            for (var index = 0; index < parentLength; index++)
            {
                oid[index] = ToOidArc(numericArcs[index], itemName);
            }

            return new MibItemIdent(oid, names);
        }

        static uint ToOidArc(long arc, string itemName)
        {
            if (arc is < 0 or > uint.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(arc),
                    arc,
                    $"OID arc in assignment '{itemName}' must be between 0 and {uint.MaxValue}.");
            }

            return (uint)arc;
        }
    }

    //
    // IMibThatExports
    //
    private readonly Dictionary<string, MibItemIdent> oidByName = [];
    private readonly Dictionary<string, MibType> typedefsByName = [];
    private readonly Dictionary<MibItemIdent, MibItem> allItems = [];

    /// <summary>All named OID arcs defined in this module, including pure OBJECT IDENTIFIER navigation nodes.</summary>
    public IReadOnlyDictionary<string, MibItemIdent> AllOids => oidByName;

    /// <summary>All resolved items including table columns (columns are removed from Items after construction).</summary>
    public IReadOnlyDictionary<MibItemIdent, MibItem> AllItems => allItems;

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
        var sb = new StringBuilder();
        foreach (var kv in Items)
        {
            var item = kv.Value;
            sb.AppendLine($"\n{item.Ident}:");
            sb.Append($"{string.Join(".", kv.Key.Oid)}");
            switch (item)
            {
                case MibModuleInfo mi:
                    sb.AppendLine(" ::= ModuleInfo {");
                    sb.AppendLine($"  LastUpdated: {mi.LastUpdated}");
                    sb.AppendLine($"  Organization: {mi.Organization}");
                    sb.AppendLine($"  ContactInfo: {mi.ContactInfo}");
                    sb.AppendLine($"  Description: {mi.Description}");
                    foreach (var (date, desc) in mi.Revisions)
                    {
                        sb.AppendLine($"  Revision: {date} - {desc}");
                    }

                    sb.AppendLine("}");
                    break;
                case MibLeaf leaf:
                    sb.AppendLine($" ::= {leaf.Type}");
                    break;
                case MibTable table:
                    sb.AppendLine("[");
                    sb.AppendLine($"  {string.Join(",\n  ", table.Index.Select(x => x.Type))}");
                    sb.AppendLine("] {");
                    sb.AppendLine($"  {string.Join(",\n  ", table.Columns.Select(x => $"{x.Ident}: {x.Type}"))}");
                    sb.AppendLine("}");
                    break;
                case MibNotification notification:
                    sb.AppendLine(
                        $" ::= Notification [{string.Join(", ", notification.Objects.Select(x => x.Ident.Name))}]");
                    break;
            }
        }

        return sb.ToString();
    }


    public bool Equals(MibModule? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Identifier == other.Identifier && Items.DictEquals(other.Items);
    }

    public override bool Equals(object? obj) => Equals(obj as MibModule);

    public override int GetHashCode()
    {
        unchecked
        {
            var itemsHash = 0;
            foreach (var kvp in Items)
                itemsHash += HashCode.Combine(kvp.Key, kvp.Value);
            return HashCode.Combine(Identifier, itemsHash);
        }
    }
}