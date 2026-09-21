using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring;
using NetLoom.Application.MonitoringControl;
using NetLoom.Desktop.Monitoring;
using NetLoom.Domain.Access;
using NetLoom.Engine;

namespace NetLoom.Tests.Modern
{
    [TestClass]
    public sealed class Sprint43DesktopMultiTargetMonitoringTests
    {
        private static readonly DateTime FirstPollUtc =
            new DateTime(
                2026, 9, 21, 14, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void ScheduleSetCommandRoundTripsThroughEngineBoundary()
        {
            var policy =
                Policy();

            var setPolicy =
                new MonitoringTargetSetPolicy(
                    2,
                    TimeSpan.FromSeconds(10));

            var tokens =
                EngineMonitoringCommandBuilder
                    .BuildScheduleSetTokens(
                        @"C:\Net Loom\targets.tsv",
                        policy,
                        setPolicy,
                        @"C:\Net Loom\netloom.db");

            var parsed =
                EngineCommandLine.Parse(
                    tokens.ToArray());

            Assert.AreEqual(
                "schedule-set",
                parsed.Command);
            Assert.AreEqual(
                @"C:\Net Loom\targets.tsv",
                parsed.TargetsFilePath);
            Assert.AreEqual(
                2,
                parsed.MaxConcurrentPolls);
            Assert.AreEqual(
                10,
                parsed.StartupJitterSeconds);
            Assert.AreEqual(
                60,
                parsed.IntervalSeconds);
            Assert.AreEqual(
                @"C:\Net Loom\netloom.db",
                parsed.DatabasePath);
            Assert.IsTrue(
                parsed.ControlStdin);
            CollectionAssert.AreEqual(
                policy.Kinds.ToArray(),
                parsed.Kinds.ToArray());

            var formatted =
                EngineMonitoringCommandBuilder
                    .FormatArguments(
                        tokens);

            StringAssert.Contains(
                formatted,
                "\"C:\\Net Loom\\targets.tsv\"");
        }

        [TestMethod]
        public async Task OneOwnedEngineProcessRunsTargetSetAndTracksOverlappingPolls()
        {
            var factory =
                new FakeEngineProcessFactory();

            using (var control =
                new DesktopEngineMonitoringControl(
                    "NetLoom.Engine.exe",
                    @"C:\Net Loom\netloom.db",
                    factory))
            {
                var first =
                    Target(
                        "11111111-1111-1111-1111-111111111111",
                        "192.0.2.61");

                var second =
                    Target(
                        "22222222-2222-2222-2222-222222222222",
                        "192.0.2.62");

                var start =
                    control.StartSetAsync(
                        new[]
                        {
                            first,
                            second
                        },
                        Policy(),
                        new MonitoringTargetSetPolicy(
                            2,
                            TimeSpan.FromSeconds(10)),
                        CancellationToken.None);

                Assert.AreEqual(
                    1,
                    factory.StartCount);

                var process =
                    factory.LastSession;

                var manifest =
                    control.ActiveTargetSetFilePath;

                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(
                        manifest));
                Assert.IsTrue(
                    File.Exists(
                        manifest));

                CollectionAssert.AreEqual(
                    new[]
                    {
                        first.DeviceId.ToString("D") +
                        "\t192.0.2.61",
                        second.DeviceId.ToString("D") +
                        "\t192.0.2.62"
                    },
                    File.ReadAllLines(
                        manifest));

                StringAssert.StartsWith(
                    factory.LastRequest.Arguments,
                    "schedule-set ");

                process.EmitOutput(
                    "NETLOOM_SCHEDULE_SET state=started targets=2 intervalSeconds=60 maxConcurrency=2 startupJitterSeconds=10");

                await start;

                Assert.AreEqual(
                    MonitoringControlState.Running,
                    control.Current.State);
                Assert.IsNull(
                    control.Current.ActiveTarget);

                await control.PollNowAsync(
                    null,
                    null,
                    CancellationToken.None);

                Assert.AreEqual(
                    "POLL_NOW",
                    process.WrittenLines.Last());

                process.EmitOutput(
                    StartedMarker(
                        first));

                process.EmitOutput(
                    StartedMarker(
                        second));

                Assert.AreEqual(
                    MonitoringControlState.Polling,
                    control.Current.State);

                process.EmitOutput(
                    CompletedMarker(
                        first,
                        FirstPollUtc,
                        true));

                Assert.AreEqual(
                    MonitoringControlState.Polling,
                    control.Current.State);
                Assert.AreEqual(
                    FirstPollUtc,
                    control.Current.LastSuccessfulPollUtc);

                process.EmitOutput(
                    CompletedMarker(
                        second,
                        FirstPollUtc.AddSeconds(1),
                        false));

                Assert.AreEqual(
                    MonitoringControlState.Running,
                    control.Current.State);
                Assert.AreEqual(
                    FirstPollUtc,
                    control.Current.LastSuccessfulPollUtc);

                var stop =
                    control.StopAsync(
                        CancellationToken.None);

                Assert.AreEqual(
                    "STOP",
                    process.WrittenLines.Last());

                process.Complete(
                    0);

                await stop;

                Assert.AreEqual(
                    MonitoringControlState.Stopped,
                    control.Current.State);
                Assert.IsFalse(
                    File.Exists(
                        manifest));
            }
        }

