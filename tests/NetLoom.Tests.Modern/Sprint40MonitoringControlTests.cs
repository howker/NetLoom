using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring;
using NetLoom.Application.MonitoringControl;
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
using NetLoom.Engine;

namespace NetLoom.Tests.Modern
{
    [TestClass]
    public sealed class Sprint40MonitoringControlTests
    {
        private static readonly DateTime T1 =
            new DateTime(
                2026, 9, 19, 10, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void ScheduleControlIsOptInAndScheduleOnly()
        {
            var parsed =
                EngineCommandLine.Parse(
                    new[]
                    {
                        "schedule",
                        "--address",
                        "192.0.2.40",
                        "--control-stdin",
                        "true"
                    });

            Assert.IsTrue(
                parsed.ControlStdin);

            var defaultParsed =
                EngineCommandLine.Parse(
                    new[]
                    {
                        "schedule",
                        "--address",
                        "192.0.2.40"
                    });

            Assert.IsFalse(
                defaultParsed.ControlStdin);

            try
            {
                EngineCommandLine.Parse(
                    new[]
                    {
                        "poll-once",
                        "--address",
                        "192.0.2.40",
                        "--control-stdin",
                        "true"
                    });

                Assert.Fail(
                    "poll-once must reject schedule stdin control.");
            }
            catch (ArgumentException exception)
            {
                Assert.AreEqual(
                    "CONTROL_STDIN_ONLY_VALID_FOR_SCHEDULE",
                    exception.Message);
            }
        }

        [TestMethod]
        public void PollNowInterruptsOnlyDelayAndRunsNextSchedulerCycle()
        {
            var events =
                new List<string>();

            using (var cancellation =
                new CancellationTokenSource())
            {
                var output =
                    new RecordingWriter();

                var control =
                    new EngineScheduleStdinControl(
                        cancellation,
                        new StringReader(string.Empty),
                        output);

                var scheduler =
                    new MonitoringScheduler(
                        Runtime(events),
                        control.Wait);

                var completed = 0;

                var run =
                    Task.Run(
                        () => scheduler.Run(
                            Request(),
                            TimeSpan.FromMinutes(5),
                            cancellation.Token,
                            pollResult =>
                            {
                                completed++;

                                if (completed == 2)
                                {
                                    cancellation.Cancel();
                                }
                            },
                            () => events.Add(
                                "Starting")));

                Assert.IsTrue(
                    SpinWait.SpinUntil(
                        () => control.HasActiveDelay,
                        TimeSpan.FromSeconds(2)),
                    "Scheduler must enter its delay after the first cycle.");

                Assert.IsFalse(
                    cancellation.IsCancellationRequested);

                control.AcceptLine(
                    "POLL_NOW");

                Assert.IsTrue(
                    run.Wait(
                        TimeSpan.FromSeconds(2)),
                    "POLL_NOW must release only the active delay.");

                var result =
                    run.GetAwaiter()
                        .GetResult();

                CollectionAssert.AreEqual(
                    new[]
                    {
                        "Starting",
                        "Lldp",
                        "Starting",
                        "Lldp"
                    },
                    events);

                Assert.AreEqual(
                    2,
                    result.CompletedCycles);

                StringAssert.Contains(
                    output.ToString(),
                    "NETLOOM_CONTROL command=POLL_NOW result=accepted");
            }
        }

        [TestMethod]
        public void PollNowOutsideDelayDoesNotQueueAnotherCycle()
        {
            using (var cancellation =
                new CancellationTokenSource())
            {
                var output =
                    new RecordingWriter();

                var control =
                    new EngineScheduleStdinControl(
                        cancellation,
                        new StringReader(string.Empty),
                        output);

                control.AcceptLine(
                    "POLL_NOW");

                Assert.IsFalse(
                    cancellation.IsCancellationRequested);

                StringAssert.Contains(
                    output.ToString(),
                    "NETLOOM_CONTROL command=POLL_NOW result=no-active-delay");
            }
        }

        [TestMethod]
        public void StopAndEndOfInputCancelOwnedSchedule()
        {
            using (var stopCancellation =
                new CancellationTokenSource())
            {
                var stopOutput =
                    new RecordingWriter();

                var stopControl =
                    new EngineScheduleStdinControl(
                        stopCancellation,
                        new StringReader(string.Empty),
                        stopOutput);

                stopControl.AcceptLine(
                    "STOP");

                Assert.IsTrue(
                    stopCancellation.IsCancellationRequested);

                StringAssert.Contains(
                    stopOutput.ToString(),
                    "NETLOOM_CONTROL command=STOP result=accepted");
            }

            using (var eofCancellation =
                new CancellationTokenSource())
            {
                var eofOutput =
                    new RecordingWriter();

                var eofControl =
                    new EngineScheduleStdinControl(
                        eofCancellation,
                        new StringReader(string.Empty),
                        eofOutput);

                eofControl.StartReading();

                Assert.IsTrue(
                    SpinWait.SpinUntil(
                        () => eofCancellation
                            .IsCancellationRequested,
                        TimeSpan.FromSeconds(2)));

                StringAssert.Contains(
                    eofOutput.ToString(),
                    "NETLOOM_CONTROL event=EOF action=stop");
            }
        }

        [TestMethod]
        public void MachineMarkersAreAsciiInvariantAndExplicitlyFlushed()
        {
            var previousCulture =
                CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture =
                    CultureInfo.GetCultureInfo(
                        "fr-FR");

                var output =
                    new RecordingWriter();

                EngineMachineOutput
                    .WriteControlReady(
                        output);

                EngineMachineOutput
                    .WriteScheduleStarted(
                        output,
                        60);

                EngineMachineOutput
                    .WritePollStarted(
                        output);

                EngineMachineOutput
                    .WritePollCompleted(
                        output,
                        new MonitoringPollResult(
                            IPAddress.Parse(
                                "192.0.2.40"),
                            T1,
                            T1.AddSeconds(2),
                            new[]
                            {
                                new MonitoringPollStepResult(
                                    MonitoringPollKind.Lldp,
                                    true,
                                    null,
                                    null),
                                new MonitoringPollStepResult(
                                    MonitoringPollKind.Cdp,
                                    false,
                                    "TimeoutException",
                                    "timeout")
                            }));

                var text =
                    output.ToString();

                StringAssert.Contains(
                    text,
                    "NETLOOM_CONTROL state=ready");

                StringAssert.Contains(
                    text,
                    "NETLOOM_SCHEDULE state=started intervalSeconds=60");

                StringAssert.Contains(
                    text,
                    "NETLOOM_POLL state=started");

                StringAssert.Contains(
                    text,
                    "NETLOOM_POLL state=completed completedUtc=2026-09-19T10:00:02.0000000Z success=1 failed=1 anySucceeded=true");

                Assert.IsTrue(
                    output.FlushCount >= 4);

                foreach (var character in text)
                {
                    Assert.IsTrue(
                        character <= 127,
                        "Machine marker output must remain ASCII.");
                }
            }
            finally
            {
                CultureInfo.CurrentCulture =
                    previousCulture;
            }
        }

