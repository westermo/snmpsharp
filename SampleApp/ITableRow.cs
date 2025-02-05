using SnmpSharpNet;

namespace WeConfig.Services;

public interface ITableRow
{
    static abstract Oid RootOid { get; }
    static abstract IEnumerable<Oid> Indices { get; }
    static abstract IEnumerable<Oid> Fields { get; }
}