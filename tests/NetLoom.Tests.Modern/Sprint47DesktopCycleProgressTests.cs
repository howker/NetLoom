using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring;
using NetLoom.Application.MonitoringControl;
using NetLoom.Desktop.Monitoring;
using NetLoom.Domain.Access;

namespace NetLoom.Tests.Modern
{
    // Sprint 47: строки Engine о начале, конце и пропуске опроса устройства превращаются в прогресс цикла,
    // Который окно получает в снимке состояния мониторинга.
    [TestClass]
    public sealed class Sprint47DesktopCycleProgressTests
    {
        private static readonly DateTime FirstPollUtc =
            new DateTime(
                2026, 10, 7, 14, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public async Task EngineTargetMarkersBuildCycleProgressThatSurvivesStop()
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
                    Target("11111111-1111-1111-1111-111111111111", "192.0.2.61");
                var second =
                    Target("22222222-2222-2222-2222-222222222222", "192.0.2.62");
                var third =
                    Target("33333333-3333-3333-3333-333333333333", "192.0.2.63");

                var start =
                    control.StartSetAsync(
                        new[] { first, second, third },
                        Policy(),
                        new MonitoringTargetSetPolicy(
                            2,
                            TimeSpan.FromSeconds(10)),
                        CancellationToken.None);

                var process =
                    factory.LastSession;

                process.EmitOutput(
                    "NETLOOM_SCHEDULE_SET state=started targets=3 intervalSeconds=60 maxConcurrency=2 startupJitterSeconds=10");

                await start;

                var waiting =
                    control.Current.CurrentCycle;

                Assert.IsNotNull(waiting, "A started set reports its cycle before the first poll.");
                Assert.AreEqual(3, waiting.TotalTargets);
                Assert.AreEqual(0, waiting.Done);

                process.EmitOutput(
                    StartedMarker(first));

                var polling =
                    control.Current.CurrentCycle;

                Assert.AreEqual(1, polling.InProgress.Count);
                Assert.AreEqual(first.DeviceId, polling.InProgress[0].DeviceId);
                Assert.AreEqual(IPAddress.Parse("192.0.2.61"), polling.InProgress[0].TargetAddress);

                process.EmitOutput(
                    CompletedMarker(first, FirstPollUtc, true));
                process.EmitOutput(
                    "NETLOOM_TARGET_POLL state=skipped reason=backpressure deviceId=" +
                    second.DeviceId.ToString("D") +
                    " address=192.0.2.62");

                var partial =
                    control.Current.CurrentCycle;

                Assert.AreEqual(1, partial.Succeeded);
                Assert.AreEqual(1, partial.Skipped);
                Assert.AreEqual(1, partial.Remaining);
                Assert.IsNull(control.Current.LastCompletedCycle);

                process.EmitOutput(
                    StartedMarker(third));
                process.EmitOutput(
                    CompletedMarker(third, FirstPollUtc.AddSeconds(4), false));

                var completed =
                    control.Current.LastCompletedCycle;

                Assert.IsNotNull(completed, "The third device closes the cycle.");
                Assert.AreEqual(1, completed.CycleNumber);
                Assert.AreEqual(1, completed.Succeeded);
                Assert.AreEqual(1, completed.Failed);
                Assert.AreEqual(1, completed.Skipped);
                Assert.AreEqual(FirstPollUtc.AddSeconds(4), completed.CompletedUtc);
                Assert.AreEqual(2, control.Current.CurrentCycle.CycleNumber);

                var failedOutcome =
                    control.Current.TargetOutcomes.Single(
                        outcome => outcome.DeviceId == third.DeviceId);

                Assert.IsFalse(failedOutcome.LastAttemptSucceeded);
                Assert.AreEqual(1, failedOutcome.ConsecutiveFailures);

                var stop =
                    control.StopAsync(
                        CancellationToken.None);

                process.Complete(0);

                await stop;

                Assert.AreEqual(MonitoringControlState.Stopped, control.Current.State);
                Assert.AreEqual(
                    1,
                    control.Current.LastCompletedCycle.CycleNumber,
                    "After stop the operator still sees the result of the last cycle.");
            }
        }

        private static MonitoringTarget Target(
            string deviceId,
            string address)
        {
            return new MonitoringTarget(
                Guid.Parse(deviceId),
                IPAddress.Parse(address));
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
                });
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
                completedUtc.ToString("o", CultureInfo.InvariantCulture) +
                " success=" + (anySucceeded ? "1" : "0") +
                " failed=" + (anySucceeded ? "0" : "1") +
                " anySucceeded=" + (anySucceeded ? "true" : "false");
        }

        private sealed class FakeEngineProcessFactory :
            IEngineProcessFactory
        {
            public FakeEngineProcessSession LastSession { get; private set; }

            public IEngineProcessSession Start(
                EngineProcessStartRequest request)
            {
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
            private readonly TaskCompletionSource<int> _completion =
                new TaskCompletionSource<int>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            public event Action<string> OutputLineReceived;

#pragma warning disable CS0067
            public event Action<string> ErrorLineReceived;
#pragma warning restore CS0067

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
                WrittenLines.Add(line);
            }

            public void Terminate()
            {
                _completion.TrySetResult(-1);
            }

            public void EmitOutput(
                string line)
            {
                OutputLineReceived?.Invoke(line);
            }

            public void Complete(
                int exitCode)
            {
                _completion.TrySetResult(exitCode);
            }

            public void Dispose()
            {
            }
        }
    }
}
