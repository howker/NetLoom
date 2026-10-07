using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Discovery;
using NetLoom.Application.Monitoring;
using NetLoom.Application.MonitoringControl;

namespace NetLoom.Tests.Unit
{
    // Sprint 47: ICMP и TCP при опросе — тем же зондом, что у обнаружения; сбой зонда — «не проверено».
    [TestClass]
    public sealed class Sprint47AvailabilityCheckerTests
    {
        private static readonly IPAddress Address =
            IPAddress.Parse("198.51.100.20");

        [TestMethod]
        public void CheckerReportsIcmpAndOpenPortsOfTheDefaultManagementSet()
        {
            var probe =
                new FakeProbe(
                    true,
                    new[] { 443, 22 });

            var availability =
                new MonitoringAvailabilityChecker(probe)
                    .Check(
                        Address,
                        750,
                        CancellationToken.None);

            Assert.AreEqual(true, availability.IcmpReachable);
            CollectionAssert.AreEqual(new[] { 22, 80, 443 }, availability.CheckedTcpPorts.ToArray());
            CollectionAssert.AreEqual(new[] { 22, 443 }, availability.OpenTcpPorts.ToArray());
            Assert.AreEqual(750, probe.Timeout, "The probe uses the poll timeout.");
        }

        [TestMethod]
        public void ProbeFailureIsNotCheckedRatherThanUnavailableButCancellationPropagates()
        {
            var availability =
                new MonitoringAvailabilityChecker(
                        new FakeProbe(
                            new InvalidOperationException("raw socket denied")))
                    .Check(
                        Address,
                        750,
                        CancellationToken.None);

            Assert.IsNull(availability.IcmpReachable);
            Assert.AreEqual(0, availability.CheckedTcpPorts.Count);

            Assert.Throws<OperationCanceledException>(
                () => new MonitoringAvailabilityChecker(
                        new FakeProbe(
                            new OperationCanceledException()))
                    .Check(
                        Address,
                        750,
                        CancellationToken.None));
        }

        [TestMethod]
        public void TrackerKeepsTheLastKnownAvailabilityAcrossPollsAndSessions()
        {
            var target =
                new MonitoringTarget(
                    Guid.NewGuid(),
                    Address);

            var tracker =
                new MonitoringCycleTracker(
                    new[] { target });

            var t0 =
                new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);

            tracker.TargetCompleted(
                target.DeviceId,
                t0,
                false,
                new MonitoringAvailability(true, new[] { 22 }, new int[0]));

            tracker.TargetCompleted(
                target.DeviceId,
                t0.AddMinutes(1),
                false);

            Assert.AreEqual(
                true,
                tracker.Outcomes.Single().Availability.IcmpReachable,
                "A poll without an availability check keeps the last known one.");

            var next =
                new MonitoringCycleTracker(
                    new[] { target },
                    tracker.Outcomes);

            Assert.AreEqual(true, next.Outcomes.Single().Availability.IcmpReachable);
        }

        private sealed class FakeProbe :
            INetworkDiscoveryProbe
        {
            private readonly bool _icmp;
            private readonly int[] _open;
            private readonly Exception _failure;

            public FakeProbe(
                bool icmp,
                int[] open)
            {
                _icmp = icmp;
                _open = open;
            }

            public FakeProbe(
                Exception failure)
            {
                _failure = failure;
            }

            public int Timeout { get; private set; }

            public bool IsIcmpReachable(
                IPAddress address,
                int timeoutMilliseconds,
                CancellationToken cancellationToken)
            {
                Timeout = timeoutMilliseconds;

                if (_failure != null)
                {
                    throw _failure;
                }

                return _icmp;
            }

            public IReadOnlyList<int> FindOpenTcpPorts(
                IPAddress address,
                IReadOnlyList<int> ports,
                int timeoutMilliseconds,
                CancellationToken cancellationToken)
            {
                if (_failure != null)
                {
                    throw _failure;
                }

                return ports
                    .Where(_open.Contains)
                    .ToArray();
            }
        }
    }
}
