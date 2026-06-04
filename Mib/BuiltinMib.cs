using System.Collections.Generic;

namespace SnmpSharpNet.Mib;

public class BuiltinMib(
    Dictionary<string, uint[]> oids,
    Dictionary<string, string> types,
    HashSet<string> keywords) : IMibThatExports
{
    public bool TryImport(string ident, out uint[]? oid, out string? syntax)
    {
        if (oids.TryGetValue(ident, out oid))
        {
            syntax = null;
            return true;
        }
        if (types.TryGetValue(ident, out syntax))
        {
            oid = null;
            return true;
        }
        if (keywords.Contains(ident))
        {
            oid = null;
            syntax = null;
            return true;
        }
        oid = null;
        syntax = null;
        return false;
    }

    // RFC 2578 - SNMPv2-SMI
    public static readonly BuiltinMib SNMPv2SMI = new(
        new Dictionary<string, uint[]>
        {
            ["org"] = [1, 3],
            ["dod"] = [1, 3, 6],
            ["internet"] = [1, 3, 6, 1],
            ["directory"] = [1, 3, 6, 1, 1],
            ["mgmt"] = [1, 3, 6, 1, 2],
            ["mib-2"] = [1, 3, 6, 1, 2, 1],
            ["transmission"] = [1, 3, 6, 1, 2, 1, 10],
            ["experimental"] = [1, 3, 6, 1, 3],
            ["private"] = [1, 3, 6, 1, 4],
            ["enterprises"] = [1, 3, 6, 1, 4, 1],
            ["security"] = [1, 3, 6, 1, 5],
            ["snmpV2"] = [1, 3, 6, 1, 6],
            ["snmpDomains"] = [1, 3, 6, 1, 6, 1],
            ["snmpProxys"] = [1, 3, 6, 1, 6, 2],
            ["snmpModules"] = [1, 3, 6, 1, 6, 3],
        },
        new Dictionary<string, string>
        {
            ["Integer32"] = "INTEGER (-2147483648..2147483647)",
            ["IpAddress"] = "[APPLICATION 0] IMPLICIT OCTET STRING (SIZE (4))",
            ["Counter32"] = "[APPLICATION 1] IMPLICIT INTEGER (0..4294967295)",
            ["Gauge32"] = "[APPLICATION 2] IMPLICIT INTEGER (0..4294967295)",
            ["Unsigned32"] = "[APPLICATION 2] IMPLICIT INTEGER (0..4294967295)",
            ["TimeTicks"] = "[APPLICATION 3] IMPLICIT INTEGER (0..4294967295)",
            ["Opaque"] = "[APPLICATION 4] IMPLICIT OCTET STRING",
            ["Counter64"] = "[APPLICATION 6] IMPLICIT INTEGER (0..18446744073709551615)",
            ["ExtUTCTime"] = "OCTET STRING(SIZE(11 | 13))",
        },
        [
            "MODULE-IDENTITY",
            "OBJECT-IDENTITY",
            "OBJECT-TYPE",
            "NOTIFICATION-TYPE",
        ]
    );

    // RFC 2580 - SNMPv2-CONF
    public static readonly BuiltinMib SNMPv2CONF = new(
        [],
        [],
        [
            "MODULE-COMPLIANCE",
            "OBJECT-GROUP",
            "NOTIFICATION-GROUP",
        ]
    );

    // RFC 2579 - SNMPv2-TC
    public static readonly BuiltinMib SNMPv2TC = new(
        [],
        new Dictionary<string, string>
        {
            ["DisplayString"] = "OCTET STRING (SIZE (0..255))",
            ["PhysAddress"] = "OCTET STRING",
            ["MacAddress"] = "OCTET STRING (SIZE (6))",
            ["TruthValue"] = "INTEGER { true(1), false(2) }",
            ["TestAndIncr"] = "INTEGER (0..2147483647)",
            ["AutonomousType"] = "OBJECT IDENTIFIER",
            ["VariablePointer"] = "OBJECT IDENTIFIER",
            ["RowPointer"] = "OBJECT IDENTIFIER",
            ["RowStatus"] = "INTEGER { active(1), notInService(2), notReady(3), createAndGo(4), createAndWait(5), destroy(6) }",
            ["TimeStamp"] = "TimeTicks",
            ["TimeInterval"] = "INTEGER (0..2147483647)",
            ["DateAndTime"] = "OCTET STRING (SIZE (8 | 11))",
            ["StorageType"] = "INTEGER { other(1), volatile(2), nonVolatile(3), permanent(4), readOnly(5) }",
            ["TDomain"] = "OBJECT IDENTIFIER",
            ["TAddress"] = "OCTET STRING (SIZE (1..255))",
        },
        [
            "TEXTUAL-CONVENTION",
        ]
    );

    // RFC 2863 - IF-MIB
    public static readonly BuiltinMib IFMIB = new(
        [],
        new Dictionary<string, string>
        {
            ["InterfaceIndex"] = "Integer32",
        },
        []
    );

    // RFC 2233 - IANAifType-MIB
    public static readonly BuiltinMib IANAifTypeMIB = new(
        [],
        new Dictionary<string, string>
        {
            ["IANAifType"] = "INTEGER",
        },
        []
    );

    public static readonly IReadOnlyDictionary<string, IMibThatExports> All = new Dictionary<string, IMibThatExports>()
    {
        ["SNMPv2-SMI"] = SNMPv2SMI,
        ["SNMPv2-TC"] = SNMPv2TC,
        ["SNMPv2-CONF"] = SNMPv2CONF,
        ["IF-MIB"] = IFMIB,
        ["IANAifType-MIB"] = IANAifTypeMIB,
    };
}
