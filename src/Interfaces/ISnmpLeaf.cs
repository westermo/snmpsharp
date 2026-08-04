namespace SnmpSharpNet;

public interface ISnmpLeaf<out TAsn> : IBranchIdentifier
{
    public static abstract TAsn? Get(AsnType value);
}