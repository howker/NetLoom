using System;
using System.Collections.Generic;
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
    public sealed class Sprint40DesktopMonitoringControlTests
    {
        private static readonly DateTime PollCompletedUtc =
            new DateTime(
                2026, 9, 19, 11, 30, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void ScheduleCommandUsesStableTargetAndExistingEnginePolicy()
        {
            var target =
                Target();

            var policy =
                Policy();

            var tokens =
                EngineMonitoringCommandBuilder
                    .BuildScheduleTokens(
                        target,
                        policy,
                        @"C:\Net Loom\netloom.db");

            var parsed =
                EngineCommandLine.Parse(
                    tokens.ToArray());

            Assert.AreEqual(
                "schedule",
                parsed.Command);
            Assert.AreEqual(
                target.DeviceId,
                parsed.DeviceId);
            Assert.AreEqual(
                target.TargetAddress,
                parsed.Address);
            Assert.AreEqual(
                @"C:\Net Loom\netloom.db",
                parsed.DatabasePath);
            Assert.AreEqual(
                policy.Port,
                parsed.Port);
            Assert.AreEqual(
                policy.Version,
                parsed.Version);
            Assert.AreEqual(
                policy.TimeoutMilliseconds,
                parsed.TimeoutMilliseconds);
            Assert.AreEqual(
                policy.RetryCount,
                parsed.RetryCount);
            Assert.AreEqual(
                policy.MaxRepetitions,
                parsed.MaxRepetitions);
            Assert.AreEqual(
                60,
                parsed.IntervalSeconds);
            Assert.IsTrue(
                parsed.ControlStdin);
            CollectionAssert.AreEqual(
                policy.Kinds.ToArray(),
                parsed.Kinds.ToArray());
            Assert.AreEqual(
                policy.InterfaceErrorRatePerMinuteThreshold,
                parsed.InterfaceErrorRatePerMinuteThreshold);
            Assert.AreEqual(
                policy.InterfaceDiscardRatePerMinuteThreshold,
                parsed.InterfaceDiscardRatePerMinuteThreshold);

            var formatted =
                EngineMonitoringCommandBuilder
                    .FormatArguments(
                        tokens);

            StringAssert.Contains(
                formatted,
                "\"C:\\Net Loom\\netloom.db\"");
        }

        [TestMethod]
        public void SessionPolicyRejectsIntervalsEngineCannotRepresent()
        {
            try
            {
                new MonitoringSessionPolicy(
                    TimeSpan.FromMilliseconds(1500),
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
                    "Engine schedule interval must be whole seconds.");
            }
            catch (ArgumentOutOfRangeException exception)
            {
                Assert.AreEqual(
                    "interval",
                    exception.ParamName);
            }
        }

        [TestMethod]
        public async Task OwnedScheduleTransitionsFromStartThroughPollAndStop()
        {
            var factory =
                new FakeEngineProcessFactory();

            using (var control =
                new DesktopEngineMonitoringControl(
                    "NetLoom.Engine.exe",
                    @"C:\Net Loom\netloom.db",
                    factory))
            {
                var states =
                    new List<MonitoringControlState>();

                control.SnapshotChanged +=
                    (sender, eventArgs) =>
                    {
                        lock (states)
                        {
                            states.Add(
                                eventArgs.Snapshot.State);
                        }
                    };

                var target =
                    Target();

                var start =
                    control.StartAsync(
                        target,
                        Policy(),
                        CancellationToken.None);

                var process =
                    factory.LastSession;

                Assert.AreEqual(
                    MonitoringControlState.Starting,
                    control.Current.State);

                process.EmitOutput(
                    "localized human output that must be ignored");

                Assert.AreEqual(
                    MonitoringControlState.Starting,
                    control.Current.State);

                process.EmitOutput(
                    "NETLOOM_SCHEDULE state=started intervalSeconds=60");

                await start;

                Assert.AreEqual(
                    MonitoringControlState.Running,
                    control.Current.State);
                Assert.AreSame(
                    target,
                    control.Current.ActiveTarget);

                process.EmitOutput(
                    "NETLOOM_POLL state=started");

                Assert.AreEqual(
                    MonitoringControlState.Polling,
                    control.Current.State);

                process.EmitOutput(
                    CompletedMarker(
                        true));

                Assert.AreEqual(
                    MonitoringControlState.Running,
                    control.Current.State);
                Assert.AreEqual(
                    PollCompletedUtc,
                    control.Current.LastSuccessfulPollUtc);

                await control.PollNowAsync(
                    Target(
                        "192.0.2.99"),
                    Policy(),
                    CancellationToken.None);

                Assert.AreEqual(
                    "POLL_NOW",
                    process.WrittenLines.Last());
                Assert.AreSame(
                    target,
                    control.Current.ActiveTarget);

                var stop =
                    control.StopAsync(
                        CancellationToken.None);

                Assert.AreEqual(
                    MonitoringControlState.Stopping,
                    control.Current.State);
                Assert.AreEqual(
                    "STOP",
                    process.WrittenLines.Last());

                process.Complete(
                    0);

                await stop;

                Assert.AreEqual(
                    MonitoringControlState.Stopped,
                    control.Current.State);
                Assert.IsNull(
                    control.Current.ActiveTarget);
                Assert.AreEqual(
                    PollCompletedUtc,
                    control.Current.LastSuccessfulPollUtc);

                Assert.IsTrue(
                    SpinWait.SpinUntil(
                        () =>
                        {
                            lock (states)
                            {
                                return states.Count >= 6;
                            }
                        },
                        TimeSpan.FromSeconds(2)));

                MonitoringControlState[] observed;

                lock (states)
                {
                    observed =
                        states.ToArray();
                }

                CollectionAssert.AreEqual(
                    new[]
                    {
                        MonitoringControlState.Starting,
                        MonitoringControlState.Running,
                        MonitoringControlState.Polling,
                        MonitoringControlState.Running,
                        MonitoringControlState.Stopping,
                        MonitoringControlState.Stopped
                    },
                    observed.Take(6).ToArray());
            }
        }

        [TestMethod]
        public async Task PollNowWhileStoppedRunsCanonicalPollOnce()
        {
            var factory =
                new FakeEngineProcessFactory();

            using (var control =
                new DesktopEngineMonitoringControl(
                    "NetLoom.Engine.exe",
                    @"C:\Net Loom\netloom.db",
                    factory))
            {
                var target =
                    Target();

                var poll =
                    control.PollNowAsync(
                        target,
                        Policy(),
                        CancellationToken.None);

                var process =
                    factory.LastSession;

                Assert.AreEqual(
                    MonitoringControlState.Polling,
                    control.Current.State);

                var tokens =
                    EngineMonitoringCommandBuilder
                        .BuildPollOnceTokens(
                            target,
                            Policy(),
                            @"C:\Net Loom\netloom.db");

                Assert.AreEqual(
                    EngineMonitoringCommandBuilder
                        .FormatArguments(
                            tokens),
                    factory.LastRequest.Arguments);

                process.EmitOutput(
                    "NETLOOM_POLL state=started");
                process.EmitOutput(
                    CompletedMarker(
                        true));
                process.Complete(
                    0);

                await poll;

                Assert.AreEqual(
                    MonitoringControlState.Stopped,
                    control.Current.State);
                Assert.AreEqual(
                    PollCompletedUtc,
                    control.Current.LastSuccessfulPollUtc);
            }
        }

        [TestMethod]
        public async Task UnexpectedOwnedEngineExitBecomesFaulted()
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
                    control.StartAsync(
                        Target(),
                        Policy(),
                        CancellationToken.None);

                factory.LastSession.EmitOutput(
                    "NETLOOM_SCHEDULE state=started intervalSeconds=60");

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
            }
        }

        [TestMethod]
        public async Task ProcessStartFailureBecomesFaultedWithoutPretendingToRun()
        {
            var factory =
                new FakeEngineProcessFactory
                {
                    StartFailure =
                        new InvalidOperationException(
                            "synthetic")
                };

            using (var control =
                new DesktopEngineMonitoringControl(
                    "NetLoom.Engine.exe",
                    "netloom.db",
                    factory))
            {
                try
                {
                    await control.StartAsync(
                        Target(),
                        Policy(),
                        CancellationToken.None);

                    Assert.Fail(
                        "Synthetic child-process start failure must surface.");
                }
                catch (InvalidOperationException exception)
                {
                    Assert.AreEqual(
                        "synthetic",
                        exception.Message);
                }

                Assert.AreEqual(
                    MonitoringControlState.Faulted,
                    control.Current.State);
                Assert.AreEqual(
                    "ENGINE_PROCESS_START_FAILED",
                    control.Current.FaultMessage);
            }
        }

        [TestMethod]
        public void MarkerParserAcceptsOnlyDocumentedMachineLines()
        {
            EngineMachineMarker marker;

            Assert.IsFalse(
                EngineMachineMarkerParser.TryParse(
                    "SCHEDULER: started intervalSeconds=60",
                    out marker));

            Assert.IsFalse(
                EngineMachineMarkerParser.TryParse(
                    "POLL: success=1 failed=0",
                    out marker));

            Assert.IsTrue(
                EngineMachineMarkerParser.TryParse(
                    CompletedMarker(
                        true),
                    out marker));

            Assert.AreEqual(
                EngineMachineMarkerKind.PollCompleted,
                marker.Kind);
            Assert.AreEqual(
                PollCompletedUtc,
                marker.CompletedUtc);
            Assert.AreEqual(
                true,
                marker.AnySucceeded);

            Assert.IsFalse(
                EngineMachineMarkerParser.TryParse(
                    "NETLOOM_POLL state=completed completedUtc=not-a-date success=1 failed=0 anySucceeded=true",
                    out marker));

            Assert.IsFalse(
                EngineMachineMarkerParser.TryParse(
                    "NETLOOM_POLL state=completed completedUtc=2026-09-19T11:30:00.0000000Z success=0 failed=1 anySucceeded=true",
                    out marker));
        }

        private static MonitoringTarget Target(
            string address = "192.0.2.40")
        {
            return new MonitoringTarget(
                Guid.Parse(
                    "11111111-1111-1111-1111-111111111111"),
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

        private static string CompletedMarker(
            bool anySucceeded)
        {
            return
                "NETLOOM_POLL state=completed completedUtc=" +
                PollCompletedUtc.ToString("o") +
                " success=" +
                (anySucceeded ? "1" : "0") +
                " failed=" +
                (anySucceeded ? "0" : "1") +
                " anySucceeded=" +
                (anySucceeded ? "true" : "false");
        }

        private sealed class FakeEngineProcessFactory :
            IEngineProcessFactory
        {
            public Exception StartFailure { get; set; }

            public EngineProcessStartRequest LastRequest { get; private set; }

            public FakeEngineProcessSession LastSession { get; private set; }

            public IEngineProcessSession Start(
                EngineProcessStartRequest request)
            {
                LastRequest = request;

                if (StartFailure != null)
                {
                    throw StartFailure;
                }

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

            public bool ReadStarted { get; private set; }

            public bool Terminated { get; private set; }

            public void BeginRead()
            {
                ReadStarted = true;
            }

            public void WriteLine(
                string line)
            {
                WrittenLines.Add(
                    line);
            }

            public void Terminate()
            {
                Terminated = true;
                _completion.TrySetResult(
                    -1);
            }

            public void EmitOutput(
                string line)
            {
                OutputLineReceived?.Invoke(
                    line);
            }

            public void EmitError(
                string line)
            {
                ErrorLineReceived?.Invoke(
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