        [TestMethod]
        public async Task UnexpectedTargetSetEngineExitCleansManifestAndFaults()
        {
            var factory =
                new FakeEngineProcessFactory();

            using (var control =
                new DesktopEngineMonitoringControl(
                    "NetLoom.Engine.exe",
                    "netloom.db",
                    factory))
            {
                var start =
                    control.StartSetAsync(
                        new[]
                        {
                            Target(
                                "33333333-3333-3333-3333-333333333333",
                                "192.0.2.63")
                        },
                        Policy(),
                        new MonitoringTargetSetPolicy(
                            1,
                            TimeSpan.Zero),
                        CancellationToken.None);

                var manifest =
                    control.ActiveTargetSetFilePath;

                factory.LastSession.EmitOutput(
                    "NETLOOM_SCHEDULE_SET state=started targets=1 intervalSeconds=60 maxConcurrency=1 startupJitterSeconds=0");

                await start;

                factory.LastSession.Complete(
                    9);

                Assert.IsTrue(
                    SpinWait.SpinUntil(
                        () =>
                            control.Current.State ==
                            MonitoringControlState.Faulted,
                        TimeSpan.FromSeconds(2)));

                Assert.AreEqual(
                    "ENGINE_PROCESS_EXITED_9",
                    control.Current.FaultMessage);

                Assert.IsFalse(
                    File.Exists(
                        manifest));
            }
        }

        [TestMethod]
        public void TargetSpecificMarkersRequireStableIdentity()
        {
            EngineMachineMarker marker;

            Assert.IsTrue(
                EngineMachineMarkerParser.TryParse(
                    "NETLOOM_TARGET_POLL state=started deviceId=44444444-4444-4444-4444-444444444444 address=192.0.2.64",
                    out marker));

            Assert.AreEqual(
                EngineMachineMarkerKind.TargetPollStarted,
                marker.Kind);
            Assert.AreEqual(
                Guid.Parse(
                    "44444444-4444-4444-4444-444444444444"),
                marker.DeviceId);
            Assert.AreEqual(
                IPAddress.Parse(
                    "192.0.2.64"),
                marker.TargetAddress);

            Assert.IsFalse(
                EngineMachineMarkerParser.TryParse(
                    "NETLOOM_TARGET_POLL state=started deviceId=not-a-guid address=192.0.2.64",
                    out marker));
        }

        private static MonitoringTarget Target(
            string deviceId,
            string address)
        {
            return new MonitoringTarget(
                Guid.Parse(
                    deviceId),
                IPAddress.Parse(
                    address));
        }

        private static MonitoringSessionPolicy Policy()
        {
            return new MonitoringSessionPolicy(
                TimeSpan.FromSeconds(60),
                SnmpVersion.V2C,
                1161,
                1500,
                2,
                40,
                new[]
                {
                    MonitoringPollKind.Lldp,
                    MonitoringPollKind.Interface
                },
                12.5,
                7.25);
        }

        private static string StartedMarker(
            MonitoringTarget target)
        {
            return
                "NETLOOM_TARGET_POLL state=started deviceId=" +
                target.DeviceId.ToString("D") +
                " address=" +
                target.TargetAddress;
        }

        private static string CompletedMarker(
            MonitoringTarget target,
            DateTime completedUtc,
            bool anySucceeded)
        {
            return
                "NETLOOM_TARGET_POLL state=completed deviceId=" +
                target.DeviceId.ToString("D") +
                " address=" +
                target.TargetAddress +
                " completedUtc=" +
                completedUtc.ToString(
                    "o",
                    CultureInfo.InvariantCulture) +
                " success=" +
                (anySucceeded
                    ? "1"
                    : "0") +
                " failed=" +
                (anySucceeded
                    ? "0"
                    : "1") +
                " anySucceeded=" +
                (anySucceeded
                    ? "true"
                    : "false");
        }

        private sealed class FakeEngineProcessFactory :
            IEngineProcessFactory
        {
            public int StartCount { get; private set; }

            public EngineProcessStartRequest LastRequest { get; private set; }

            public FakeEngineProcessSession LastSession { get; private set; }

            public IEngineProcessSession Start(
                EngineProcessStartRequest request)
            {
                StartCount++;
                LastRequest =
                    request;
                LastSession =
                    new FakeEngineProcessSession();

                return LastSession;
            }

            public void Dispose()
            {
            }
        }

        private sealed class FakeEngineProcessSession :
            IEngineProcessSession
        {
            private readonly TaskCompletionSource<int>
                _completion =
                    new TaskCompletionSource<int>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

            public event Action<string> OutputLineReceived;

            public event Action<string> ErrorLineReceived;

            public Task<int> Completion =>
                _completion.Task;

            public IList<string> WrittenLines { get; } =
                new List<string>();

            public void BeginRead()
            {
            }

            public void WriteLine(
                string line)
            {
                WrittenLines.Add(
                    line);
            }

            public void Terminate()
            {
                _completion.TrySetResult(
                    -1);
            }

            public void EmitOutput(
                string line)
            {
                OutputLineReceived?.Invoke(
                    line);
            }

            public void Complete(
                int exitCode)
            {
                _completion.TrySetResult(
                    exitCode);
            }

            public void Dispose()
            {
            }
        }
    }
}
