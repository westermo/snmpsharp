using Microsoft.CodeAnalysis;

internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor ParseError = new(
        id: "SNMP001",
        title: "Failed to parse or construct SNMP MIB module",
        messageFormat: "{0}",
        category: "Design", DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor MibNotFound = new(
        id: "SNMP002",
        title: "Failed to find SNMP MIB module",
        messageFormat: "{0}",
        category: "Design", DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor FilenameMismatch = new(
        id: "SNMP004",
        title: "MIB filename does not match module identifier",
        messageFormat: "{0}",
        category: "Design", DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor ImpliedIndexMustBeLast = new(
        id: "SNMP005",
        title: "IMPLIED INDEX component must be last",
        messageFormat: "INDEX component '{0}' in '{1}' uses IMPLIED but is not the final component",
        category: "Design", DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor ImpliedIndexMustBeVariableLength = new(
        id: "SNMP006",
        title: "IMPLIED INDEX component must have variable-length syntax",
        messageFormat: "INDEX component '{0}' in '{1}' uses IMPLIED but does not have variable-length syntax",
        category: "Design", DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor CounterCannotBeIndex = new(
        id: "SNMP007",
        title: "Counter cannot be an INDEX component",
        messageFormat: "INDEX component '{0}' in '{1}' has prohibited syntax '{2}'",
        category: "Design", DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor ConceptualTableAndRowMustBeNotAccessible = new(
        id: "SNMP008",
        title: "Conceptual tables and rows must be not-accessible",
        messageFormat: "Conceptual {0} '{1}' must have MAX-ACCESS not-accessible",
        category: "Design", DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor AuxiliaryIndexMustBeNotAccessible = new(
        id: "SNMP009",
        title: "Auxiliary INDEX object must be not-accessible",
        messageFormat: "Auxiliary INDEX object '{0}' in '{1}' must have MAX-ACCESS not-accessible",
        category: "Design", DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor ReadCreateAndReadWriteColumnsCannotMix = new(
        id: "SNMP010",
        title: "read-create and read-write columns cannot mix",
        messageFormat: "Conceptual row '{0}' contains both read-create and read-write columns",
        category: "Design", DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor NotificationObjectMustBeAccessible = new(
        id: "SNMP011",
        title: "Notification OBJECTS member must be accessible",
        messageFormat: "OBJECTS member '{0}' in notification '{1}' must not have MAX-ACCESS not-accessible",
        category: "Design", DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor AllAuxiliaryIndexesMustIncludeReadOnly = new(
        id: "SNMP012",
        title: "All-auxiliary rows must include a read-only index",
        messageFormat: "Conceptual row '{0}' has only auxiliary INDEX objects, so at least one must have MAX-ACCESS read-only",
        category: "Design", DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor OpaqueCannotBeIndex = new(
        id: "SNMP013",
        title: "Opaque cannot be an INDEX component",
        messageFormat: "INDEX component '{0}' in '{1}' has prohibited syntax 'Opaque'",
        category: "Design", DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor ImpliedIndexCannotBeEmpty = new(
        id: "SNMP014",
        title: "IMPLIED INDEX component cannot permit an empty value",
        messageFormat: "INDEX component '{0}' in '{1}' uses IMPLIED but may have zero length",
        category: "Design", DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor NotificationMustUseZeroPenultimateArc = new(
        id: "SNMP015",
        title: "New notifications must use a zero penultimate sub-identifier",
        messageFormat: "Notification '{0}' must have a zero-valued penultimate sub-identifier",
        category: "Design", DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor ConceptualTableMustHaveDirectRowAtOne = new(
        id: "SNMP016",
        title: "Conceptual table must have its row at direct child one",
        messageFormat: "Conceptual table '{0}' must have its sole direct child be a conceptual row at sub-identifier 1",
        category: "Design", DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor ConceptualTableHasDirectChildConflict = new(
        id: "SNMP017",
        title: "Conceptual table has conflicting direct children",
        messageFormat: "Conceptual table '{0}' may only have its conceptual row as direct child sub-identifier 1",
        category: "Design", DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor ColumnSubIdentifierMustBePositive = new(
        id: "SNMP018",
        title: "Conceptual row column sub-identifier must be positive",
        messageFormat: "Direct column '{0}' in conceptual row '{1}' must use a positive sub-identifier",
        category: "Design", DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor ColumnSubIdentifierMustBeUnique = new(
        id: "SNMP019",
        title: "Conceptual row column sub-identifiers must be unique",
        messageFormat: "Direct columns '{0}' in conceptual row '{1}' use duplicate sub-identifier {2}",
        category: "Design", DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
