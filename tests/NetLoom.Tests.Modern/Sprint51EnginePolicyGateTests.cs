using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NetLoom.Domain.Access;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Discovery;
using NetLoom.Application.Monitoring;
using NetLoom.Application.PollingPolicies;
using NetLoom.Application.Snmp;
using NetLoom.Engine;

namespace NetLoom.Tests.Modern
{
    [TestClass]
    public sealed class Sprint51EnginePolicyGateTests
    {
        private static readonly Guid A = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid B = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid C = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly DateTime Now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void ApplyAndComposePreventAllProbesForDisabledDevice()
        {
            var gate = Gate();
            var targets = gate.Apply(new[] { Target(A), Target(B), Target(C) });
            CollectionAssert.AreEqual(new[] { B }, targets.DisabledDeviceIds.ToArray());
            CollectionAssert.AreEqual(new[] { A, C }, targets.Targets.Select(target => target.DeviceId).ToArray());
            var probe = new CountingProbe();
            var snmp = 0;
            var poll = EnginePollingPolicyGate.ComposePoll(request =>
            {
                snmp++;
                return Result(request);
            }, new MonitoringAvailabilityChecker(probe), gate);
            Assert.ThrowsExactly<InvalidOperationException>(() => poll(Request(B)));
            Assert.AreEqual(0, snmp);
            Assert.AreEqual(0, probe.IcmpCalls);
            Assert.AreEqual(0, probe.TcpCalls);

            poll(Request(A));
            CollectionAssert.AreEqual(new[] { 22, 80, 443 }, probe.LastPorts.ToArray());
            poll(Request(C));
            CollectionAssert.AreEqual(new[] { 502, 8080 }, probe.LastPorts.ToArray());
            Assert.AreEqual(2, snmp);
            var beforeEmpty = probe.TcpCalls;
            new MonitoringAvailabilityChecker(probe).Check(IPAddress.Parse("192.0.2.1"),
                1000, CancellationToken.None, new int[0]);
            Assert.AreEqual(beforeEmpty, probe.TcpCalls);
        }

        [TestMethod]
        public void RunnerNeverPollsOrMarksDisabledDevice()
        {
            var gate = Gate();
            var targets = gate.Apply(new[] { Target(A), Target(B), Target(C) });
            var probe = new CountingProbe();
            var polled = new List<Guid>();
            using (var cancellation = new CancellationTokenSource())
            using (var output = new StringWriter(CultureInfo.InvariantCulture))
            {
                var poll = EnginePollingPolicyGate.ComposePoll(request =>
                {
                    lock (polled)
                    {
                        polled.Add(request.DeviceId.Value);
                        if (polled.Count >= 4) cancellation.Cancel();
                    }
                    return Result(request);
                }, new MonitoringAvailabilityChecker(probe), gate);
                var runner = new EngineMultiTargetMonitoringRunner(poll,
                    (delay, token) => Task.CompletedTask);
                runner.Run(targets.Targets, new MonitoringConcurrencyPolicy(2), cancellation.Token, output);
                Assert.IsFalse(polled.Contains(B));
                Assert.IsFalse(output.ToString().Contains("NETLOOM_TARGET_POLL state=started deviceId=" + B.ToString("D")));
                Assert.AreEqual(polled.Count, probe.IcmpCalls);
                Assert.AreEqual(polled.Count, probe.TcpCalls);
            }
        }

        private static EnginePollingPolicyGate Gate()
        {
            var disabled = new PollingPolicy(Guid.NewGuid(), "Disabled", false, false,
                PollingSchedule.General, PollingSchedule.General, new[] { 1 });
            var custom = new PollingPolicy(Guid.NewGuid(), "Custom", false, true,
                PollingSchedule.General, PollingSchedule.General, new[] { 8080, 502, 502 });
            var resolver = new PollingPolicyResolver(new[] { PollingPolicy.CreateDefault(), disabled, custom },
                new[]
                {
                    new PollingPolicyAssignment(PollingPolicySubjectKind.Device, B, disabled.Id),
                    new PollingPolicyAssignment(PollingPolicySubjectKind.Device, C, custom.Id)
                }, new Dictionary<Guid, Guid?>(), new Dictionary<Guid, Guid?>());
            return new EnginePollingPolicyGate(resolver, TimeSpan.FromSeconds(60));
        }

        private static MonitoringScheduleTarget Target(Guid id) =>
            new MonitoringScheduleTarget(Request(id), TimeSpan.FromSeconds(60), TimeSpan.Zero);

        private static MonitoringPollRequest Request(Guid id) => new MonitoringPollRequest(
            IPAddress.Parse("192.0.2.1"), 161, SnmpVersion.V2C,
            new SnmpCommunityCredentials(new byte[] { 4, 5, 6 }), 1000, 0, 10,
            new[] { MonitoringPollKind.Health }, id);

        private static MonitoringPollResult Result(MonitoringPollRequest request) => new MonitoringPollResult(
            request.Address, Now, Now, new[] { new MonitoringPollStepResult(MonitoringPollKind.Health, true, null, null) });

        private sealed class CountingProbe : INetworkDiscoveryProbe
        {
            public int IcmpCalls;
            public int TcpCalls;
            public IReadOnlyList<int> LastPorts;

            public bool IsIcmpReachable(IPAddress address, int timeoutMilliseconds, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref IcmpCalls);
                return true;
            }

            public IReadOnlyList<int> FindOpenTcpPorts(IPAddress address, IReadOnlyList<int> ports,
                int timeoutMilliseconds, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref TcpCalls);
                LastPorts = ports.ToArray();
                return new int[0];
            }
        }
    }
}
