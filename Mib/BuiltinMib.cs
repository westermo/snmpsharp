using System;
using System.Collections.Generic;
using System.Linq;

namespace SnmpSharpNet.Mib;

public class BuiltinMib : IMibThatExports
{
    public Dictionary<MibItemIdent, MibItem> Items { get; } = [];
    public readonly string Name;
    public readonly Dictionary<string, MibType> Types;

    private readonly Dictionary<string, MibItemIdent> oidByName = [];
    private readonly HashSet<string> keywords;

    public BuiltinMib(
        string name,
        Dictionary<string, uint[]> oids,
        Dictionary<string, MibType> types,
        HashSet<string> keywords,
        Dictionary<string, (uint[], MibType)>? leaves = null)
    {
        Name = name;
        Types = types;
        this.keywords = keywords;

        foreach (var kvp in oids)
        {
            var ident = new MibItemIdent(kvp.Value, [$"<{name}>", kvp.Key]);
            oidByName[kvp.Key] = ident;
            Items[ident] = new MibItem(ident);
        }

        if (leaves is not null)
        {
            foreach (var kvp in leaves)
            {
                var oid = kvp.Value.Item1;
                var ident = new MibItemIdent(oid, [$"<{name}>", kvp.Key]);
                oidByName[kvp.Key] = ident;
                Items[ident] = new MibLeaf(ident, kvp.Value.Item2, Ast.SMIv2Accessibility.ReadOnly);
            }
        }
    }

    public bool TryImport(string ident, out MibItem? item, out MibType? type)
    {
        item = null;
        type = null;
        if (oidByName.TryGetValue(ident, out var itemIdent))
        {
            item = Items[itemIdent];
            return true;
        }
        if (Types.TryGetValue(ident, out type))
        {
            return true;
        }
        if (keywords.Contains(ident))
        {
            return true;
        }
        return false;
    }

    // RFC 2578 - SNMPv2-SMI
    public static readonly BuiltinMib SNMPv2SMI = new(
        "SNMPv2-SMI",
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
            ["zeroDotZero"] = [0, 0],
        },
        new Dictionary<string, MibType>
        {
            ["Integer32"] = MibType.Integer32,
            ["IpAddress"] = MibType.IpAddress,
            ["Counter32"] = MibType.Counter32,
            ["Gauge32"] = MibType.Gauge32,
            ["Unsigned32"] = MibType.Unsigned32,
            ["TimeTicks"] = MibType.TimeTicks,
            ["Opaque"] = MibType.Opaque,
            ["Counter64"] = MibType.Counter64,
            ["ExtUTCTime"] = MibType.OctetString.WithSize(11).WithSize(13),
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
        "SNMPv2-CONF",
        [],
        [],
        [
            "MODULE-COMPLIANCE",
            "OBJECT-GROUP",
            "NOTIFICATION-GROUP",
        ]
    );

    // RFC 3418 - SNMPv2-MIB
    public static readonly BuiltinMib SNMPv2MIB = new(
        "SNMPv2-MIB",
        new Dictionary<string, uint[]>
        {
            ["snmpMIB"] = [1, 3, 6, 1, 6, 3, 1],
            ["snmpMIBObjects"] = [1, 3, 6, 1, 6, 3, 1, 1],
            ["snmpTraps"] = [1, 3, 6, 1, 6, 3, 1, 1, 5],
        },
        [],
        []
    );

