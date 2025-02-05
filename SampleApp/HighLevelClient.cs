using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Reflection;
using Microsoft.Extensions.Logging;
using SnmpSharpNet;

namespace SampleApp;

public class HighLevelSnmpClient(UdpTarget target, IAgentParameters parameters, ILogger? logger)
{
    private const int SnmpTimeout = 4000;

    public AsnType QuerySingleValueAsync(Oid oid)
    {
        var pdu = new Pdu(PduType.Get)
        {
            RequestId = Random.Shared.Next()
        };

        pdu.VbList.Add(oid);

        SnmpPacket? result;
        try
        {
            result = target.Request(pdu, parameters);
            if (result == null)
            {
                logger?.LogWarning("Unintelligible SNMP packet returned from {Address}", target.Address);
                throw new IOException($"Unrecognized SNMP packet received from {target.Address}");
            }
        }
        catch (Exception e)
        {
            Log(pdu, e);
            logger?.LogDebug("SNMP Get request failed for {Oid} on {Address}: {Exception}", oid, target.Address, e);
            throw new TimeoutException();
        }

        // ErrorStatus other than 0 is an error returned by 
        // the Agent - see SnmpConstants for error definitions
        if (result.Pdu.ErrorStatus != 0)
        {
            // agent reported an error with the request
            logger?.LogWarning("Error in SNMP Get reply. Error {Status} index {Index}", result.Pdu.ErrorStatus,
                result.Pdu.ErrorIndex);
            throw new IOException($"SNMP error: {result.Pdu.ErrorStatus} ({result.Pdu.ErrorIndex})");
        }

        Log(pdu, result);

        return result.Pdu.VbList.Single().Value!;
    }

    public Vb[] QueryMultipleValues(params IEnumerable<Oid> oids)
    {
        var pdu = new Pdu(PduType.Get)
        {
            RequestId = Random.Shared.Next()
        };
        foreach (var oid in oids)
        {
            pdu.VbList.Add(oid);
        }

        SnmpPacket? result;
        var start = DateTime.UtcNow;
        try
        {
            result = target.Request(pdu, parameters);
            if (result == null)
            {
                logger?.LogWarning("Unintelligible SNMP packet returned from {Address}", target.Address);
                throw new IOException($"Unrecognized SNMP packet received from {target.Address}");
            }
        }
        catch (OperationCanceledException ex)
        {
            Log(pdu, ex);
            logger?.LogWarning(
                "SnmpClient.QueryMultipleValuesAsync was cancelled. CancellationToken.IsCancellationRequested = {CancellationState}",
                false);
            var end = DateTime.UtcNow - start;
            if (end.TotalMilliseconds >= SnmpTimeout)
            {
                throw new TimeoutException();
            }

            throw;
        }
        catch (Exception e)
        {
            Log(pdu, e);
            logger?.LogDebug("SNMP Get multiple requests failed for  {{ {PDU} }} on {Address}: {ErrorMessage}", pdu,
                target.Address,
                e);
            throw new TimeoutException();
        }

        // ErrorStatus other than 0 is an error returned by 
        // the Agent - see SnmpConstants for error definitions
        if (result.Pdu.ErrorStatus != 0)
        {
            // agent reported an error with the request
            // However, in some case we get answers, but one of the values in the returned array is wrong.
            // Any errors in the returned values are handled by the caller of this function.

            logger?.LogWarning("Error in SNMP Get reply. Error {ErrorStatus} index {ErrorIndex}",
                result.Pdu.ErrorStatus, result.Pdu.ErrorIndex);
        }

        Log(pdu, result);

        return result.Pdu.VbList.ToArray();
    }

    private void Log(Pdu pdu, Exception exception)
    {
        logger?.LogDebug("SNMP Error for {address}: {ErrorMessage}: {PDU}", target.Address, exception.Message, pdu);
    }

    private void Log(Pdu pdu, SnmpPacket snmpPacket)
    {
        logger?.LogDebug("SNMP Response for {address}: {Status}:\nRequest: {PDU}\nResponse:{Packet}", target.Address,
            snmpPacket.Pdu.ErrorStatus == 0 ? "Good" : "Bad", pdu, snmpPacket);
    }

