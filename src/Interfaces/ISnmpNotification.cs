namespace SnmpSharpNet;

public interface ISnmpNotification<out TSelf> :
    IBranchIdentifier,
    ISnmpOrderedBindings,
    IPduParseable<TSelf>
    where TSelf : IPduParseable<TSelf>
{
    Oid InstanceOid { get; }
}