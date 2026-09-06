using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring;
using NetLoom.Application.Observations.Arp;
using NetLoom.Application.Observations.Cdp;
using NetLoom.Application.Observations.Fdb;
using NetLoom.Application.Observations.Lldp;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using NetLoom.Domain.Observations.Arp;
using NetLoom.Domain.Observations.Cdp;
using NetLoom.Domain.Observations.Fdb;
using NetLoom.Domain.Observations.Lldp;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class MonitoringSchedulerTests
    {
        private static readonly DateTime T1 =
            new DateTime(
                2026, 1, 1, 10, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void SchedulerRunsImmediateCycleThenFixedDelayWithoutOverlap()
        {
            var events =
                new List<string>();

            var runtime =
                Runtime(
                    events);

            using (var cancellation =
                new CancellationTokenSource())
            {
                var callbacks = 0;

                var scheduler =
                    new MonitoringScheduler(
                        runtime,
                        (interval, token) =>
                        {
                            events.Add("Wait");
                        });

                var result =
                    scheduler.Run(
                        Request(),
                        TimeSpan.FromSeconds(5),
                        cancellation.Token,
                        poll =>
                        {
                            callbacks++;

                            if (callbacks == 2)
                            {
                                cancellation.Cancel();
                            }
                        });

                CollectionAssert.AreEqual(
                    new[]
                    {
                        "Lldp",
                        "Wait",
                        "Lldp"
                    },
                    events);

                Assert.AreEqual(
                    2,
                    result.CompletedCycles);

                Assert.IsTrue(
                    result.CancellationRequested);
            }
        }

        [TestMethod]
        public void AlreadyCanceledSchedulerRunsNoCycle()
        {
            var events =
                new List<string>();

            var runtime =
                Runtime(
                    events);

            using (var cancellation =
                new CancellationTokenSource())
            {
                cancellation.Cancel();

                var scheduler =
                    new MonitoringScheduler(
                        runtime,
                        (interval, token) =>
                        {
                            Assert.Fail(
                                "Wait must not run.");
                        });

                var result =
                    scheduler.Run(
                        Request(),
                        TimeSpan.FromSeconds(5),
                        cancellation.Token);

                Assert.AreEqual(
                    0,
                    result.CompletedCycles);

                Assert.IsTrue(
                    result.CancellationRequested);

                Assert.IsNull(
                    result.LastResult);

                Assert.AreEqual(
                    0,
                    events.Count);
            }
        }

        [TestMethod]
        public void CancellationDuringDelayStopsCleanly()
        {
            var events =
                new List<string>();

            var runtime =
                Runtime(
                    events);

            using (var cancellation =
                new CancellationTokenSource())
            {
                var scheduler =
                    new MonitoringScheduler(
                        runtime,
                        (interval, token) =>
                        {
                            cancellation.Cancel();

                            throw new OperationCanceledException(
                                token);
                        });

                var result =
                    scheduler.Run(
                        Request(),
                        TimeSpan.FromSeconds(5),
                        cancellation.Token);

                Assert.AreEqual(
                    1,
                    result.CompletedCycles);

                Assert.IsTrue(
                    result.CancellationRequested);
            }
        }

        [TestMethod]
        public void SchedulerRejectsNonPositiveInterval()
        {
            var scheduler =
                new MonitoringScheduler(
                    Runtime(
                        new List<string>()));

            try
            {
                scheduler.Run(
                    Request(),
                    TimeSpan.Zero,
                    CancellationToken.None);

                Assert.Fail(
                    "Expected invalid interval.");
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        private static MonitoringRuntime Runtime(
            IList<string> events)
        {
            return new MonitoringRuntime(
                new RecordingLldpCollector(
                    events),
                new NoopCdpCollector(),
                new NoopFdbCollector(),
                new NoopArpCollector(),
                () => T1);
        }

        private static MonitoringPollRequest Request()
        {
            return new MonitoringPollRequest(
                IPAddress.Parse(
                    "192.0.2.20"),
                161,
                SnmpVersion.V2C,
                new SnmpCommunityCredentials(
                    new byte[] { 4, 5, 6 }),
                1000,
                1,
                10,
                new[]
                {
                    MonitoringPollKind.Lldp
                });
        }

        private sealed class RecordingLldpCollector :
            ILldpCollector
        {
            private readonly IList<string> _events;

            public RecordingLldpCollector(
                IList<string> events)
            {
                _events = events;
            }

            public LldpObservation Collect(
                LldpCollectionRequest request)
            {
                _events.Add("Lldp");
                return null;
            }
        }

        private sealed class NoopCdpCollector :
            ICdpCollector
        {
            public CdpObservation Collect(
                CdpCollectionRequest request)
            {
                return null;
            }
        }

        private sealed class NoopFdbCollector :
            IFdbCollector
        {
            public FdbObservation Collect(
                FdbCollectionRequest request)
            {
                return null;
            }
        }

        private sealed class NoopArpCollector :
            IArpCollector
        {
            public ArpObservation Collect(
                ArpCollectionRequest request)
            {
                return null;
            }
        }
    }
}