    // RFC 2579 - SNMPv2-TC
    public static readonly BuiltinMib SNMPv2TC = new(
        "SNMPv2-TC",
        [],
        new Dictionary<string, MibType>
        {
            ["DisplayString"] = MibType.OctetString.WithSize(0, 255),
            ["PhysAddress"] = MibType.OctetString,
            ["MacAddress"] = MibType.OctetString.WithSize(6),
            ["TruthValue"] = MibType.Integer32Enum
                .WithNamedValue("true", 1)
                .WithNamedValue("false", 2),
            ["TestAndIncr"] = MibType.Integer32.WithRange(0, 2147483647),
            ["AutonomousType"] = MibType.ObjectIdentifier,
            ["VariablePointer"] = MibType.ObjectIdentifier,
            ["RowPointer"] = MibType.ObjectIdentifier,
            ["RowStatus"] = MibType.Integer32Enum
                .WithNamedValue("active", 1)
                .WithNamedValue("notInService", 2)
                .WithNamedValue("notReady", 3)
                .WithNamedValue("createAndGo", 4)
                .WithNamedValue("createAndWait", 5)
                .WithNamedValue("destroy", 6),
            ["TimeStamp"] = MibType.TimeTicks,
            ["TimeInterval"] = MibType.Integer32.WithRange(0, 2147483647),
            ["DateAndTime"] = MibType.OctetString.WithSize(8).WithSize(11),
            ["StorageType"] = MibType.Integer32Enum
                .WithNamedValue("other", 1)
                .WithNamedValue("volatile", 2)
                .WithNamedValue("nonVolatile", 3)
                .WithNamedValue("permanent", 4)
                .WithNamedValue("readOnly", 5),
            ["TDomain"] = MibType.ObjectIdentifier,
            ["TAddress"] = MibType.OctetString.WithSize(1, 255),
        },
        [
            "TEXTUAL-CONVENTION",
        ]
    );

    // RFC 2021 - RMON2-MIB
    // This one IMPORTS stuff from the SNMPv1 era. Let's avoid that.
    public static readonly BuiltinMib RMON2MIB = new(
        "RMON2-MIB",
        new Dictionary<string, uint[]>
        {
            ["rmon"] = [1, 3, 6, 1, 2, 1, 16],
            ["rmonEvents"] = [1, 3, 6, 1, 2, 1, 16, 0],
            ["statistics"] = [1, 3, 6, 1, 2, 1, 16, 1],
            ["history"] = [1, 3, 6, 1, 2, 1, 16, 2],
            ["alarm"] = [1, 3, 6, 1, 2, 1, 16, 3],
            ["hosts"] = [1, 3, 6, 1, 2, 1, 16, 4],
            ["hostTopN"] = [1, 3, 6, 1, 2, 1, 16, 5],
            ["matrix"] = [1, 3, 6, 1, 2, 1, 16, 6],
            ["filter"] = [1, 3, 6, 1, 2, 1, 16, 7],
            ["capture"] = [1, 3, 6, 1, 2, 1, 16, 8],
            ["event"] = [1, 3, 6, 1, 2, 1, 16, 9],
            ["tokenRing"] = [1, 3, 6, 1, 2, 1, 16, 10],
            ["protocolDir"] = [1, 3, 6, 1, 2, 1, 16, 11],
            ["protocolDistribution"] = [1, 3, 6, 1, 2, 1, 16, 12],
            ["addressMap"] = [1, 3, 6, 1, 2, 1, 16, 13],
            ["nlHost"] = [1, 3, 6, 1, 2, 1, 16, 14],
            ["nlMatrix"] = [1, 3, 6, 1, 2, 1, 16, 15],
            ["alHost"] = [1, 3, 6, 1, 2, 1, 16, 16],
            ["alMatrix"] = [1, 3, 6, 1, 2, 1, 16, 17],
            ["usrHistory"] = [1, 3, 6, 1, 2, 1, 16, 18],
            ["probeConfig"] = [1, 3, 6, 1, 2, 1, 16, 19],
            ["rmonConformance"] = [1, 3, 6, 1, 2, 1, 16, 20],
        },
        new Dictionary<string, MibType>
        {
            ["ZeroBasedCounter32"] = MibType.Gauge32,
            ["LastCreateTime"] = MibType.TimeTicks,
            ["TimeFilter"] = MibType.TimeTicks,
            ["DataSource"] = MibType.ObjectIdentifier,
            ["ControlString"] = MibType.OctetString,
        },
        []
    );

    public static readonly IReadOnlyDictionary<string, IMibThatExports> All = new Dictionary<string, IMibThatExports>()
    {
        ["SNMPv2-SMI"] = SNMPv2SMI,
        ["SNMPv2-TC"] = SNMPv2TC,
        ["SNMPv2-CONF"] = SNMPv2CONF,
        ["SNMPv2-MIB"] = SNMPv2MIB,
        ["RMON2-MIB"] = RMON2MIB,
    };
}
