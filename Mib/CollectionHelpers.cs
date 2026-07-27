using System.Collections.Generic;
using System.Linq;

namespace SnmpSharpNet.Mib;

internal static class CollectionHelpers
{
    extension<T>(IEnumerable<T> items)
    {
        public int SequenceHash()
        {
            unchecked
            {
                int hash = 17;
                foreach (var item in items)
                    hash = hash * 31 + (item?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }

    extension<TKey, TValue>(IReadOnlyDictionary<TKey, TValue>? left) where TKey : notnull
    {
        public bool DictEquals(IReadOnlyDictionary<TKey, TValue>? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return left is null && right is null;
            if (left.Count != right.Count) return false;
            return left.All(kv =>
                right.TryGetValue(kv.Key, out var rv)
                && EqualityComparer<TValue>.Default.Equals(kv.Value, rv));
        }
    }
}
