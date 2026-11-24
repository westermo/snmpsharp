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
using System.Runtime.CompilerServices;
using System.Text;

namespace SnmpSharpNet;

/// <summary>SMI TimeTicks class</summary>
/// <remarks>
///     TimeTicks value is stored as an unsigned
///     32-bit integer representing device uptime in 1/100s of a second time periods.
/// </remarks>
[Serializable]
public class TimeTicks : UInteger32, ISpanParsable<TimeTicks>
{
    /// <summary>Constructor</summary>
    public TimeTicks()
    {
        _asnType = SnmpConstants.SMI_TIMETICKS;
    }

    /// <summary>Constructor</summary>
    /// <param name="second">Initialize class with value from this class.</param>
    public TimeTicks(TimeTicks second) : base(second)
    {
        _asnType = SnmpConstants.SMI_TIMETICKS;
    }

    /// <summary>Constructor</summary>
    /// <param name="uint32">The UInteger32 value to initialize the class with.</param>
    public TimeTicks(UInteger32 uint32) : base(uint32)
    {
        _asnType = SnmpConstants.SMI_TIMETICKS;
    }

    /// <summary>
    ///     Constructor.
    /// </summary>
    /// <param name="value">Initialize the TimeTicks class to this unsigned integer value.</param>
    [OverloadResolutionPriority(1)]
    public TimeTicks(uint value)
        : base(value)
    {
        _asnType = SnmpConstants.SMI_TIMETICKS;
    }

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="val">
    ///     String holding 32-bit unsigned integer value to initialize the class with
    /// </param>
    public TimeTicks(string val) : base(val)
    {
        _asnType = SnmpConstants.SMI_TIMETICKS;
    }

    /// <summary>
    ///     Get TimeTicks value as milliseconds value.
    /// </summary>
    public long Milliseconds
    {
        get
        {
            var ttval = Convert.ToInt64(_value);
            var res = ttval * 10L;
            return res;
        }
    }

    /// <summary>Duplicate object</summary>
    /// <returns>Object cloned copy cast to Object</returns>
    public override object Clone()
    {
        return new TimeTicks(this);
    }

    /// <summary>
    ///     Operator to allow explicit conversion of TimeTicks class to a TimeSpan class.
    /// </summary>
    /// <param name="value">TimeTicks class to convert to TimeSpan</param>
    /// <returns>TimeSpan value representing the value of TimeTicks class.</returns>
    public static explicit operator TimeSpan(TimeTicks value)
    {
        long ticks = value.Value;
        ticks *= 10;
        ticks *= 10000;
        var ts = new TimeSpan(ticks);
        return ts;
    }

    /// <summary>
    ///     Operator to allow explicit conversion of a TimeSpan to a TimeTicks class.
    /// </summary>
    /// <remarks>
    ///     TimeTicks has a resolution of 1/100th of a second; any remainder is truncated.
    /// </remarks>
    /// <param name="value">TimeSpan to convert to TimeTicks</param>
    /// <returns>TimeTicks class representing the TimeSpan value.</returns>
    /// <exception cref="OverflowException">TimeSpan is negative or too large to be represented as TimeTicks.</exception>
    public static explicit operator TimeTicks(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
            throw new OverflowException("A negative TimeSpan cannot be represented as TimeTicks.");

        return new TimeTicks(checked((uint)(value.Ticks / (TimeSpan.TicksPerMillisecond * 10))));
    }

    /// <summary>
    ///     Implicit casting of UInt32 value (hundredths of a second) as TimeTicks class
    /// </summary>
    /// <param name="value">UInt32 value</param>
    /// <returns>New TimeTicks class initialized with the value.</returns>
    public static implicit operator TimeTicks(uint value)
    {
        return new TimeTicks(value);
    }

    /// <summary>
    ///     Returns the string representation of the object.
    /// </summary>
    /// <returns>String representation of the class value.</returns>
    public override string ToString()
    {
        var buf = new StringBuilder();
        long time = Value;
        long tmp;
        if ((tmp = time / (24 * 3600 * 100)) > 0)
        {
            buf.Append(tmp).Append("d ");
            time %= 24 * 3600 * 100;
        }
        else
        {
            buf.Append("0d ");
        }

        if ((tmp = time / (3600 * 100)) > 0)
        {
            buf.Append(tmp).Append("h ");
            time %= 3600 * 100;
        }
        else
        {
            buf.Append("0h ");
        }

        if ((tmp = time / 6000) > 0)
        {
            buf.Append(tmp).Append("m ");
            time %= 6000;
        }
        else
        {
            buf.Append("0m ");
        }

        if ((tmp = time / 100) > 0)
        {
            buf.Append(tmp).Append("s ");
            time %= 100;
        }
        else
        {
            buf.Append("0s ");
        }

        buf.Append(time * 10).Append("ms");

        return buf.ToString();
    }

    public override string ToString(string? format, IFormatProvider? formatProvider)
    {
        ValidateFormat(format);
        return ToString();
    }

