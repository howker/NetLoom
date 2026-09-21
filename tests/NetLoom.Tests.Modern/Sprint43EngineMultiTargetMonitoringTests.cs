using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using NetLoom.Engine;

namespace NetLoom.Tests.Modern
{
    [TestClass]
    public sealed class Sprint43EngineMultiTargetMonitoringTests
    {
        private static readonly DateTime T1 =
            new DateTime(
                2026, 9, 21, 12, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void ScheduleSetCommandUsesExplicitTargetSetConcurrencyAndJitter()
        {
            var parsed =
                EngineCommandLine.Parse(
                    new[]
                    {
                        "schedule-set",
                        "--targets-file",
                        "targets.tsv",
                        "--max-concurrency",
                        "3",
                        "--startup-jitter-seconds",
                        "7",
                        "--interval-seconds",
                        "60",
                        "--control-stdin",
                        "true",
                        "--kinds",
                        "Lldp,Interface"
                    });

            Assert.AreEqual(
                "schedule-set",
                parsed.Command);
            Assert.AreEqual(
                "targets.tsv",
                parsed.TargetsFilePath);
            Assert.AreEqual(
                3,
                parsed.MaxConcurrentPolls);
            Assert.AreEqual(
                7,
                parsed.StartupJitterSeconds);
            Assert.AreEqual(
                60,
                parsed.IntervalSeconds);
            Assert.IsTrue(
                parsed.ControlStdin);
            Assert.IsNull(
                parsed.Address);
            Assert.IsFalse(
                parsed.DeviceId.HasValue);
            Assert.AreEqual(
                2,
                parsed.Kinds.Count);

            try
            {
                EngineCommandLine.Parse(
                    new[]
                    {
                        "schedule-set",
                        "--targets-file",
                        "targets.tsv",
                        "--max-concurrency",
                        "2",
                        "--startup-jitter-seconds",
                        "61",
                        "--interval-seconds",
                        "60"
                    });

                Assert.Fail(
                    "Startup jitter must remain bounded by cadence.");
            }
            catch (ArgumentException exception)
            {
                Assert.AreEqual(
                    "STARTUP_JITTER_EXCEEDS_INTERVAL",
                    exception.Message);
            }
        }

        [TestMethod]
        public void TargetSetFileBuildsStableRequestsAndDeterministicBoundedJitter()
        {
            var firstId =
                Guid.Parse(
                    "11111111-1111-1111-1111-111111111111");

            var secondId =
                Guid.Parse(
                    "22222222-2222-2222-2222-222222222222");

            var path =
                Path.GetTempFileName();

            try
            {
                File.WriteAllLines(
                    path,
                    new[]
                    {
                        firstId.ToString("D") +
                        "\t192.0.2.31",
                        secondId.ToString("D") +
                        "\t192.0.2.32"
                    });

                var first =
                    EngineMonitoringTargetSetFile.Read(
                        path,
                        Request,
                        TimeSpan.FromSeconds(60),
                        TimeSpan.FromSeconds(10));

                var second =
                    EngineMonitoringTargetSetFile.Read(
                        path,
                        Request,
                        TimeSpan.FromSeconds(60),
                        TimeSpan.FromSeconds(10));

                Assert.AreEqual(
                    2,
                    first.Count);

                Assert.AreEqual(
                    firstId,
                    first[0].DeviceId);
                Assert.AreEqual(
                    "192.0.2.31",
                    first[0].Request.Address.ToString());
                Assert.AreEqual(
                    TimeSpan.FromSeconds(60),
                    first[0].Cadence);

                for (var index = 0;
                     index < first.Count;
                     index++)
                {
                    Assert.IsTrue(
                        first[index].InitialDelay >=
                        TimeSpan.Zero);

                    Assert.IsTrue(
                        first[index].InitialDelay <=
                        TimeSpan.FromSeconds(10));

                    Assert.AreEqual(
                        first[index].InitialDelay,
                        second[index].InitialDelay);
                }
            }
            finally
            {
                File.Delete(
                    path);
            }
        }

        [TestMethod]
        public void TargetSetFileRejectsDuplicateStableDeviceIdentity()
        {
            var deviceId =
                Guid.Parse(
                    "33333333-3333-3333-3333-333333333333");

            var path =
                Path.GetTempFileName();

            try
            {
                File.WriteAllLines(
                    path,
                    new[]
                    {
                        deviceId.ToString("D") +
                        "\t192.0.2.33",
                        deviceId.ToString("D") +
                        "\t192.0.2.34"
                    });

                try
                {
                    EngineMonitoringTargetSetFile.Read(
                        path,
                        Request,
                        TimeSpan.FromSeconds(60),
                        TimeSpan.Zero);

                    Assert.Fail(
                        "Duplicate DeviceId must be rejected before scheduling.");
                }
                catch (InvalidDataException exception)
                {
                    StringAssert.StartsWith(
                        exception.Message,
                        "TARGET_SET_DUPLICATE_DEVICE_ID");
                }
            }
            finally
            {
                File.Delete(
                    path);
            }
        }

        [TestMethod]
        public void PollNowReleasesAllActiveMultiTargetDelaysAndStopCancelsSchedule()
        {
            using (var cancellation =
                new CancellationTokenSource())
            {
                var output =
                    new StringWriter(
                        CultureInfo.InvariantCulture);

                var control =
                    new EngineMultiTargetScheduleStdinControl(
                        cancellation,
                        new StringReader(
                            string.Empty),
                        output);

                var first =
                    control.WaitAsync(
                        TimeSpan.FromMinutes(5),
                        cancellation.Token);

                var second =
                    control.WaitAsync(
                        TimeSpan.FromMinutes(5),
                        cancellation.Token);

                Assert.IsTrue(
                    SpinWait.SpinUntil(
                        () =>
                            control.ActiveDelayCount == 2,
                        TimeSpan.FromSeconds(2)));

                control.AcceptLine(
                    "POLL_NOW");

                Assert.IsTrue(
                    Task.WaitAll(
                        new[]
                        {
                            first,
                            second
                        },
                        TimeSpan.FromSeconds(2)));

                Assert.IsTrue(
                    SpinWait.SpinUntil(
                        () =>
                            control.ActiveDelayCount == 0,
                        TimeSpan.FromSeconds(2)));

                StringAssert.Contains(
                    output.ToString(),
                    "NETLOOM_CONTROL command=POLL_NOW result=accepted");

                control.AcceptLine(
                    "STOP");

                Assert.IsTrue(
                    cancellation.IsCancellationRequested);

                StringAssert.Contains(
                    output.ToString(),
                    "NETLOOM_CONTROL command=STOP result=accepted");
            }
        }

        [TestMethod]
        public void RunnerPollsMultipleStableTargetsAndEmitsTargetIdentityMarkers()
        {
            var firstId =
                Guid.Parse(
                    "44444444-4444-4444-4444-444444444444");

            var secondId =
                Guid.Parse(
                    "55555555-5555-5555-5555-555555555555");

            using (var cancellation =
                new CancellationTokenSource())
            {
                var pollCount = 0;
                var completionCount = 0;

                var output =
                    new StringWriter(
                        CultureInfo.InvariantCulture);

                var runner =
                    new EngineMultiTargetMonitoringRunner(
                        request =>
                        {
                            var count =
                                Interlocked.Increment(
                                    ref pollCount);

                            if (count == 2)
                            {
                                cancellation.Cancel();
                            }

                            return Result(
                                request.Address);
                        },
                        (delay, token) =>
                            Task.Delay(
                                Timeout.Infinite,
                                token));

                var result =
                    runner.Run(
                        new[]
                        {
                            Target(
                                firstId,
                                "192.0.2.41"),
                            Target(
                                secondId,
                                "192.0.2.42")
                        },
                        new MonitoringConcurrencyPolicy(
                            2),
                        cancellation.Token,
                        output,
                        (target, pollResult) =>
                            Interlocked.Increment(
                                ref completionCount));

                Assert.AreEqual(
                    2,
                    result.CompletedPolls);
                Assert.AreEqual(
                    2,
                    pollCount);
                Assert.AreEqual(
                    2,
                    completionCount);
                Assert.IsTrue(
                    result.CancellationRequested);

                var text =
                    output.ToString();

                StringAssert.Contains(
                    text,
                    "NETLOOM_TARGET_POLL state=started deviceId=" +
                    firstId.ToString("D") +
                    " address=192.0.2.41");

                StringAssert.Contains(
                    text,
                    "NETLOOM_TARGET_POLL state=completed deviceId=" +
                    secondId.ToString("D") +
                    " address=192.0.2.42");

                EngineMachineOutput
                    .WriteScheduleSetStarted(
                        output,
                        2,
                        60,
                        2,
                        10);

                EngineMachineOutput
                    .WriteScheduleSetStopped(
                        output,
                        2,
                        0);

                text =
                    output.ToString();

                StringAssert.Contains(
                    text,
                    "NETLOOM_SCHEDULE_SET state=started targets=2 intervalSeconds=60 maxConcurrency=2 startupJitterSeconds=10");

                StringAssert.Contains(
                    text,
                    "NETLOOM_SCHEDULE_SET state=stopped completedPolls=2 backpressureSkips=0");

                foreach (var character in text)
                {
                    Assert.IsTrue(
                        character <= 127,
                        "Machine marker output must remain ASCII.");
                }
            }
        }

        [TestMethod]
        public void TargetSetOptionsRemainExclusiveToScheduleSet()
        {
            try
            {
                EngineCommandLine.Parse(
                    new[]
                    {
                        "schedule",
                        "--address",
                        "192.0.2.50",
                        "--targets-file",
                        "targets.tsv"
                    });

                Assert.Fail(
                    "Single-target schedule must reject target-set options.");
            }
            catch (ArgumentException exception)
            {
                Assert.AreEqual(
                    "TARGET_SET_OPTIONS_ONLY_VALID_FOR_SCHEDULE_SET",
                    exception.Message);
            }
        }

        private static MonitoringPollRequest Request(
            Guid deviceId,
            IPAddress address)
        {
            return new MonitoringPollRequest(
                address,
                161,
                SnmpVersion.V2C,
                new SnmpCommunityCredentials(
                    new byte[] { 4, 5, 6 }),
                1000,
                0,
                10,
                new[]
                {
                    MonitoringPollKind.Lldp
                },
                deviceId);
        }

        private static MonitoringScheduleTarget Target(
            Guid deviceId,
            string address)
        {
            return new MonitoringScheduleTarget(
                Request(
                    deviceId,
                    IPAddress.Parse(
                        address)),
                TimeSpan.FromSeconds(60),
                TimeSpan.Zero);
        }

        private static MonitoringPollResult Result(
            IPAddress address)
        {
            return new MonitoringPollResult(
                address,
                T1,
                T1.AddSeconds(1),
                new[]
                {
                    new MonitoringPollStepResult(
                        MonitoringPollKind.Lldp,
                        true,
                        null,
                        null)
                });
        }
    }
}
