using System.Collections.Generic;

namespace SnmpSharpNet;

/// <summary>
///     A group of SNMP to be queried and stored
/// </summary>
public interface IOidGroup<TSelf>
{
    static abstract IEnumerable<Oid> SimpleOids { get; }
    static abstract IEnumerable<Oid> BatchOids  { get; }

    static abstract TSelf? FromValues(IReadOnlyDictionary<Oid, AsnType> values);
    IDictionary<Oid, AsnType> ToValues();
}