namespace SnmpSharpNet;

public interface IBranchIdentifier : ISnmpNode
{
    static abstract string FullBranchName { get; }
}