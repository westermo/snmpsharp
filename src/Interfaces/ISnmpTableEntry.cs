using System;
using System.Collections.Generic;

namespace SnmpSharpNet;

public interface ISnmpTableEntry<TSelf>
{
    public static abstract Oid Root { get; }
    public static abstract string TableName { get; }
    public static abstract TSelf? EntryFromValues(ReadOnlySpan<uint> index, IReadOnlyDictionary<uint, AsnType> values);
    public static abstract TSelf[] TableFromValues(IReadOnlyDictionary<Oid, AsnType> values);
    IDictionary<Oid, AsnType> EntryToValues();
    public static abstract IDictionary<Oid, AsnType> TableToValues(TSelf[] entries);
}