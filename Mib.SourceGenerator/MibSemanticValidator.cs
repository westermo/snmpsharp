using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using SnmpSharpNet.Mib.Ast;

namespace SnmpSharpNet.Mib.SourceGenerator;

internal static class MibSemanticValidator
{
    public static IEnumerable<Diagnostic> ValidateDefinitions(
        IReadOnlyDictionary<string, GenMibDefinition> definitions)
    {
        foreach (var definition in definitions.Values)
        {
            if (!definition.Module.IsOk)
            {
                continue;
            }

            foreach (var diagnostic in ValidateDirectStructure(definition.Location, definition.Module.Value))
            {
                yield return diagnostic;
            }
        }
    }

    public static IEnumerable<Diagnostic> Validate(
        IReadOnlyDictionary<string, GenMibDefinition> definitions,
        IReadOnlyDictionary<string, MibModule> modules)
    {
        foreach (var pair in definitions)
        {
            var moduleName = pair.Key;
            var definition = pair.Value;
            if (!definition.Module.IsOk || !modules.TryGetValue(moduleName, out var module))
            {
                continue;
            }

            foreach (var diagnostic in ValidateModule(definition.Location, definition.Module.Value, module))
            {
                yield return diagnostic;
            }
        }
    }

    private static IEnumerable<Diagnostic> ValidateModule(
        Location location,
        ModuleDefinition definition,
        MibModule module)
    {
        var entryDefinitions = definition.Items
            .OfType<EntryDef>()
            .ToDictionary(entry => entry.Name.ToString());
        foreach (var table in definition.Items.OfType<ConceptualTable>())
        {
            if (table.Accessibility != SMIv2Accessibility.NotAccessible)
            {
                yield return Diagnostic.Create(
                    Diagnostics.ConceptualTableAndRowMustBeNotAccessible,
                    location,
                    "table",
                    table.Name.ToString());
            }
        }

        foreach (var row in definition.Items.OfType<ConceptualRow>())
        {
            foreach (var diagnostic in ValidateRow(
                         location,
                         row,
                         module,
                         entryDefinitions))
            {
                yield return diagnostic;
            }
        }

        foreach (var row in definition.Items.OfType<AugmentingConceptualRow>())
        {
            if (row.Accessibility != SMIv2Accessibility.NotAccessible)
            {
                yield return Diagnostic.Create(
                    Diagnostics.ConceptualTableAndRowMustBeNotAccessible,
                    location,
                    "row",
                    row.Name.ToString());
            }

            foreach (var diagnostic in ValidateColumnAccess(
                         location,
                         row.Name.ToString(),
                         row.EntryType,
                         module,
                         entryDefinitions))
            {
                yield return diagnostic;
            }
        }

        foreach (var notification in definition.Items.OfType<NotificationType>())
        {
            foreach (var objectName in notification.Objects)
            {
                if (TryGetLeaf(module, objectName.ToString(), out var leaf) &&
                    leaf.Accessibility == SMIv2Accessibility.NotAccessible)
                {
                    yield return Diagnostic.Create(
                        Diagnostics.NotificationObjectMustBeAccessible,
                        location,
                        objectName.ToString(),
                        notification.Name.ToString());
                }
            }
        }

        foreach (var diagnostic in ValidateNotificationOids(location, definition, module))
        {
            yield return diagnostic;
        }
    }

    private static IEnumerable<Diagnostic> ValidateRow(
        Location location,
        ConceptualRow row,
        MibModule module,
        IReadOnlyDictionary<string, EntryDef> entryDefinitions)
    {
        if (row.Accessibility != SMIv2Accessibility.NotAccessible)
        {
            yield return Diagnostic.Create(
                Diagnostics.ConceptualTableAndRowMustBeNotAccessible,
                location,
                "row",
                row.Name.ToString());
        }

        foreach (var diagnostic in ValidateIndex(
                     location,
                     row,
                     module,
                     entryDefinitions))
        {
            yield return diagnostic;
        }

        foreach (var diagnostic in ValidateColumnAccess(
                     location,
                     row.Name.ToString(),
                     row.EntryType,
                     module,
                     entryDefinitions))
        {
            yield return diagnostic;
        }
    }

    private static IEnumerable<Diagnostic> ValidateColumnAccess(
        Location location,
        string rowName,
        AstType entryType,
        MibModule module,
        IReadOnlyDictionary<string, EntryDef> entryDefinitions)
    {
        var columns = GetColumns(entryType, module, entryDefinitions).ToArray();
        if (columns.Any(column => column.Accessibility == SMIv2Accessibility.ReadCreate) &&
            columns.Any(column => column.Accessibility == SMIv2Accessibility.ReadWrite))
        {
            yield return Diagnostic.Create(
                Diagnostics.ReadCreateAndReadWriteColumnsCannotMix,
                location,
                rowName);
        }
    }

