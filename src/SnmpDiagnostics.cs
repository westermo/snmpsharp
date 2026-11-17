// This file is part of SNMP#NET.
//
// SNMP#NET is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// SNMP#NET is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public License
// along with SNMP#NET. If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SnmpSharpNet;

/// <summary>OpenTelemetry-compatible diagnostics for SNMP requests.</summary>
/// <remarks>
///     Listen to the <see cref="ActivitySourceName"/> activity source and the <see cref="MeterName"/> meter.
///     Measurements use only low-cardinality protocol, operation and outcome tags; peer addresses and credentials
///     are never recorded.
/// </remarks>
public static class SnmpDiagnostics
{
    /// <summary>Name of the SNMP request activity source.</summary>
    public const string ActivitySourceName = "Westermo.SnmpSharpNet";

    /// <summary>Name of the SNMP request meter.</summary>
    public const string MeterName = "Westermo.SnmpSharpNet";

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    internal static readonly Meter Meter = new(MeterName);
    internal static readonly Counter<long> Requests = Meter.CreateCounter<long>("snmpsharp.requests");
    internal static readonly Counter<long> Retries = Meter.CreateCounter<long>("snmpsharp.retries");
    internal static readonly Counter<long> Timeouts = Meter.CreateCounter<long>("snmpsharp.timeouts");
    internal static readonly Counter<long> AuthenticationFailures =
        Meter.CreateCounter<long>("snmpsharp.authentication.failures");
    internal static readonly Histogram<double> RequestDuration =
        Meter.CreateHistogram<double>("snmpsharp.request.duration", "ms");

    internal static TagList RequestTags(SnmpVersion version, PduType operation, string outcome)
    {
        var tags = new TagList
        {
            { "snmp.version", version.ToString() },
            { "snmp.operation", operation.ToString() },
            { "outcome", outcome }
        };
        return tags;
    }

    internal static TagList TransportTags(string outcome)
    {
        var tags = new TagList { { "outcome", outcome } };
        return tags;
    }
}
