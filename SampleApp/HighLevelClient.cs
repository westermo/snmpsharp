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
            MaxRepetitions = 30
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
}