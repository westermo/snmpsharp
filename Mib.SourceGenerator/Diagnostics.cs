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
}
