using System.Collections.Generic;

namespace SnmpSharpNet;

public interface ISnmpTable<out TSelf, TEntry> : IBranchIdentifier,
    IList<TEntry>,
    IOidParseable<TSelf>
    where TEntry : ISnmpTableEntry<TEntry>,
    ISnmpBindings;