using System.Collections.Generic;

namespace SnmpSharpNet;

/// <summary>
///     A group of SNMP OIDs to be queried and stored.
///     <see cref="FromValues"/> always returns a populated instance;
///     unmatched scalars hold <c>NoSuchInstance</c> and tables are empty.
/// </summary>
public interface IOidGroup<TSelf>
{
    static abstract IEnumerable<Oid> SimpleOids { get; }
    static abstract IEnumerable<Oid> BatchOids  { get; }

    static abstract TSelf FromValues(IReadOnlyDictionary<Oid, AsnType> values);
    IDictionary<Oid, AsnType> ToValues();
}