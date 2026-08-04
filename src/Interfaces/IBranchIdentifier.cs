namespace SnmpSharpNet;

public interface IBranchIdentifier
{
    static abstract Oid Oid { get; }
    static abstract string BranchName { get; }
    static abstract string FullBranchName { get; }
}