    private static IEnumerable<Diagnostic> ValidateIndex(
        Location location,
        ConceptualRow row,
        MibModule module,
        IReadOnlyDictionary<string, EntryDef> entryDefinitions)
    {
        var fieldNames = entryDefinitions.TryGetValue(row.EntryType.ToString(), out var entryDefinition)
            ? new HashSet<string>(entryDefinition.Fields.Select(field => field.name.ToString()))
            : [];
        var allColumnsAreIndexes = fieldNames.Count > 0 &&
                                   fieldNames.All(fieldName => row.Index.Any(index =>
                                       index.Name.ToString() == fieldName));
        var hasReadOnlyIndex = false;

        for (var index = 0; index < row.Index.Count; index++)
        {
            var indexEntry = row.Index[index];
            var indexName = indexEntry.Name.ToString();
            if (!TryGetLeaf(module, indexName, out var leaf))
            {
                continue;
            }

            if (fieldNames.Contains(indexName) &&
                !allColumnsAreIndexes &&
                leaf.Accessibility != SMIv2Accessibility.NotAccessible)
            {
                yield return Diagnostic.Create(
                    Diagnostics.AuxiliaryIndexMustBeNotAccessible,
                    location,
                    indexName,
                    row.Name.ToString());
            }

            hasReadOnlyIndex |= fieldNames.Contains(indexName) &&
                                leaf.Accessibility == SMIv2Accessibility.ReadOnly;

            if (indexEntry.IsImplied && index != row.Index.Count - 1)
            {
                yield return Diagnostic.Create(
                    Diagnostics.ImpliedIndexMustBeLast,
                    location,
                    indexName,
                    row.Name.ToString());
            }

            if (indexEntry.IsImplied && !HasVariableLengthSyntax(leaf.Type))
            {
                yield return Diagnostic.Create(
                    Diagnostics.ImpliedIndexMustBeVariableLength,
                    location,
                    indexName,
                    row.Name.ToString());
            }

            if (leaf.Type.Kind == TypeKind.Opaque)
            {
                yield return Diagnostic.Create(
                    Diagnostics.OpaqueCannotBeIndex,
                    location,
                    indexName,
                    row.Name.ToString());
            }

            if (indexEntry.IsImplied &&
                HasVariableLengthSyntax(leaf.Type) &&
                PermitsZeroLength(leaf.Type))
            {
                yield return Diagnostic.Create(
                    Diagnostics.ImpliedIndexCannotBeEmpty,
                    location,
                    indexName,
                    row.Name.ToString());
            }

            if (leaf.Type.Kind is TypeKind.Counter32 or TypeKind.Counter64)
            {
                yield return Diagnostic.Create(
                    Diagnostics.CounterCannotBeIndex,
                    location,
                    indexName,
                    row.Name.ToString(),
                    leaf.Type.Kind);
            }
        }

        if (allColumnsAreIndexes && !hasReadOnlyIndex)
        {
            yield return Diagnostic.Create(
                Diagnostics.AllAuxiliaryIndexesMustIncludeReadOnly,
                location,
                row.Name.ToString());
        }
    }

    private static IEnumerable<MibLeaf> GetColumns(
        AstType entryType,
        MibModule module,
        IReadOnlyDictionary<string, EntryDef> entryDefinitions)
    {
        if (!entryDefinitions.TryGetValue(entryType.ToString(), out var entryDefinition))
        {
            return [];
        }

        var columns = new List<MibLeaf>();
        foreach (var field in entryDefinition.Fields)
        {
            if (TryGetLeaf(module, field.name.ToString(), out var leaf))
            {
                columns.Add(leaf);
            }
        }

        return columns;
    }

    private static bool TryGetLeaf(MibModule module, string name, out MibLeaf leaf)
    {
        if (module.AllOids.TryGetValue(name, out var oid) &&
            module.AllItems.TryGetValue(oid, out var localItem) &&
            localItem is MibLeaf localLeaf)
        {
            leaf = localLeaf;
            return true;
        }

        if (module.TryImport(name, out var item, out _) && item is MibLeaf found)
        {
            leaf = found;
            return true;
        }

        leaf = null!;
        return false;
    }

