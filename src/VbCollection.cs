// This file is part of SNMP#NET.
// 
// SNMP#NET is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// SNMP#NET is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with SNMP#NET.  If not, see <http://www.gnu.org/licenses/>.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SnmpSharpNet;

/// <summary>
///     Variable Binding collection
/// </summary>
public class VbCollection : AsnType, IEnumerable<Vb>
{
    private readonly List<Vb> _vbs;

    /// <summary>
    ///     Standard constructor
    /// </summary>
    public VbCollection()
    {
        // this is the SMI type for the VarBind sequence
        Type = SnmpConstants.SMI_SEQUENCE;
        // list to store VarBind
        _vbs = new List<Vb>();
    }

    /// <summary>
    ///     Copy constructor
    /// </summary>
    public VbCollection(IEnumerable<Vb> second)
    {
        // this is the SMI type for the VarBind sequence
        Type = SnmpConstants.SMI_SEQUENCE;
        // list to store VarBind
        _vbs = new List<Vb>();
        foreach (var v in second) _vbs.Add(v);
    }

    /// <summary>
    ///     Get number of VarBind entries in the collection
    /// </summary>
    public int Count => _vbs.Count;

    /// <summary>
    ///     Indexed access to VarBind collection.
    /// </summary>
    /// <param name="index">Index position of the VarBind entry</param>
    /// <returns>VarBind entry at the specified index</returns>
    /// <exception cref="IndexOutOfRangeException">Thrown when index is outside the bounds of the collection</exception>
    public Vb this[int index]
    {
        get
        {
            switch (index)
            {
                case < 0 when index >= _vbs.Count:
                    throw new IndexOutOfRangeException("Requested VarBind entry is outside the collection range.");
                default:
                    return _vbs[index];
            }
        }
    }

    /// <summary>
    ///     Access variable bindings using Vb Oid value
    /// </summary>
    /// <param name="oid">Required Oid value</param>
    /// <returns>Variable binding with the Oid matching the parameter, otherwise null</returns>
    public Vb? this[Oid oid]
    {
        get
        {
            if (!ContainsOid(oid))
                return null;
            foreach (var v in _vbs)
                if (v.Oid is not null && v.Oid.Equals(oid))
                    return v;
            return null;
        }
    }

    /// <summary>
    ///     Access variable bindings using Vb Oid value in the string format
    /// </summary>
    /// <param name="oid">Oid value in string representation</param>
    /// <returns>Variable binding with the Oid matching the parameter, otherwise null</returns>
    public Vb? this[string oid]
    {
        get
        {
            foreach (var v in _vbs)
                if (v.Oid is not null && v.Oid.Equals(oid))
                    return v;
            return null;
        }
    }

    /// <summary>
    ///     Get enumerator.
    /// </summary>
    /// <returns>Enumerator</returns>
    public IEnumerator<Vb> GetEnumerator()
    {
        return _vbs.GetEnumerator();
    }

