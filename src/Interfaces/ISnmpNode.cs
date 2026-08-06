namespace SnmpSharpNet;

public interface ISnmpNode
{
    static abstract Oid Oid { get; }
    static abstract string BranchName { get; }
}