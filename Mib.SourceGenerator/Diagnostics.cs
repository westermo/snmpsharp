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
    public static readonly DiagnosticDescriptor AttributeFilterError = new(
        id: "SNMP003",
        title: "Failed to apply MibOids filter",
        messageFormat: "{0}",
        category: "Design", DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