    public IDictionary<Oid, AsnType?> WalkTree(Oid oid)
    {
        var treeData = new Dictionary<Oid, AsnType?>();
        var rootOid = oid;
        var lastOid = (Oid)rootOid.Clone();

        var pdu = new Pdu(PduType.GetBulk)
        {
            NonRepeaters = 0,
#if false // Enable to test WeOS 5.x
                MaxRepetitions = 10
#else
            MaxRepetitions = 30
#endif
        };

        // Loop through results
        while (lastOid is not null)
        {
            pdu.RequestId = Random.Shared.Next();
            pdu.VbList.Clear();
            pdu.VbList.Add(lastOid);

            // Make SNMP request
            SnmpPacket? result;
            try
            {
                result = target.Request(pdu, parameters);
            }
            catch (Exception e)
            {
                logger?.LogDebug("SNMP request failed for {Root} on {Address}: {ErrorMessage}", rootOid, target.Address,
                    e.Message);
                throw;
            }

            // If result is null then agent didn't reply or we couldn't parse the reply.
            if (result == null)
            {
                logger?.LogWarning("No response received from SNMP agent.");
                break;
            }

            // ErrorStatus other then 0 is an error returned by 
            // the Agent - see SnmpConstants for error definitions
            if (result.Pdu.ErrorStatus != 0)
            {
                // agent reported an error with the request
                logger?.LogWarning("Error in SNMP reply. Error {ErrorStatus} index {ErrorIndex}",
                    result.Pdu.ErrorStatus, result.Pdu.ErrorIndex);
                break;
            }

            // Walk through returned variable bindings
            foreach (var vb in result.Pdu.VbList)
            {
                // Check that retrieved Oid is "child" of the root OID
                if (rootOid.IsRootOf(vb.Oid!))
                {
                    treeData[vb.Oid!] = vb.Value;

                    // Done?
                    lastOid = vb.Value!.Type == SnmpConstants.SMI_ENDOFMIBVIEW
                        ? null
                        : vb.Oid;
                }
                else
                {
                    lastOid = null;
                }
            }
        }

        return treeData;
    }
    //
    // public IEnumerable<TTableRow> QueryTable<TTableRow>(IPAddress deviceIp)
    //     where TTableRow : ITableRow
    // {
    //     var entryOid = TTableRow.RootOid;
    //     var indices = TTableRow.Indices.ToArray();
    //     if (indices.Length == 0)
    //         throw new ArgumentException(
    //             $"The table row type {typeof(TTableRow).FullName} has no property marked with [SnmpIndexField] attribute.");
    //
    //     var fields = TTableRow.Fields;
    //     var table = new Dictionary<Index, TTableRow>();
    //
    //     foreach (var value in WalkTree(deviceIp, entryOid))
    //     {
    //         if (!entryOid.IsRootOf(value.Key))
    //             throw new ArgumentException(
    //                 $"values contains an OID {value.Key} that is NOT a field OID of SNMP entry {entryOid}");
    //
    //         var index = new Index(value.Key, indices.Length);
    //         if (!table.TryGetValue(index, out var currentRow))
    //         {
    //             table.Add(index, currentRow = CreateTableRow<TTableRow>());
    //             UpdateRowObjectWithIndices(currentRow, value.Key, indices);
    //         }
    //
    //         UpdateRowObjectWithFieldValue(currentRow, value.Key, value.Value, indices, fields);
    //     }
    //
    //     Log(deviceIp, fields, table);
    //
    //     return table.Values;
    // }

    private void Log<TTableRow>(IPAddress address, IDictionary<Oid, PropertyInfo> fields,
        Dictionary<Index, TTableRow> table)
    {
        logger?.LogDebug("SNMP Table response for {address}: {Table}", address, typeof(TTableRow).Name);
        logger?.LogDebug("------------------ Rows Start ------------------");

        var ordinal = 0;
        foreach (var index in table.Keys.OrderBy(k => k))
        {
            logger?.LogDebug("Ordinal: {ordinal}", ordinal);
            logger?.LogDebug("Index: {index}", index);

            foreach (var field in fields.Values.OrderBy(f => f.Name))
            {
                logger?.LogDebug("{Name}: {Value}", field.Name, field.GetValue(table[index]));
            }

            ++ordinal;
        }

        logger?.LogDebug("------------------  Rows End  ------------------");
    }

    private static void UpdateRowObjectWithIndices<TTableRow>(TTableRow row, Oid oid,
        IDictionary<uint, PropertyInfo> indices)
    {
        var oidLength = oid.Length;
        var indexLength = indices.Count;

        foreach (var index in indices)
        {
            // See comment for SnmpIndexFieldAttribute for a explanation
            var value = oid[oidLength - indexLength + (int)index.Key];
            index.Value.SetValue(row, value, null);
        }
    }