    /// <summary>
    ///     Get enumerator.
    /// </summary>
    /// <returns>Enumerator</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return _vbs.GetEnumerator();
    }

    /// <summary>
    ///     Reset the VarBind collection.
    /// </summary>
    public void Clear()
    {
        _vbs.Clear();
    }

    /// <summary>
    ///     Remove VarBind entry for the specified position in the collection.
    /// </summary>
    /// <param name="pos">Position of the entry to remove (zero based)</param>
    /// <exception cref="IndexOutOfRangeException">Thrown when position is outside the bounds of the collection</exception>
    public void RemoveAt(int pos)
    {
        switch (pos)
        {
            case < 0 when pos >= _vbs.Count:
                throw new IndexOutOfRangeException("Requested VarBind entry is outside the collection range.");
            default:
                _vbs.RemoveAt(pos);
                break;
        }
    }

    /// <summary>
    ///     Insert VarBind item at specific location
    /// </summary>
    /// <param name="pos">Position (zero based)</param>
    /// <param name="item">VarBind item to insert</param>
    /// <exception cref="IndexOutOfRangeException">Thrown when position is outside the bounds of the collection</exception>
    public void Insert(int pos, Vb item)
    {
        switch (pos)
        {
            case < 0 when pos >= _vbs.Count:
                throw new IndexOutOfRangeException("Requested VarBind position is outside the collection range.");
            default:
                _vbs.Insert(pos, item);
                break;
        }
    }

    /// <summary>
    ///     Add variable binding to the collection
    /// </summary>
    /// <param name="vb">VarBind item to add to the collection</param>
    public void Add(Vb vb)
    {
        _vbs.Add(vb);
    }

    /// <summary>
    ///     Create a new Variable Binding with the supplied Oid and SnmpNull value and add it to the end of Vb collection
    /// </summary>
    /// <param name="oid">Oid value in dotted decimal format</param>
    public void Add(string oid)
    {
        var o = new Oid(oid);
        var v = new Vb(o);
        Add(v);
    }

    /// <summary>
    ///     Add Vb with the supplied OID and value of SnmpNull to the end of the Vb collection
    /// </summary>
    /// <param name="oid">OID to assign to the new Vb</param>
    public void Add(Oid? oid)
    {
        if (oid is null)
            throw new ArgumentNullException(nameof(oid), "Can't create vb entry with null Oid.");
        var v = new Vb(oid);
        Add(v);
    }

    /// <summary>
    ///     Create a new Variable Binding with the supplied OID and value and add to the end of the Vb collection
    /// </summary>
    /// <param name="oid">OID to assign to the new Vb</param>
    /// <param name="value">SNMP value to assign to the new Vb</param>
    public void Add(Oid oid, AsnType value)
    {
        var v = new Vb(oid, value);
        Add(v);
    }

    /// <summary>
    ///     Add content of the enumerable collection of Variable Bindings to the end of the Vb collection.
    /// </summary>
    /// <param name="varList">Variable Binding collection.</param>
    public void Add(IEnumerable<Vb>? varList)
    {
        if (varList is null) return;
        foreach (var v in varList)
            Add(v);
    }

    /// <summary>
    ///     Construct Variable Bindings from the enumerable collections of OIDs, each with the value of SnmpNull and add to the
    ///     end of the Vb collection
    /// </summary>
    /// <param name="oidList">Enumerable collection of OIDs</param>
    public void Add(IEnumerable<Oid>? oidList)
    {
        if (oidList is null) return;
        foreach (var o in oidList)
        {
            var v = new Vb(o);
            Add(v);
        }
    }

    /// <summary>
    ///     Does the collection contain variable binding with Oid matching the parameter
    /// </summary>
    /// <param name="oid">Oid to search for</param>
    /// <returns>True if collection contains a variable binding with the Oid match the parameter, otherwise false.</returns>
    public bool ContainsOid(Oid? oid) => oid is not null && _vbs.Any(v => v.Oid is not null && v.Oid.Equals(oid));

    /// <summary>
    ///     Get array of Oid keys stored in collection
    /// </summary>
    /// <returns>Array of clone Oid keys</returns>
    public Oid[] OidArray()
    {
        return _vbs.Select(v => v.Oid?.Clone() as Oid).OfType<Oid>().ToArray();
    }

    /// <summary>
    ///     Duplicate Vbs object
    /// </summary>
    /// <returns>Duplicate of the Vbs object</returns>
    public override object Clone()
    {
        return new VbCollection(this);
    }

    #region Encode and decode methods

    /// <summary>
    ///     Encode VarBind collection sequence
    /// </summary>
    /// <param name="buffer">Target buffer. Encoded VarBind collection is appended.</param>
    public override void encode(MutableByte buffer)
    {
        var tmp = new MutableByte();
        foreach (var v in _vbs) v.encode(tmp);
        BuildHeader(buffer, Type, tmp.Length);
        buffer.Append(tmp);
    }

    /// <summary>
    ///     Decode VarBind collection sequence.
    /// </summary>
    /// <param name="buffer">Buffer containing BER encoded VarBind collection</param>
    /// <param name="offset">Offset to start decoding from</param>
    /// <returns>New offset of the position following the VarBind collection</returns>
    /// <exception cref="SnmpException">Thrown when parsed ASN.1 type is not a VarBind collection sequence type</exception>
    public override int decode(byte[] buffer, int offset)
    {
        var b = ParseHeader(buffer, ref offset, out var headerLen);
        if (b != Type)
            throw new SnmpException("Invalid ASN.1 encoding for variable binding list.");

        _vbs.Clear();

        var oldOffset = offset;
        while (headerLen > 0)
        {
            var vb = new Vb();
            offset = vb.decode(buffer, offset);

            headerLen -= offset - oldOffset;
            oldOffset = offset;
            _vbs.Add(vb);
        }

        return offset;
    }

    public override int encode(Span<byte> buffer)
    {
        var written = BuildHeader(buffer, Type, MemberByteLength());
        foreach (var v in _vbs) written += v.encode(buffer[written..]);
        return written;
    }

    public override int ByteLength
    {
        get
        {
            var len = MemberByteLength();
            return len + HeaderSize(len);
        }
    }

    private int MemberByteLength()
    {
        var len = 0;
        foreach (var v in _vbs) len += v.ByteLength;
        return len;
    }

    public override int decode(Span<byte> buffer, int offset)
    {
        var b = ParseHeader(buffer, ref offset, out var headerLen);
        if (b != Type)
            throw new SnmpException("Invalid ASN.1 encoding for variable binding list.");

        _vbs.Clear();

        var oldOffset = offset;
        while (headerLen > 0)
        {
            var vb = new Vb();
            offset = vb.decode(buffer, offset);

            headerLen -= offset - oldOffset;
            oldOffset = offset;
            _vbs.Add(vb);
        }

        return offset;
    }

    #endregion
}