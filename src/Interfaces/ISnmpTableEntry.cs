namespace SnmpSharpNet;

public interface ISnmpTableEntry<out TSelf> : ISnmpNode, ISnmpBindings, IOidIndexParseable<TSelf>;