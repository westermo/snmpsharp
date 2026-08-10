using System.Collections.Generic;

namespace SnmpSharpNet;

public interface IOidParseable<out TSelf>
{
    public static abstract TSelf? Parse(IReadOnlyDictionary<Oid, AsnType> values);
}