    public override bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    {
        ValidateFormat(format);
        var time = Value;
        var written = 0;
        var days = time / (24u * 3600 * 100);
        time %= 24u * 3600 * 100;
        var hours = time / (3600 * 100);
        time %= 3600 * 100;
        var minutes = time / 6000;
        time %= 6000;
        var seconds = time / 100;
        var milliseconds = time % 100 * 10;

        if (!AppendValue(days, "d ", destination, ref written, provider)
            || !AppendValue(hours, "h ", destination, ref written, provider)
            || !AppendValue(minutes, "m ", destination, ref written, provider)
            || !AppendValue(seconds, "s ", destination, ref written, provider)
            || !AppendValue(milliseconds, "ms", destination, ref written, provider))
        {
            charsWritten = 0;
            return false;
        }

        charsWritten = written;
        return true;
    }

    public override bool TryFormat(
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    {
        ValidateFormat(format);
        var time = Value;
        var written = 0;
        var days = time / (24u * 3600 * 100);
        time %= 24u * 3600 * 100;
        var hours = time / (3600 * 100);
        time %= 3600 * 100;
        var minutes = time / 6000;
        time %= 6000;
        var seconds = time / 100;
        var milliseconds = time % 100 * 10;

        if (!AppendValue(days, "d "u8, utf8Destination, ref written, provider)
            || !AppendValue(hours, "h "u8, utf8Destination, ref written, provider)
            || !AppendValue(minutes, "m "u8, utf8Destination, ref written, provider)
            || !AppendValue(seconds, "s "u8, utf8Destination, ref written, provider)
            || !AppendValue(milliseconds, "ms"u8, utf8Destination, ref written, provider))
        {
            bytesWritten = 0;
            return false;
        }

        bytesWritten = written;
        return true;
    }

    public new static TimeTicks Parse(string s, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan(), provider);
    }

    public new static TimeTicks Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (!TryParseValue(s, provider, out var value))
            throw new FormatException("Invalid TimeTicks value.");
        return new TimeTicks(value);
    }

    public static bool TryParse(string? s, IFormatProvider? provider, out TimeTicks result)
    {
        if (s is not null && TryParseValue(s.AsSpan(), provider, out var value))
        {
            result = new TimeTicks(value);
            return true;
        }

        result = null!;
        return false;
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out TimeTicks result)
    {
        if (TryParseValue(s, provider, out var value))
        {
            result = new TimeTicks(value);
            return true;
        }

        result = null!;
        return false;
    }

    private static bool TryParseValue(ReadOnlySpan<char> value, IFormatProvider? provider, out uint ticks)
    {
        if (uint.TryParse(value, provider, out ticks)) return true;

        var remaining = value;
        if (!TryReadComponent(ref remaining, "d ", provider, out var days)
            || !TryReadComponent(ref remaining, "h ", provider, out var hours)
            || !TryReadComponent(ref remaining, "m ", provider, out var minutes)
            || !TryReadComponent(ref remaining, "s ", provider, out var seconds)
            || !remaining.EndsWith("ms", StringComparison.Ordinal)
            || !uint.TryParse(remaining[..^2], provider, out var milliseconds)
            || hours >= 24
            || minutes >= 60
            || seconds >= 60
            || milliseconds >= 1000
            || milliseconds % 10 != 0)
        {
            ticks = 0;
            return false;
        }

        var totalTicks = (ulong)days * (24 * 3600 * 100)
                         + (ulong)hours * (3600 * 100)
                         + (ulong)minutes * 6000
                         + (ulong)seconds * 100
                         + milliseconds / 10;
        if (totalTicks > uint.MaxValue)
        {
            ticks = 0;
            return false;
        }

        ticks = (uint)totalTicks;
        return true;
    }

    private static bool TryReadComponent(
        ref ReadOnlySpan<char> value,
        ReadOnlySpan<char> suffix,
        IFormatProvider? provider,
        out uint component)
    {
        var suffixIndex = value.IndexOf(suffix, StringComparison.Ordinal);
        if (suffixIndex < 0 || !uint.TryParse(value[..suffixIndex], provider, out component))
        {
            component = 0;
            return false;
        }

        value = value[(suffixIndex + suffix.Length)..];
        return true;
    }

    private static bool AppendValue(
        uint value,
        ReadOnlySpan<char> suffix,
        Span<char> destination,
        ref int written,
        IFormatProvider? provider)
    {
        if (!value.TryFormat(destination[written..], out var numberLength, default, provider)) return false;
        written += numberLength;
        if (suffix.Length > destination.Length - written) return false;
        suffix.CopyTo(destination[written..]);
        written += suffix.Length;
        return true;
    }

    private static bool AppendValue(
        uint value,
        ReadOnlySpan<byte> suffix,
        Span<byte> destination,
        ref int written,
        IFormatProvider? provider)
    {
        if (!value.TryFormat(destination[written..], out var numberLength, default, provider)) return false;
        written += numberLength;
        if (suffix.Length > destination.Length - written) return false;
        suffix.CopyTo(destination[written..]);
        written += suffix.Length;
        return true;
    }

    private static void ValidateFormat(string? format)
    {
        if (format is not (null or "" or "G"))
            throw new FormatException($"The format '{format}' is not supported.");
    }

    private static void ValidateFormat(ReadOnlySpan<char> format)
    {
        if (!format.IsEmpty && !format.SequenceEqual("G"))
            throw new FormatException($"The format '{format.ToString()}' is not supported.");
    }

    /// <summary>
    ///     Return hash code for the class.
    /// </summary>
    /// <returns>Hash code is the hash code of the class value.</returns>
    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
}