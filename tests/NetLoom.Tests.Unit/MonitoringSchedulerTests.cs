using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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

        [TestMethod]
        public void MultiTargetSchedulerBoundsConcurrencyAndDropsBusyTicks()
        {
            var active = 0;
            var maximumActive = 0;
            var skipped = 0;

            using (var release =
                new ManualResetEvent(false))
            using (var twoStarted =
                new ManualResetEvent(false))
            using (var cancellation =
                new CancellationTokenSource())
            {
                var scheduler =
                    new MultiTargetMonitoringScheduler(
                        request =>
                        {
                            var current =
                                Interlocked.Increment(
                                    ref active);

                            UpdateMaximum(
                                ref maximumActive,
                                current);

                            if (current == 2)
                            {
                                twoStarted.Set();
                            }

                            release.WaitOne();

                            Interlocked.Decrement(
                                ref active);

                            return null;
                        },
                        (delay, token) =>
                            Task.Delay(
                                Timeout.Infinite,
                                token));

                var run =
                    Task.Run(
                        () =>
                            scheduler.Run(
                                new[]
                                {
                                    ScheduledTarget(
                                        "11111111-1111-1111-1111-111111111111",
                                        "192.0.2.21"),
                                    ScheduledTarget(
                                        "22222222-2222-2222-2222-222222222222",
                                        "192.0.2.22"),
                                    ScheduledTarget(
                                        "33333333-3333-3333-3333-333333333333",
                                        "192.0.2.23")
                                },
                                new MonitoringConcurrencyPolicy(
                                    2),
                                cancellation.Token,
                                onBackpressureSkipped:
                                    target =>
                                        Interlocked.Increment(
                                            ref skipped)));

                Assert.IsTrue(
                    twoStarted.WaitOne(
                        TimeSpan.FromSeconds(2)),
                    "Two different targets should be allowed to poll concurrently.");

                Assert.IsTrue(
                    SpinWait.SpinUntil(
                        () =>
                            Volatile.Read(
                                ref skipped) >= 1,
                        TimeSpan.FromSeconds(2)),
                    "A due target must be dropped for backpressure instead of queued behind occupied poll slots.");

                Assert.AreEqual(
                    2,
                    Volatile.Read(
                        ref maximumActive));

                cancellation.Cancel();
                release.Set();

                var result =
                    run.GetAwaiter()
                        .GetResult();

                Assert.AreEqual(
                    2,
                    result.CompletedPolls);

                Assert.IsTrue(
                    result.BackpressureSkips >= 1);

                Assert.IsTrue(
                    result.CancellationRequested);
            }
        }

        [TestMethod]
        public void MultiTargetSchedulerHonorsInitialDelayCadenceAndNoOverlapPerDevice()
        {
            var events =
                new List<string>();

            var active = 0;
            var maximumActive = 0;
            var polls = 0;

            using (var cancellation =
                new CancellationTokenSource())
            {
                var scheduler =
                    new MultiTargetMonitoringScheduler(
                        request =>
                        {
                            var current =
                                Interlocked.Increment(
                                    ref active);

                            UpdateMaximum(
                                ref maximumActive,
                                current);

                            events.Add(
                                "Poll");

                            var count =
                                Interlocked.Increment(
                                    ref polls);

                            Interlocked.Decrement(
                                ref active);

                            if (count == 2)
                            {
                                cancellation.Cancel();
                            }

                            return null;
                        },
                        (delay, token) =>
                        {
                            events.Add(
                                "Wait:" +
                                delay.TotalSeconds.ToString(
                                    "0"));

                            return Task.CompletedTask;
                        });

                var result =
                    scheduler.Run(
                        new[]
                        {
                            ScheduledTarget(
                                "44444444-4444-4444-4444-444444444444",
                                "192.0.2.24",
                                5,
                                3)
                        },
                        new MonitoringConcurrencyPolicy(
                            2),
                        cancellation.Token);

                CollectionAssert.AreEqual(
                    new[]
                    {
                        "Wait:3",
                        "Poll",
                        "Wait:5",
                        "Poll"
                    },
                    events);

                Assert.AreEqual(
                    1,
                    maximumActive,
                    "The same stable DeviceId must never overlap with itself.");

                Assert.AreEqual(
                    2,
                    result.CompletedPolls);

                Assert.AreEqual(
                    0,
                    result.BackpressureSkips);
            }
        }

        [TestMethod]
        public void MultiTargetSchedulerCancellationStopsFuturePollsAfterActivePollCompletes()
        {
            var polls = 0;

            using (var pollStarted =
                new ManualResetEvent(false))
            using (var release =
                new ManualResetEvent(false))
            using (var cancellation =
                new CancellationTokenSource())
            {
                var scheduler =
                    new MultiTargetMonitoringScheduler(
                        request =>
                        {
                            Interlocked.Increment(
                                ref polls);

                            pollStarted.Set();
                            release.WaitOne();
                            return null;
                        });

                var run =
                    Task.Run(
                        () =>
                            scheduler.Run(
                                new[]
                                {
                                    ScheduledTarget(
                                        "55555555-5555-5555-5555-555555555555",
                                        "192.0.2.25")
                                },
                                new MonitoringConcurrencyPolicy(
                                    1),
                                cancellation.Token));

                Assert.IsTrue(
                    pollStarted.WaitOne(
                        TimeSpan.FromSeconds(2)));

                cancellation.Cancel();
                release.Set();

                var result =
                    run.GetAwaiter()
                        .GetResult();

                Assert.AreEqual(
                    1,
                    Volatile.Read(
                        ref polls));

                Assert.AreEqual(
                    1,
                    result.CompletedPolls);

                Assert.IsTrue(
                    result.CancellationRequested);
            }
        }

        [TestMethod]
        public void MultiTargetSchedulerRequiresStableUniqueDeviceIdentity()
        {
            try
            {
                new MonitoringScheduleTarget(
                    Request(),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.Zero);

                Assert.Fail(
                    "A multi-target schedule target requires stable DeviceId.");
            }
            catch (ArgumentException)
            {
            }

            var deviceId =
                Guid.Parse(
                    "66666666-6666-6666-6666-666666666666");

            var scheduler =
                new MultiTargetMonitoringScheduler(
                    request => null);

            try
            {
                scheduler.Run(
                    new[]
                    {
                        ScheduledTarget(
                            deviceId.ToString(),
                            "192.0.2.26"),
                        ScheduledTarget(
                            deviceId.ToString(),
                            "192.0.2.27")
                    },
                    new MonitoringConcurrencyPolicy(
                        2),
                    CancellationToken.None);

                Assert.Fail(
                    "Duplicate stable DeviceId targets must be rejected before scheduling.");
            }
            catch (ArgumentException)
            {
            }
        }

        private static void UpdateMaximum(
            ref int maximum,
            int candidate)
        {
            while (true)
            {
                var observed =
                    Volatile.Read(
                        ref maximum);

                if (candidate <= observed)
                {
                    return;
                }

                if (Interlocked.CompareExchange(
                        ref maximum,
                        candidate,
                        observed) == observed)
                {
                    return;
                }
            }
        }

        private static MonitoringScheduleTarget ScheduledTarget(
            string deviceId,
            string address,
            int cadenceSeconds = 5,
            int initialDelaySeconds = 0)
        {
            return new MonitoringScheduleTarget(
                Request(
                    Guid.Parse(
                        deviceId),
                    address),
                TimeSpan.FromSeconds(
                    cadenceSeconds),
                TimeSpan.FromSeconds(
                    initialDelaySeconds));
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

        private static MonitoringPollRequest Request(
            Guid? deviceId = null,
            string address = "192.0.2.20")
        {
            return new MonitoringPollRequest(
                IPAddress.Parse(
                    address),
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
                },
                deviceId);
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
