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
public class TimeTicks : UInteger32, ICloneable
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

    /// <summary>
    ///     Return hash code for the class.
    /// </summary>
    /// <returns>Hash code is the hash code of the class value.</returns>
    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
}