namespace SnmpSharpNet;

public interface ISnmpNotification<out TSelf> : IBranchIdentifier, ISnmpBindings, IOidParseable<TSelf>
    where TSelf : IOidParseable<TSelf>;