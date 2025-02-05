using System;

namespace SnmpSharpNet;

/// <summary>
///     Represents SNMP sequence
/// </summary>
[Serializable]
public class Sequence : AsnType, ICloneable
{
    /// <summary>
    ///     data buffer
    /// </summary>
    protected byte[] _data = [];

    /// <summary>
    ///     Constructor
    /// </summary>
    public Sequence()
    {
        _asnType = SnmpConstants.SMI_SEQUENCE;
    }

    /// <summary>
    ///     Constructor.
    /// </summary>
    /// <param name="value">Sequence data</param>
    public Sequence(byte[]? value)
        : this()
    {
        if (value is { Length: > 0 })
        {
            _data = new byte[value.Length];
            Buffer.BlockCopy(value, 0, _data, 0, value.Length);
        }
    }

    /// <summary>
    ///     Get sequence data
    /// </summary>
    public byte[] Value => _data;

    /// <summary>
    ///     Clone sequence
    /// </summary>
    /// <returns>Cloned sequence cast as object</returns>
    public override object Clone()
    {
        return new Sequence(_data);
    }

    /// <summary>
    ///     Set sequence data
    /// </summary>
    /// <param name="value">Byte array containing BER encoded sequence data</param>
    public void Set(byte[] value)
    {
        if (value is { Length: > 0 })
        {
            _data = new byte[value.Length];
            Buffer.BlockCopy(value, 0, _data, 0, value.Length);
        }
        else
        {
            _data = [];
        }
    }

    /// <summary>
    ///     BER encode sequence
    /// </summary>
    /// <param name="buffer">Target buffer</param>
    public override void encode(MutableByte buffer)
    {
        var dataLen = 0;
        if (_data.Length > 0)
            dataLen = _data.Length;
        BuildHeader(buffer, Type, dataLen);
        switch (dataLen)
        {
            case > 0:
                buffer.Append(_data);
                break;
        }
    }

    /// <summary>
    ///     BER encode sequence
    /// </summary>
    /// <param name="buffer">Target buffer</param>
    public override int encode(Span<byte> buffer)
    {
        var slice = BuildHeader(buffer, Type, _data.Length);
        _data.CopyTo(buffer[slice..]);
        return _data.Length + slice;
    }

    /// <summary>
    ///     Decode sequence from the byte array. Returned offset value is advanced by the size of the sequence header.
    /// </summary>
    /// <param name="buffer">Source data buffer</param>
    /// <param name="offset">Offset within the buffer to start parsing from</param>
    /// <returns>Returns offset position after the sequence header</returns>
    public override int decode(byte[] buffer, int offset)
    {
        return decode(buffer.AsSpan(), offset);
    }

    /// <summary>
    ///     Decode sequence from the byte array. Returned offset value is advanced by the size of the sequence header.
    /// </summary>
    /// <param name="buffer">Source data buffer</param>
    /// <param name="offset">Offset within the buffer to start parsing from</param>
    /// <returns>Returns offset position after the sequence header</returns>
    public override int decode(Span<byte> buffer, int offset)
    {
        int asnType = ParseHeader(buffer, ref offset, out var dataLen);
        if (asnType != Type)
            throw new SnmpException("Invalid ASN.1 type.");
        if (offset + dataLen > buffer.Length)
            throw new OverflowException("Sequence longer then packet.");
        switch (dataLen)
        {
            case > 0:
                Array.Resize(ref _data, dataLen);
                buffer[offset..(offset + dataLen)].CopyTo(_data);
                break;
            default:
                _data = [];
                break;
        }

        return offset;
    }

    public override int ByteLength => encode(stackalloc byte[_data.Length + MaxHeaderSize]);
}