        [TestMethod]
        public void MonitoringTargetUsesStableDeviceIdAndSessionTargetAddress()
        {
            var deviceId =
                Guid.NewGuid();

            var target =
                new MonitoringTarget(
                    deviceId,
                    IPAddress.Parse(
                        "192.0.2.40"));

            Assert.AreEqual(
                deviceId,
                target.DeviceId);

            Assert.AreEqual(
                "192.0.2.40",
                target.TargetAddress.ToString());

            try
            {
                new MonitoringTarget(
                    Guid.Empty,
                    IPAddress.Loopback);

                Assert.Fail(
                    "Empty DeviceId must be rejected.");
            }
            catch (ArgumentException exception)
            {
                StringAssert.StartsWith(
                    exception.Message,
                    "DEVICE_ID_REQUIRED");

                Assert.AreEqual(
                    "deviceId",
                    exception.ParamName);
            }
        }

        [TestMethod]
        public void SessionPolicyValidatesExistingEngineKnobs()
        {
            var policy =
                new MonitoringSessionPolicy(
                    TimeSpan.FromSeconds(60),
                    SnmpVersion.V2C,
                    161,
                    2000,
                    1,
                    25,
                    new[]
                    {
                        MonitoringPollKind.Lldp,
                        MonitoringPollKind.Lldp,
                        MonitoringPollKind.Interface
                    },
                    10.0,
                    20.0);

            Assert.AreEqual(
                2,
                policy.Kinds.Count);

            try
            {
                new MonitoringSessionPolicy(
                    TimeSpan.Zero,
                    SnmpVersion.V2C,
                    161,
                    2000,
                    1,
                    25,
                    new[]
                    {
                        MonitoringPollKind.Lldp
                    });

                Assert.Fail(
                    "Non-positive interval must be rejected.");
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
                    "192.0.2.40"),
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

        private sealed class RecordingWriter :
            StringWriter
        {
            public int FlushCount { get; private set; }

            public override void Flush()
            {
                FlushCount++;
                base.Flush();
            }
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
                _events.Add(
                    "Lldp");

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