    private static IEnumerable<Diagnostic> ValidateDirectStructure(
        Location location,
        ModuleDefinition definition)
    {
        var assigners = definition.Items.OfType<OidAssigner>().ToArray();

        foreach (var table in definition.Items.OfType<ConceptualTable>())
        {
            var tableName = table.Name.ToString();
            var directChildren = GetDirectChildren(assigners, tableName).ToArray();
            var validRows = directChildren
                .Where(child => IsDirectRowForTable(child, table))
                .ToArray();

            if (validRows.Length != 1)
            {
                yield return Diagnostic.Create(
                    Diagnostics.ConceptualTableMustHaveDirectRowAtOne,
                    location,
                    tableName);
            }

            if (directChildren.Length > 0 &&
                (directChildren.Length != 1 || validRows.Length != 1))
            {
                yield return Diagnostic.Create(
                    Diagnostics.ConceptualTableHasDirectChildConflict,
                    location,
                    tableName);
            }
        }

        foreach (var row in definition.Items.OfType<ConceptualRow>())
        {
            foreach (var diagnostic in ValidateDirectColumnArcs(location, assigners, row.Name.ToString()))
            {
                yield return diagnostic;
            }
        }

        foreach (var row in definition.Items.OfType<AugmentingConceptualRow>())
        {
            foreach (var diagnostic in ValidateDirectColumnArcs(location, assigners, row.Name.ToString()))
            {
                yield return diagnostic;
            }
        }
    }

    private static IEnumerable<Diagnostic> ValidateDirectColumnArcs(
        Location location,
        IReadOnlyList<OidAssigner> assigners,
        string rowName)
    {
        var directColumns = GetDirectChildren(assigners, rowName).ToArray();
        foreach (var column in directColumns.Where(column => column.Oid.Oid <= 0))
        {
            yield return Diagnostic.Create(
                Diagnostics.ColumnSubIdentifierMustBePositive,
                location,
                column.Name.ToString(),
                rowName);
        }

        foreach (var duplicateColumns in directColumns
                     .GroupBy(column => column.Oid.Oid)
                     .Where(columns => columns.Count() > 1))
        {
            yield return Diagnostic.Create(
                Diagnostics.ColumnSubIdentifierMustBeUnique,
                location,
                string.Join(", ", duplicateColumns.Select(column => column.Name.ToString())),
                rowName,
                duplicateColumns.Key);
        }
    }

    private static IEnumerable<OidAssigner> GetDirectChildren(
        IEnumerable<OidAssigner> assigners,
        string parentName)
    {
        return assigners.Where(assigner =>
            assigner.Oid.NumericArcs is null &&
            assigner.Oid.Components.Count == 0 &&
            assigner.Oid.Parent.ToString() == parentName);
    }

    private static bool IsDirectRowForTable(OidAssigner assigner, ConceptualTable table)
    {
        return assigner.Oid.Oid == 1 &&
               assigner switch
               {
                   ConceptualRow row => row.EntryType.ToString() == table.EntryType.ToString() &&
                                        row.Status == table.Status,
                   AugmentingConceptualRow row => row.EntryType.ToString() == table.EntryType.ToString() &&
                                                  row.Status == table.Status,
                   _ => false
               };
    }

    private static IEnumerable<Diagnostic> ValidateNotificationOids(
        Location location,
        ModuleDefinition definition,
        MibModule module)
    {
        // RFC 2578 section 8.5 applies to notifications newly defined by an
        // information module. Preserve established notification trees whose
        // parent is imported (for example IF-MIB's snmpTraps subtree).
        if (definition.Identifier.ToString() == "SNMPv2-MIB")
        {
            yield break;
        }

        var locallyDefinedOids = new HashSet<string>(definition.Items
            .OfType<OidAssigner>()
            .Select(assigner => assigner.Name.ToString()));

        foreach (var notification in definition.Items.OfType<NotificationType>())
        {
            var isNewlyDefined = notification.Oid.NumericArcs is not null ||
                                 locallyDefinedOids.Contains(notification.Oid.Parent.ToString());
            if (!isNewlyDefined ||
                !module.AllOids.TryGetValue(notification.Name.ToString(), out var oid) ||
                oid.Oid.Length < 2 ||
                oid.Oid[oid.Oid.Length - 2] == 0)
            {
                continue;
            }

            yield return Diagnostic.Create(
                Diagnostics.NotificationMustUseZeroPenultimateArc,
                location,
                notification.Name.ToString());
        }
    }

    private static bool HasVariableLengthSyntax(MibType type)
    {
        return type.Kind switch
        {
            TypeKind.ObjectIdentifier or TypeKind.Opaque or TypeKind.Bits => true,
            TypeKind.OctetString => type.Refinement is null ||
                                    type.Refinement.Constraints.Count == 0 ||
                                    type.Refinement.Constraints.Any(constraint => constraint.Min != constraint.Max),
            _ => false
        };
    }

    private static bool PermitsZeroLength(MibType type)
    {
        return type.Kind switch
        {
            TypeKind.Bits => true,
            TypeKind.OctetString => type.Refinement is null ||
                                    type.Refinement.Constraints.Count == 0 ||
                                    type.Refinement.Constraints.Any(constraint =>
                                        constraint is { Min: <= 0, Max: >= 0 }),
            _ => false
        };
    }
}