    private void UpdateRowObjectWithFieldValue<TTableRow>(TTableRow row, Oid valueOid, AsnType? value,
        IDictionary<uint, PropertyInfo> indices,
        IDictionary<Oid, PropertyInfo> fields)
    {
        var indexLength = indices.Count;

        // Remove the last components of the value oid, as it contains the index. We've
        // mapped the row properties against the field oid as defined by the MIB.
        var fieldOid = new Oid(valueOid.Take(valueOid.Length - indexLength).ToArray());

        if (fields.TryGetValue(fieldOid, out var property))
        {
            // If the value given to us is null, and the target property is a value type,
            // then log the error and move on.
            if (value == null && property.PropertyType.IsValueType)
            {
                logger?.LogError(
                    "Failed to update {Table}.{PropertyName} with value null, because property is a value type ({PropertyType}).",
                    typeof(TTableRow).Name, property.Name, property.PropertyType.Name);
                return;
            }

            // If the target property type is not the same as the type of the value being assigned,
            // and there is no way to coerce the value to fit the property's type, then we log the
            // error and move on.
            AsnType? coercedValue = null;

            // ReSharper disable once UseMethodIsInstanceOfType
            // ReSharper disable once PossibleMistakenCallToGetType.2
            if (value != null && !property.PropertyType.IsAssignableFrom(value.GetType()) &&
                !TryCoerce(value, property.PropertyType, out coercedValue))
            {
                logger?.LogError(
                    "Failed to update {Table}.{PropertyName} with value {Value} of type {Type}, because property has incompatible type ({PropertyType}).",
                    typeof(TTableRow).Name, property.Name, value, value.GetType().Name, property.PropertyType.Name);
                return;
            }

            if (coercedValue != null)
            {
                logger?.LogWarning(
                    "Value {Value} of type {Type} destined for {Table}.{PropertyName} of type {PropertyType} had to be coerced.",
                    value, value!.GetType().Name, typeof(TTableRow).Name, property.Name, property.PropertyType.Name);
            }

            try
            {
                property.SetValue(row, coercedValue ?? value, null);
            }
            catch (Exception ex)
            {
                logger?.LogError(
                    "Failed to update {Table}.{PropertyName} with {CoercedValue} ({CoercedType}), but failed: {Exception}",
                    typeof(TTableRow).Name, property.Name, coercedValue ?? value,
                    (coercedValue ?? value)?.GetType().Name ?? "<null>", ex);
                throw;
            }
        }
    }

    private Type? GetPlainType(Type targetType)
    {
        if (targetType == typeof(Integer32))
            return typeof(int);
        if (targetType == typeof(Counter32))
            return typeof(uint);
        if (targetType == typeof(Counter64))
            return typeof(ulong);
        if (targetType == typeof(UInteger32))
            return typeof(uint);
        if (targetType == typeof(OctetString))
            return typeof(string);
        return null;
    }

    // Must ONLY be called if the type of value is different than targetType!
    private bool TryCoerce(AsnType value, Type targetType, [NotNullWhen(true)] out AsnType? coercedValue)
    {
        coercedValue = null;

        // This the "plain" type of the target type. For instance if targetType == typeof(Integer32), then the plain type is typeof(int)
        var targetPlainType = GetPlainType(targetType);

        if (targetPlainType == null)
            return false;

        switch (value)
        {
            case Integer32 i32Value:
                coercedValue = (AsnType)Activator.CreateInstance(targetType,
                    Convert.ChangeType(i32Value.Value, targetPlainType, CultureInfo.InvariantCulture))!;
                return true;
            case Counter32 c32Value:
                coercedValue = (AsnType)Activator.CreateInstance(targetType,
                    Convert.ChangeType(c32Value.Value, targetPlainType, CultureInfo.InvariantCulture))!;
                return true;
            case Counter64 c64Value:
                coercedValue = (AsnType)Activator.CreateInstance(targetType,
                    Convert.ChangeType(c64Value.Value, targetPlainType, CultureInfo.InvariantCulture))!;
                return true;
            case UInteger32 ui32Value:
                coercedValue = (AsnType)Activator.CreateInstance(targetType,
                    Convert.ChangeType(ui32Value.Value, targetPlainType, CultureInfo.InvariantCulture))!;
                return true;
            case OctetString stringValue:
                coercedValue = (AsnType)Activator.CreateInstance(targetType,
                    Convert.ChangeType(stringValue.ToString(), targetPlainType, CultureInfo.InvariantCulture))!;
                return true;
            default:
                return false;
        }
    }

    private static TTableRow CreateTableRow<TTableRow>() => (TTableRow)Activator.CreateInstance(typeof(TTableRow))!;
}