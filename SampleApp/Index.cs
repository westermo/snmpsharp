// An index represents the N last identifiers in an OID where N is the number of index fields for the SNMP field value OID.
// Suppose we have this oid: 1.0.8802.1.1.2.1.4.1.1.4.16702300.5.2 = lldpRemChassisIdSubType.16702300.5.2
// Then the index is [16702300, 5, 2]

using System.Globalization;
using SnmpSharpNet;

public class Index : IComparable<Index>, IComparable
{
    private readonly uint[] m_fields;

    public Index(Oid valueOid, int indexFieldCount)
    {
        ArgumentNullException.ThrowIfNull(valueOid);

        if (indexFieldCount < 1 || indexFieldCount > valueOid.Length)
            throw new ArgumentOutOfRangeException(nameof(indexFieldCount),
                "Index field count must be > 0 and <= valueOid.Length");

        m_fields = new uint[indexFieldCount];
        for (var i = 0; i < indexFieldCount; ++i)
        {
            m_fields[i] = valueOid[valueOid.Length - indexFieldCount + i];
        }
    }

    public override bool Equals(object? obj)
    {
        var typed = obj as Index;

        return m_fields.Length == typed?.m_fields.Length && m_fields.Zip(typed.m_fields, (l, r) => l == r).All(b => b);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return m_fields.Aggregate(0, (current, t) => (current * 397) ^ (int)t);
        }
    }

    public override string ToString() =>
        string.Join(".", m_fields.Select(v => v.ToString(CultureInfo.InvariantCulture)));

    public int CompareTo(Index? other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var cmp = m_fields.Length - other.m_fields.Length;
        if (cmp != 0)
            return cmp;

        for (var i = 0; i < m_fields.Length; ++i)
        {
            if (m_fields[i] < other.m_fields[i])
                return -1;

            if (m_fields[i] > other.m_fields[i])
                return 1;
        }

        return 0;
    }

    public int CompareTo(object? obj)
    {
        if (ReferenceEquals(null, obj)) return 1;
        if (ReferenceEquals(this, obj)) return 0;
        return obj is Index other
            ? CompareTo(other)
            : throw new ArgumentException($"Object must be of type {nameof(Index)}");
    }

    public static bool operator <(Index? left, Index? right) => Comparer<Index>.Default.Compare(left, right) < 0;
    public static bool operator >(Index? left, Index? right) => Comparer<Index>.Default.Compare(left, right) > 0;
    public static bool operator <=(Index? left, Index? right) => Comparer<Index>.Default.Compare(left, right) <= 0;
    public static bool operator >=(Index? left, Index? right) => Comparer<Index>.Default.Compare(left, right) >= 0;
}