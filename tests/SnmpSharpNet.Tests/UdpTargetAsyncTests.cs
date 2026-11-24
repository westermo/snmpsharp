using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Sockets;

namespace SnmpSharpNet.Tests;

public class UdpTargetAsyncTests
{
    private const string WalkRoot = "1.3.6.1.2.1.1";
    private const string WalkLeaf = WalkRoot + ".1.0";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Test]
    [Arguments(SnmpVersion.Ver1)]
    [Arguments(SnmpVersion.Ver2)]
    [Arguments(SnmpVersion.Ver3)]
    public async Task RequestAsync_DeliversValidatedAgentResponse(SnmpVersion version)
    {
        await using var agent = new FakeAgent(version: version);
        using var target = new UdpTarget(IPAddress.Loopback, agent.Port, 1000, 0);
        var pdu = GetPdu();
        IAgentParameters parameters = version == SnmpVersion.Ver3
            ? new SecureAgentParameters()
            : new AgentParameters(version, new OctetString("public"));

        var packet = await target.RequestAsync(pdu, parameters).WaitAsync(TestTimeout);

        await Assert.That(packet.Version).IsEqualTo(version);
        await Assert.That(packet.Pdu.Type).IsEqualTo(PduType.Response);
        await Assert.That(packet.Pdu.RequestId).IsEqualTo(pdu.RequestId);
        await Assert.That(target.IsBusy).IsFalse();
    }

    [Test]
    public async Task RequestAsync_EmitsActivityAndRequestMetrics()
    {
        using var scope = StartDiagnosticsScope();
        var activities = new ConcurrentBag<Activity>();
        var measurements = new ConcurrentBag<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();
        var histogramNames = new ConcurrentBag<string>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SnmpDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == scope.TraceId)
                    activities.Add(activity);
            }
        };
        ActivitySource.AddActivityListener(activityListener);
        using var meterListener = CreateMeterListener(scope.TraceId, measurements, histogramNames);

        await using var agent = new FakeAgent();
        using var target = new UdpTarget(IPAddress.Loopback, agent.Port, 1000, 0);
        await target.RequestAsync(GetPdu(), V2Parameters()).WaitAsync(TestTimeout);

        var activity = activities.Single(item => item.OperationName == "snmp.request");
        await Assert.That(activity.Status).IsEqualTo(ActivityStatusCode.Ok);
        await Assert.That(activity.GetTagItem("snmp.version")).IsEqualTo(SnmpVersion.Ver2.ToString());
        var request = measurements.Single(item => item.Name == "snmpsharp.requests");
        await Assert.That(request.Value).IsEqualTo(1L);
        await Assert.That(request.Tags.Single(tag => tag.Key == "outcome").Value).IsEqualTo("success");
        await Assert.That(histogramNames.Contains("snmpsharp.request.duration")).IsTrue();
    }

    [Test]
    public async Task RequestAsync_RecordsTimeoutAndRetryMetrics()
    {
        using var scope = StartDiagnosticsScope();
        var measurements = new ConcurrentBag<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();
        using var meterListener = CreateMeterListener(scope.TraceId, measurements);
        await using var agent = new FakeAgent(ignoreFirst: int.MaxValue);
        using var target = new UdpTarget(IPAddress.Loopback, agent.Port, 50, 1);

        await Assert.That(async () => await target.RequestAsync(GetPdu(), V2Parameters()).WaitAsync(TestTimeout))
            .Throws<SnmpException>();

        await Assert.That(measurements.Single(item => item.Name == "snmpsharp.retries").Value).IsEqualTo(1L);
        await Assert.That(measurements.Single(item => item.Name == "snmpsharp.timeouts").Value).IsEqualTo(1L);
        var request = measurements.Single(item => item.Name == "snmpsharp.requests");
        await Assert.That(request.Tags.Single(tag => tag.Key == "outcome").Value).IsEqualTo("timeout");
    }

    [Test]
    public async Task RequestAsync_RecordsAuthenticationFailure()
    {
        using var scope = StartDiagnosticsScope();
        var measurements = new ConcurrentBag<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();
        using var meterListener = CreateMeterListener(scope.TraceId, measurements);
        await using var agent = new FakeAgent(mutateReply: packet =>
            ((SnmpV2Packet)packet).Community.Set("wrong"));
        using var target = new UdpTarget(IPAddress.Loopback, agent.Port, 1000, 0);

        await Assert.That(async () => await target.RequestAsync(GetPdu(), V2Parameters()).WaitAsync(TestTimeout))
            .Throws<SnmpAuthenticationException>();

        await Assert.That(measurements.Single(item => item.Name == "snmpsharp.authentication.failures").Value)
            .IsEqualTo(1L);
        var request = measurements.Single(item => item.Name == "snmpsharp.requests");
        await Assert.That(request.Tags.Single(tag => tag.Key == "outcome").Value)
            .IsEqualTo("authentication_failure");
    }

    [Test]
    public async Task RequestAsync_DeliversReplyAfterRetry()
    {
        await using var agent = new FakeAgent(ignoreFirst: 1);
        using var target = new UdpTarget(IPAddress.Loopback, agent.Port, 200, 2);

        var packet = await target.RequestAsync(GetPdu(), V2Parameters()).WaitAsync(TestTimeout);

        await Assert.That(packet.Pdu.Type).IsEqualTo(PduType.Response);
        await Assert.That(agent.RequestCount).IsEqualTo(2);
    }

    [Test]
    [Arguments(0)]
    [Arguments(2)]
    public async Task RequestAsync_TimesOutAfterExactlyTheConfiguredAttempts(int retries)
    {
        await using var agent = new FakeAgent(ignoreFirst: int.MaxValue);
        using var target = new UdpTarget(IPAddress.Loopback, agent.Port, 100, retries);

        var request = target.RequestAsync(GetPdu(), V2Parameters());
        await Assert.That(async () => await request.WaitAsync(TestTimeout)).Throws<SnmpException>();

        var exception = (SnmpException)request.Exception!.InnerException!;
        await Assert.That(exception.ErrorCode).IsEqualTo(SnmpException.RequestTimedOut);
        await Assert.That(agent.RequestCount).IsEqualTo(retries + 1);
        await Assert.That(target.IsBusy).IsFalse();
    }

    [Test]
    public async Task RequestAsync_TargetIsReusableAfterTimeout()
    {
        await using var agent = new FakeAgent(ignoreFirst: 1);
        using var target = new UdpTarget(IPAddress.Loopback, agent.Port, 200, 0);

        await Assert.That(async () => await target.RequestAsync(GetPdu(), V2Parameters()).WaitAsync(TestTimeout))
            .Throws<SnmpException>();
        var packet = await target.RequestAsync(GetPdu(), V2Parameters()).WaitAsync(TestTimeout);

        await Assert.That(packet.Pdu.Type).IsEqualTo(PduType.Response);
        await Assert.That(agent.RequestCount).IsEqualTo(2);
    }

    [Test]
    public async Task RequestAsync_CancellationDoesNotRetryAndTargetIsReusable()
    {
        await using var agent = new FakeAgent(ignoreFirst: 1);
        using var target = new UdpTarget(IPAddress.Loopback, agent.Port, 5000, 3);
        using var cancellation = new CancellationTokenSource();

        var request = target.RequestAsync(GetPdu(), V2Parameters(), cancellation.Token);
        await agent.FirstRequest.WaitAsync(TestTimeout);
        cancellation.Cancel();
        await Assert.That(async () => await request.WaitAsync(TestTimeout)).Throws<OperationCanceledException>();
        await Assert.That(request.IsCanceled).IsTrue();
        await Assert.That(target.IsBusy).IsFalse();
        await Assert.That(agent.RequestCount).IsEqualTo(1);

        var packet = await target.RequestAsync(GetPdu(), V2Parameters()).WaitAsync(TestTimeout);
        await Assert.That(packet.Pdu.Type).IsEqualTo(PduType.Response);
        await Assert.That(agent.RequestCount).IsEqualTo(2);
    }

    [Test]
    public async Task RequestAsync_PreCanceledRequestSendsNothing()
    {
        await using var agent = new FakeAgent();
        using var target = new UdpTarget(IPAddress.Loopback, agent.Port, 1000, 0);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.That(async () =>
            await target.RequestAsync(GetPdu(), V2Parameters(), cancellation.Token).WaitAsync(TestTimeout))
            .Throws<OperationCanceledException>();

        await Assert.That(target.IsBusy).IsFalse();
        await Assert.That(agent.RequestCount).IsEqualTo(0);
    }

    [Test]
    public async Task RequestAsync_RejectsConcurrentSyncAndAsyncRequests()
    {
        await using var agent = new FakeAgent(ignoreFirst: int.MaxValue);
        using var target = new UdpTarget(IPAddress.Loopback, agent.Port, 5000, 0);
        using var cancellation = new CancellationTokenSource();
        var request = target.RequestAsync(GetPdu(), V2Parameters(), cancellation.Token);
        try
        {
            await agent.FirstRequest.WaitAsync(TestTimeout);
            await Assert.That(target.IsBusy).IsTrue();
            await Assert.That(async () => await target.RequestAsync(GetPdu(), V2Parameters()))
                .Throws<InvalidOperationException>();
#pragma warning disable CS0618 // The obsolete wrapper must still respect the async transport's busy state.
            await Assert.That(() => target.Request(GetPdu(), V2Parameters())).Throws<InvalidOperationException>();
#pragma warning restore CS0618
        }
        finally
        {
            cancellation.Cancel();
            await Assert.That(async () => await request.WaitAsync(TestTimeout)).Throws<OperationCanceledException>();
        }
    }

    [Test]
    public async Task RequestAsync_CloseAbortsPendingRequestAndRejectsFutureRequests()
    {
        await using var agent = new FakeAgent(ignoreFirst: int.MaxValue);
        using var target = new UdpTarget(IPAddress.Loopback, agent.Port, 5000, 0);

        var request = target.RequestAsync(GetPdu(), V2Parameters());
        await agent.FirstRequest.WaitAsync(TestTimeout);
        target.Close();

        await Assert.That(async () => await request.WaitAsync(TestTimeout)).Throws<ObjectDisposedException>();
        await Assert.That(target.IsBusy).IsFalse();
        await Assert.That(async () => await target.RequestAsync(GetPdu(), V2Parameters()))
            .Throws<ObjectDisposedException>();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RequestAsync_EnforcesReplySourcePolicy(bool disableSourceCheck)
    {
        await using var agent = new FakeAgent(replyFromDifferentPort: true);
        using var target = new UdpTarget(IPAddress.Loopback, agent.Port, 200, 0);
        var parameters = V2Parameters();
        parameters.DisableReplySourceCheck = disableSourceCheck;

        var request = target.RequestAsync(GetPdu(), parameters);
        if (disableSourceCheck)
        {
            var packet = await request.WaitAsync(TestTimeout);
            await Assert.That(packet.Pdu.Type).IsEqualTo(PduType.Response);
        }
        else
        {
            await Assert.That(async () => await request.WaitAsync(TestTimeout)).Throws<SnmpException>();
            var exception = (SnmpException)request.Exception!.InnerException!;
            await Assert.That(exception.ErrorCode).IsEqualTo(SnmpException.RequestTimedOut);
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task RequestAsync_RejectsInvalidCommunityOrRequestId(bool wrongCommunity)
    {
        await using var agent = new FakeAgent(mutateReply: packet =>
        {
            if (wrongCommunity)
                ((SnmpV2Packet)packet).Community.Set("wrong");
            else
                packet.Pdu.RequestId++;
        });
        using var target = new UdpTarget(IPAddress.Loopback, agent.Port, 1000, 0);
        var request = target.RequestAsync(GetPdu(), V2Parameters());

        if (wrongCommunity)
            await Assert.That(async () => await request.WaitAsync(TestTimeout)).Throws<SnmpAuthenticationException>();
        else
        {
            await Assert.That(async () => await request.WaitAsync(TestTimeout)).Throws<SnmpException>();
            var exception = (SnmpException)request.Exception!.InnerException!;
            await Assert.That(exception.ErrorCode).IsEqualTo(SnmpException.InvalidRequestId);
        }
        await Assert.That(target.IsBusy).IsFalse();
    }

    [Test]
    public async Task Request_UsesSameTransportAndPacketValidation()
    {
        await using var agent = new FakeAgent();
        using var target = new UdpTarget(IPAddress.Loopback, agent.Port, 1000, 0);

#pragma warning disable CS0618 // Exercise the obsolete synchronous wrapper.
        var packet = target.Request(GetPdu(), V2Parameters());
#pragma warning restore CS0618

        await Assert.That(packet!.Pdu.Type).IsEqualTo(PduType.Response);
        await Assert.That(target.IsBusy).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DiscoveryAsync_UpdatesEngineParametersAndPerformsOptionalSecondExchange(bool twoExchanges)
    {
        await using var agent = new FakeAgent(version: SnmpVersion.Ver3, mutateReply: packet =>
        {
            var reply = (SnmpV3Packet)packet;
            reply.USM.EngineId.Set(new byte[] { 0x80, 0, 0, 1, 2 });
            reply.USM.EngineBoots = twoExchanges ? 0 : 1;
            reply.USM.EngineTime = twoExchanges ? 0 : 10;
            reply.Pdu.Type = PduType.Report;
            reply.Pdu.VbList.Clear();
            reply.Pdu.VbList.Add(new Vb(SnmpConstants.usmStatsUnknownEngineIDs, new Counter32(1)));
        });
        using var target = new UdpTarget(IPAddress.Loopback, agent.Port, 1000, 0);
        var parameters = new SecureAgentParameters();

        var discovered = await target.DiscoveryAsync(parameters).WaitAsync(TestTimeout);

        await Assert.That(discovered).IsTrue();
        await Assert.That(parameters.EngineId.Length).IsEqualTo(5);
        await Assert.That(agent.RequestCount).IsEqualTo(twoExchanges ? 2 : 1);
    }

    [Test]
    public async Task RawRequest_SendsOnlyTheSpecifiedBufferLengthAndReceivesLargeDatagram()
    {
        await using var agent = new FakeAgent(mutateReply: packet =>
            packet.Pdu.VbList.Add(new Vb(new Oid("1.3.6.1.2.1.1.2.0"), new OctetString(new byte[8192]))));
        using var transport = new UdpTransport(false);
        var outgoing = new SnmpV2Packet("public").Encode();
        var padded = new byte[outgoing.Length + 100];
        outgoing.CopyTo(padded, 0);

#pragma warning disable CS0618 // Exercise the obsolete synchronous wrapper.
        var response = transport.Request(IPAddress.Loopback, agent.Port, padded, outgoing.Length, 1000, 0);
#pragma warning restore CS0618

        await Assert.That(agent.LastRequestLength).IsEqualTo(outgoing.Length);
        var packet = new SnmpV2Packet(response);
        if (packet.Pdu.VbList[0].Value is not OctetString value)
            throw new InvalidOperationException("Expected an OctetString in the reply.");
        await Assert.That(value.Length).IsEqualTo(8192);
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task RawRequestAsync_NoTimeoutRemainsCancellable(int timeout)
    {
        await using var agent = new FakeAgent(ignoreFirst: int.MaxValue);
        using var transport = new UdpTransport(false);
        using var cancellation = new CancellationTokenSource();

        var request = transport.RequestAsync(IPAddress.Loopback, agent.Port, new byte[] { 1 }, timeout, 0,
            cancellation.Token);
        await agent.FirstRequest.WaitAsync(TestTimeout);
        cancellation.Cancel();

        await Assert.That(async () => await request.WaitAsync(TestTimeout)).Throws<OperationCanceledException>();
        await Assert.That(transport.IsBusy).IsFalse();
    }

    [Test]
    public async Task RequestAsync_AddressFamilyChangeIsRejectedWhileBusy()
    {
        await using var agent = new FakeAgent(ignoreFirst: int.MaxValue);
        using var target = new UdpTarget(IPAddress.Loopback, agent.Port, 1000, 0);
        using var cancellation = new CancellationTokenSource();
        var request = target.RequestAsync(GetPdu(), V2Parameters(), cancellation.Token);
        try
        {
            await agent.FirstRequest.WaitAsync(TestTimeout);
            await Assert.That(() => target.Address = IPAddress.IPv6Loopback).Throws<InvalidOperationException>();
            await Assert.That(target.Address).IsEqualTo(IPAddress.Loopback);
            await Assert.That(target.IsIPv6).IsFalse();
        }
        finally
        {
            cancellation.Cancel();
            await Assert.That(async () => await request.WaitAsync(TestTimeout)).Throws<OperationCanceledException>();
        }
    }

    [Test]
    public async Task SimpleSnmpGetAsync_UsesAsyncTransportAndReturnsBindings()
    {
        await using var agent = new FakeAgent(mutateReply: packet =>
        {
            packet.Pdu.VbList.Clear();
            packet.Pdu.VbList.Add(new Vb(new Oid("1.3.6.1.2.1.1.1.0"), new OctetString("test")));
        });
        var snmp = new SimpleSnmp("127.0.0.1", agent.Port, "public", 1000, 0);

        var result = await snmp.GetAsync(SnmpVersion.Ver2, ["1.3.6.1.2.1.1.1.0"])
            .WaitAsync(TestTimeout);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Count).IsEqualTo(1);
        await Assert.That(result[new Oid("1.3.6.1.2.1.1.1.0")].ToString()).IsEqualTo("test");
        await Assert.That(agent.RequestCount).IsEqualTo(1);
    }

    [Test]
    public async Task SimpleSnmpAsync_PropagatesConfigurationAndVersionErrors()
    {
        var snmp = new SimpleSnmp();

        await Assert.That(async () => await snmp.GetAsync(SnmpVersion.Ver2, GetPdu()))
            .Throws<SnmpException>();
        await Assert.That(async () => await snmp.GetBulkAsync(GetPdu(), SnmpVersion.Ver1))
            .Throws<SnmpInvalidVersionException>();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SimpleSnmpWalkAsync_ThrowsOnNonIncreasingOid(bool duplicateInSameReply)
    {
        var root = new Oid(WalkRoot);
        var leaf = new Oid(WalkLeaf);
        await using var agent = new FakeAgent(mutateReply: packet =>
        {
            var firstReply = packet.Pdu.VbList[0].Oid!.Equals(root);
            packet.Pdu.VbList.Clear();
            if (firstReply)
            {
                packet.Pdu.VbList.Add(leaf, new OctetString("first"));
                if (duplicateInSameReply)
                    packet.Pdu.VbList.Add(leaf, new OctetString("duplicate"));
            }
            else
            {
                var oid = duplicateInSameReply ? new Oid("1.3.6.1.2.1.2.1.0") : leaf;
                packet.Pdu.VbList.Add(oid, new OctetString("next"));
            }
        });
        var snmp = new SimpleSnmp("127.0.0.1", agent.Port, "public", 1000, 0);
        var results = new List<KeyValuePair<Oid, AsnType>>();

        async Task WalkAndCollectAsync()
        {
            await foreach (var binding in snmp.WalkAsync(SnmpVersion.Ver2, WalkRoot))
                results.Add(binding);
        }

        await Assert.That(WalkAndCollectAsync).Throws<SnmpException>();

        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0].Value.ToString()).IsEqualTo("first");
        await Assert.That(agent.RequestCount).IsEqualTo(duplicateInSameReply ? 1 : 2);
    }

    [Test]
    [Arguments(SnmpVersion.Ver1, false)]
    [Arguments(SnmpVersion.Ver1, true)]
    [Arguments(SnmpVersion.Ver2, true)]
    public async Task SimpleSnmpWalkAsync_CompletesNormallyAtEndOfMib(SnmpVersion version, bool returnBinding)
    {
        var root = new Oid(WalkRoot);
        var leaf = new Oid(WalkLeaf);
        await using var agent = new FakeAgent(version: version, mutateReply: packet =>
        {
            var cursor = packet.Pdu.VbList[0].Oid!;
            if (returnBinding && cursor.Equals(root))
            {
                packet.Pdu.VbList.Clear();
                packet.Pdu.VbList.Add(leaf, new OctetString("test"));
            }
            else if (version == SnmpVersion.Ver1)
            {
                packet.Pdu.ErrorStatus = SnmpConstants.ErrNoSuchName;
                packet.Pdu.ErrorIndex = 1;
            }
            else
            {
                packet.Pdu.VbList.Clear();
                packet.Pdu.VbList.Add(cursor, new EndOfMibView());
            }
        });
        var snmp = new SimpleSnmp("127.0.0.1", agent.Port, "public", 1000, 0);
        var oids = new List<Oid>();

        await foreach (var binding in snmp.WalkAsync(version, WalkRoot))
            oids.Add(binding.Key);

        await Assert.That(oids.Count).IsEqualTo(returnBinding ? 1 : 0);
        if (returnBinding)
            await Assert.That(oids[0]).IsEqualTo(leaf);
        await Assert.That(agent.RequestCount).IsEqualTo(returnBinding ? 2 : 1);
    }

    [Test]
    public async Task SimpleSnmpWalkAsync_PropagatesOtherV1AgentErrors()
    {
        await using var agent = new FakeAgent(version: SnmpVersion.Ver1, mutateReply: packet =>
        {
            packet.Pdu.ErrorStatus = SnmpConstants.ErrGenError;
            packet.Pdu.ErrorIndex = 1;
        });
        var snmp = new SimpleSnmp("127.0.0.1", agent.Port, "public", 1000, 0);
        await using var walker = snmp.WalkAsync(SnmpVersion.Ver1, WalkRoot).GetAsyncEnumerator();

        await Assert.That(async () => await walker.MoveNextAsync()).Throws<SnmpErrorStatusException>();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SimpleSnmpGetAsync_RediscoversAfterInterruptedDiscovery(bool cancelDiscovery)
    {
        var discoveryBindings = new ConcurrentQueue<int>();
        await using var agent = new FakeAgent(ignoreFirst: 1, version: SnmpVersion.Ver3, mutateReply: packet =>
        {
            var reply = (SnmpV3Packet)packet;
            if (reply.IsDiscoveryPacket)
            {
                discoveryBindings.Enqueue(reply.Pdu.VbCount);
                SetV3Report(reply, SnmpConstants.usmStatsUnknownEngineIDs);
                return;
            }

            reply.Pdu.VbList.Clear();
            reply.Pdu.VbList.Add(new Oid(WalkLeaf), new OctetString(reply.USM.SecurityName));
        });
        var snmp = new SimpleSnmp("127.0.0.1", agent.Port, "test-user", AuthenticationDigests.None, "",
            PrivacyProtocols.None, "", "") { Timeout = cancelDiscovery ? 5000 : 150, Retry = 0 };
        using var cancellation = new CancellationTokenSource();
        var firstRequest = snmp.GetAsync(SnmpVersion.Ver3, [WalkLeaf], cancellation.Token);
        await agent.FirstRequest.WaitAsync(TestTimeout);

        if (cancelDiscovery)
        {
            cancellation.Cancel();
            await Assert.That(async () => await firstRequest.WaitAsync(TestTimeout)).Throws<OperationCanceledException>();
        }
        else
        {
            await Assert.That(async () => await firstRequest.WaitAsync(TestTimeout)).Throws<SnmpException>();
            await Assert.That(((SnmpException)firstRequest.Exception!.InnerException!).ErrorCode)
                .IsEqualTo(SnmpException.RequestTimedOut);
        }

        snmp.Timeout = 1000;
        var result = await snmp.GetAsync(SnmpVersion.Ver3, [WalkLeaf]).WaitAsync(TestTimeout);

        await Assert.That(agent.RequestCount).IsEqualTo(3);
        await Assert.That(discoveryBindings.Single()).IsEqualTo(0);
        await Assert.That(result[new Oid(WalkLeaf)].ToString()).IsEqualTo("test-user");
    }

    [Test]
    public async Task SimpleSnmpGetAsync_RejectsDiscoveryWithoutAnEngineId()
    {
        await using var agent = new FakeAgent(version: SnmpVersion.Ver3);
        var snmp = new SimpleSnmp("127.0.0.1", agent.Port, "public", 1000, 0);

        await Assert.That(async () => await snmp.GetAsync(SnmpVersion.Ver3, [WalkLeaf]).WaitAsync(TestTimeout))
            .Throws<SnmpException>();
        await Assert.That(agent.RequestCount).IsEqualTo(2);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task SimpleSnmpAsync_HandlesV3Reports(bool timeWindowReport, bool recoverOnRetry)
    {
        var reportOid = timeWindowReport ? SnmpConstants.usmStatsNotInTimeWindows
            : SnmpConstants.usmStatsUnknownSecurityNames;
        var operationReplies = 0;
        await using var agent = new FakeAgent(version: SnmpVersion.Ver3, mutateReply: packet =>
        {
            var reply = (SnmpV3Packet)packet;
            if (reply.IsDiscoveryPacket)
                SetV3Report(reply, SnmpConstants.usmStatsUnknownEngineIDs);
            else if (!recoverOnRetry || ++operationReplies == 1)
                SetV3Report(reply, reportOid);
        });
        var snmp = new SimpleSnmp("127.0.0.1", agent.Port, "public", 1000, 0);

        var request = timeWindowReport
            ? snmp.SetAsync(SnmpVersion.Ver3, [new Vb(new Oid(WalkLeaf), new Integer32(1))])
            : snmp.GetAsync(SnmpVersion.Ver3, [WalkLeaf]);

        if (recoverOnRetry)
        {
            var bindings = await request.WaitAsync(TestTimeout);
            await Assert.That(bindings[new Oid(WalkLeaf)].ToString()).IsEqualTo("1");
        }
        else
        {
            await Assert.That(async () => await request.WaitAsync(TestTimeout)).Throws<SnmpException>();
            await Assert.That(request.Exception!.InnerException!.Message).Contains(timeWindowReport
                ? nameof(SnmpConstants.usmStatsNotInTimeWindows)
                : nameof(SnmpConstants.usmStatsUnknownSecurityNames));
        }

        await Assert.That(agent.RequestCount).IsEqualTo(timeWindowReport ? 3 : 2);
    }

    [Test]
    public async Task SimpleSnmpResolveAsync_ResolvesHostnameAsynchronously()
    {
        var snmp = new SimpleSnmp("localhost");

        await Assert.That(snmp.PeerIP).IsEqualTo(IPAddress.None);
        await Assert.That(await snmp.ResolveAsync().WaitAsync(TestTimeout)).IsTrue();
        await Assert.That(snmp.PeerIP.Equals(IPAddress.None)).IsFalse();
    }

    [Test]
    public async Task SimpleSnmpResolution_PreservesDnsErrorsForRequestsWithoutSuppression()
    {
        var snmp = new SimpleSnmp(new string('x', 512));

        await Assert.That(await snmp.ResolveAsync()).IsFalse();
        await Assert.That(async () => await snmp.GetAsync(SnmpVersion.Ver2, GetPdu()))
            .Throws<ArgumentOutOfRangeException>();

#pragma warning disable CS0618 // Exercise the legacy exception-suppression contract.
        await Assert.That(snmp.Get(SnmpVersion.Ver2, GetPdu())).IsNull();
        snmp.SuppressExceptions = false;
        await Assert.That(() => snmp.Get(SnmpVersion.Ver2, GetPdu())).Throws<ArgumentOutOfRangeException>();
#pragma warning restore CS0618
    }

    [Test]
    public async Task IpAddressSetAsync_ResolvesHostnamesToIpv4()
    {
        var address = new IpAddress();

        await address.SetAsync("localhost", CancellationToken.None);

        await Assert.That(address.Valid).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task TrapAgentSendV2TrapAsync_SendsToIpAddressFamily(bool ipv6)
    {
        if (ipv6 && !Socket.OSSupportsIPv6)
            return;

        var peer = ipv6 ? IPAddress.IPv6Loopback : IPAddress.Loopback;
        using var listener = new UdpClient(new IPEndPoint(peer, 0));
        using var cancellation = new CancellationTokenSource(TestTimeout);
        var endpoint = (IPEndPoint)listener.Client.LocalEndPoint!;
        var receive = listener.ReceiveAsync(cancellation.Token);
        await using var sender = new TrapAgent();
        await sender.SendV2TrapAsync(CreateTrapPacket(), peer, endpoint.Port, cancellation.Token)
            .WaitAsync(TestTimeout);
        var datagram = await receive.AsTask().WaitAsync(TestTimeout);

        await Assert.That(datagram.Buffer.Length).IsGreaterThan(0);
    }

    private static SnmpV2Packet CreateTrapPacket() =>
        new("public")
        {
            Pdu =
            {
                Type = PduType.V2Trap,
                TrapObjectID = new Oid("1.3.6.1.6.3.1.1.5.1"),
                TrapSysUpTime = { Value = 1 }
            }
        };

    private static Pdu GetPdu()
    {
        var pdu = Pdu.GetPdu();
        pdu.RequestId = 12345;
        pdu.VbList.Add(new Oid("1.3.6.1.2.1.1.1.0"));
        return pdu;
    }

    private static AgentParameters V2Parameters() => new(SnmpVersion.Ver2, new OctetString("public"));

    private static void SetV3Report(SnmpV3Packet packet, Oid oid)
    {
        packet.SetEngineId([0x80, 0, 0, 1, 2]);
        packet.SetEngineTime(1, 10);
        packet.MsgFlags.Reportable = false;
        packet.Pdu.Type = PduType.Report;
        packet.Pdu.VbList.Clear();
        packet.Pdu.VbList.Add(oid, new Counter32(1));
    }

    private static Activity StartDiagnosticsScope() => new Activity("test.snmp")
        .SetParentId(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None)
        .Start();

    private static MeterListener CreateMeterListener(ActivityTraceId traceId,
        ConcurrentBag<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> measurements,
        ConcurrentBag<string>? histogramNames = null)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == SnmpDiagnostics.MeterName)
                    meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (Activity.Current?.TraceId == traceId)
                measurements.Add((instrument.Name, value, tags.ToArray()));
        });
        if (histogramNames is not null)
            listener.SetMeasurementEventCallback<double>((instrument, _, _, _) =>
            {
                if (Activity.Current?.TraceId == traceId)
                    histogramNames.Add(instrument.Name);
            });
        listener.Start();
        return listener;
    }

    private sealed class FakeAgent : IAsyncDisposable
    {
        private readonly UdpClient _client = new(new IPEndPoint(IPAddress.Loopback, 0));
        private readonly UdpClient? _replyClient;
        private readonly CancellationTokenSource _cts = new();
        private readonly TaskCompletionSource _firstRequest = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly int _ignoreFirst;
        private readonly SnmpVersion _version;
        private readonly Action<SnmpPacket>? _mutateReply;
        private readonly Task _loop;
        private int _requestCount;

        public FakeAgent(int ignoreFirst = 0, SnmpVersion version = SnmpVersion.Ver2,
            bool replyFromDifferentPort = false, Action<SnmpPacket>? mutateReply = null)
        {
            _ignoreFirst = ignoreFirst;
            _version = version;
            _mutateReply = mutateReply;
            if (replyFromDifferentPort) _replyClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            _loop = RunAsync();
        }

        public int Port => ((IPEndPoint)_client.Client.LocalEndPoint!).Port;
        public int RequestCount => Volatile.Read(ref _requestCount);
        public int LastRequestLength { get; private set; }
        public Task FirstRequest => _firstRequest.Task;

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();
            try
            {
                await _loop;
            }
            finally
            {
                _replyClient?.Dispose();
                _client.Dispose();
                _cts.Dispose();
            }
        }

        private async Task RunAsync()
        {
            try
            {
                while (true)
                {
                    var received = await _client.ReceiveAsync(_cts.Token);
                    LastRequestLength = received.Buffer.Length;
                    var count = Interlocked.Increment(ref _requestCount);
                    _firstRequest.TrySetResult();
                    if (count <= _ignoreFirst) continue;

                    SnmpPacket packet = _version switch
                    {
                        SnmpVersion.Ver1 => new SnmpV1Packet(),
                        SnmpVersion.Ver2 => new SnmpV2Packet(),
                        _ => new SnmpV3Packet()
                    };
                    packet.Decode(received.Buffer);
                    packet.Pdu.Type = PduType.Response;
                    _mutateReply?.Invoke(packet);
                    await (_replyClient ?? _client).SendAsync(packet.Encode(), received.RemoteEndPoint, _cts.Token);
                }
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
            }
        }
    }
}
