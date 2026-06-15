using System;
using System.Linq;

namespace SnmpSharpNet.Mib;

public static class ArrayExtensions
{
    extension<T>(T[] oid)
    {
        public T[] Add(T value)
        {
            var newOid = new T[oid.Length+1];
            Array.Copy(oid, newOid, oid.Length);
            newOid[oid.Length] = value;
            return newOid;
        }
    }
}

public readonly struct MibItemIdent(uint[] oid, string[] path)
{
    public readonly uint[] Oid = oid;
    public readonly string[] Path = path;

    public string Name => Path[Path.Length-1];
    public string Key => string.Join(".", Path);

    public MibItemIdent Add(uint oidSegment, string pathSegment)
    {
        return new MibItemIdent(
            Oid.Add(oidSegment),
            Path.Add(pathSegment)
        );
    }

    public override bool Equals(object obj)
    {
        if (obj is not MibItemIdent other) { return false; }

        return Oid.SequenceEqual(other.Oid);
    }
    
    public override int GetHashCode()
    {
        return Oid.Aggregate(0, (current, t) => current ^ (t > int.MaxValue ? int.MaxValue : (int)t));
    }
}