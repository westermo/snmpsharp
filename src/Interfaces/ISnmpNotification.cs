using System.Collections.Generic;

namespace SnmpSharpNet;

public interface ISnmpNotification<out TSelf> : IBranchIdentifier where TSelf : ISnmpNotification<TSelf>
{
    public static abstract TSelf? FromValues(IReadOnlyDictionary<Oid, AsnType> values);
    IDictionary<Oid, AsnType> ToValues();
}