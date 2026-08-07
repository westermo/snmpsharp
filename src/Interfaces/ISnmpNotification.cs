namespace SnmpSharpNet;

public interface ISnmpNotification<out TSelf> :
    IBranchIdentifier,
    ISnmpOrderedBindings,
    IVbParseable<TSelf>
    where TSelf : IVbParseable<TSelf>;