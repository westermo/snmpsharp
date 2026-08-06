using System.Collections.Generic;

namespace SnmpSharpNet;

public interface ISnmpTable<TEntry> : IBranchIdentifier
{
    public static abstract TEntry FromValues(IReadOnlyDictionary<Oid, AsnType> values);
    public static abstract IDictionary<Oid, AsnType> ToValues(TEntry entries);
}