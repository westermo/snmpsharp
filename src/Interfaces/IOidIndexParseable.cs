using System;
using System.Collections.Generic;

namespace SnmpSharpNet;

public interface IOidIndexParseable<out TSelf>
{
    public static abstract TSelf? Parse(ReadOnlySpan<uint> indexes, IReadOnlyDictionary<uint, AsnType> values);
}