namespace SnmpSharpNet;

public interface ISnmpLeaf<out TAsn> : IBranchIdentifier, IOidParseable<TAsn>;