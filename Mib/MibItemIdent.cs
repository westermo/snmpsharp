using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

public readonly struct MibItemIdent(uint[] oid, string?[] path)
{
    public readonly uint[] Oid = oid;
    public readonly string?[] OidNames = path;

    public string? Name => OidNames[OidNames.Length-1];
    public string Path => string.Join(".", OidNames);

    public MibItemIdent Add(uint oidSegment, string? name = null)
    {
        return new MibItemIdent(
            Oid.Add(oidSegment),
            OidNames.Add(name)
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

    public override string ToString()
    {
        var unnameds = Oid.Length - OidNames.Length;
        string?[] names = [..Enumerable.Repeat<string?>(null, unnameds), ..OidNames];

        return string.Join(".",
            Enumerable.Zip(names, Oid, (name, oid) => {
                return name is null ? $"{oid}" : $"{name}({oid})";
            })
        );
    }


    // See Annex A "Top-level arcs of the OID tree" of ITU-T X.660
    // and 3.5. "OBJECT IDENTIFIER values" of RFC2578
    // https://www.rfc-editor.org/rfc/rfc2578.html#section-3.5
    public static MibItemIdent RootItut = new([1], [".itu-t"]);
    public static MibItemIdent RootIso = new([1], [".iso"]);
    public static MibItemIdent RootJoint = new([1], [".joint-iso-itu-t"]);
    public static IReadOnlyDictionary<string, MibItemIdent> TopLevelArcs = new Dictionary<string, MibItemIdent>
    {
        { "ccitt", RootItut },
        { "iso", RootIso },
        { "joint-iso-ccitt", RootJoint },
    };
}