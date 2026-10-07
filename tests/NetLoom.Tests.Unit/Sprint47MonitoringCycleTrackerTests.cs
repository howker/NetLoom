using System;
using System.Linq;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Alerts;
using NetLoom.Application.MonitoringControl;
using NetLoom.Contracts.Alerts;

namespace NetLoom.Tests.Unit
{
    // Sprint 47: цикл опроса собирается из событий отдельных устройств набора.
    [TestClass]
    public sealed class Sprint47MonitoringCycleTrackerTests
    {
        private static readonly DateTime T0 =
            new DateTime(
                2026, 10, 7, 10, 0, 0,
                DateTimeKind.Utc);

        private static readonly MonitoringTarget A =
            Target(1);

        private static readonly MonitoringTarget B =
            Target(2);

        private static readonly MonitoringTarget C =
            Target(3);

        [TestMethod]
        public void CycleCountsProgressAndCompletesWhenEveryTargetWasPolled()
        {
            var tracker =
                new MonitoringCycleTracker(
                    new[] { A, B, C });

            var idle =
                tracker.Current;

            Assert.AreEqual(1, idle.CycleNumber);
            Assert.AreEqual(3, idle.TotalTargets);
            Assert.AreEqual(0, idle.Done);
            Assert.IsNull(idle.StartedUtc, "A cycle has not started before the first poll.");

            tracker.TargetStarted(A.DeviceId, T0);
            tracker.TargetStarted(B.DeviceId, T0.AddSeconds(1));

            var running =
                tracker.Current;

            Assert.AreEqual(T0, running.StartedUtc);
            CollectionAssert.AreEqual(
                new[] { A.DeviceId, B.DeviceId },
                running.InProgress.Select(target => target.DeviceId).ToArray(),
                "Devices being polled right now, oldest first.");

            Assert.IsFalse(tracker.TargetCompleted(A.DeviceId, T0.AddSeconds(2), true));
            Assert.IsFalse(tracker.TargetCompleted(B.DeviceId, T0.AddSeconds(3), false));

            var partial =
                tracker.Current;

            Assert.AreEqual(1, partial.Succeeded);
            Assert.AreEqual(1, partial.Failed);
            Assert.AreEqual(2, partial.Done);
            Assert.AreEqual(1, partial.Remaining);
            Assert.AreEqual(0, partial.InProgress.Count);
            Assert.IsFalse(partial.IsCompleted);

            tracker.TargetStarted(C.DeviceId, T0.AddSeconds(4));

            Assert.IsTrue(
                tracker.TargetCompleted(C.DeviceId, T0.AddSeconds(5), true),
                "The last device of the set completes the cycle.");

            var completed =
                tracker.LastCompleted;

            Assert.AreEqual(1, completed.CycleNumber);
            Assert.AreEqual(2, completed.Succeeded);
            Assert.AreEqual(1, completed.Failed);
            Assert.AreEqual(0, completed.Remaining);
            Assert.AreEqual(T0, completed.StartedUtc);
            Assert.AreEqual(T0.AddSeconds(5), completed.CompletedUtc);

            var next =
                tracker.Current;

            Assert.AreEqual(2, next.CycleNumber);
            Assert.AreEqual(0, next.Done);
            Assert.IsNull(next.StartedUtc);
        }

        [TestMethod]
        public void TargetThatRunsAheadCountsInTheNextCycleOnly()
        {
            var tracker =
                new MonitoringCycleTracker(
                    new[] { A, B });

            tracker.TargetStarted(A.DeviceId, T0);
            tracker.TargetCompleted(A.DeviceId, T0.AddSeconds(1), true);

            // A уже на второй попытке, а B ещё не ответил на первую.
            tracker.TargetStarted(A.DeviceId, T0.AddSeconds(10));
            tracker.TargetCompleted(A.DeviceId, T0.AddSeconds(11), false);

            var first =
                tracker.Current;

            Assert.AreEqual(1, first.CycleNumber);
            Assert.AreEqual(1, first.Succeeded, "The second attempt of A must not leak into cycle 1.");
            Assert.AreEqual(0, first.Failed);

            tracker.TargetStarted(B.DeviceId, T0.AddSeconds(12));
            Assert.IsTrue(tracker.TargetCompleted(B.DeviceId, T0.AddSeconds(13), true));

            Assert.AreEqual(2, tracker.LastCompleted.Succeeded);

            var second =
                tracker.Current;

            Assert.AreEqual(2, second.CycleNumber);
            Assert.AreEqual(1, second.Failed, "A already finished its second attempt.");
            Assert.AreEqual(1, second.Remaining);
            Assert.AreEqual(T0.AddSeconds(10), second.StartedUtc);
        }

        [TestMethod]
        public void SkippedPollCompletesTheAttemptButDoesNotCountAsFailure()
        {
            var tracker =
                new MonitoringCycleTracker(
                    new[] { A, B });

            tracker.TargetCompleted(A.DeviceId, T0, false);
            tracker.TargetCompleted(A.DeviceId, T0.AddSeconds(1), false);
            tracker.TargetSkipped(A.DeviceId, T0.AddSeconds(2));
            tracker.TargetSkipped(B.DeviceId, T0.AddSeconds(2));

            var outcomeA =
                tracker.Outcomes.Single(item => item.DeviceId == A.DeviceId);

            Assert.IsTrue(outcomeA.LastAttemptSkipped);
            Assert.AreEqual(2, outcomeA.ConsecutiveFailures, "A skipped poll neither breaks nor extends the failure streak.");
            Assert.IsNull(outcomeA.LastSuccessUtc);

            Assert.AreEqual(1, tracker.LastCompleted.Failed);
            Assert.AreEqual(1, tracker.LastCompleted.Skipped);

            tracker.TargetCompleted(A.DeviceId, T0.AddSeconds(3), true);

            outcomeA =
                tracker.Outcomes.Single(item => item.DeviceId == A.DeviceId);

            Assert.AreEqual(0, outcomeA.ConsecutiveFailures);
            Assert.AreEqual(T0.AddSeconds(3), outcomeA.LastSuccessUtc);
        }

        [TestMethod]
        public void UnknownDeviceAndLocalTimeAreRejectedWithoutChangingProgress()
        {
            var tracker =
                new MonitoringCycleTracker(
                    new[] { A });

            Assert.IsFalse(tracker.TargetCompleted(B.DeviceId, T0, true));
            Assert.AreEqual(0, tracker.Current.Done);
            Assert.AreEqual(0, tracker.Outcomes.Count);

            Assert.Throws<ArgumentException>(
                () => tracker.TargetStarted(A.DeviceId, new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Local)));

            Assert.Throws<ArgumentException>(
                () => new MonitoringCycleTracker(new[] { A, A }));
        }

        [TestMethod]
        public void NewSessionContinuesTheFailureStreakWithoutCountingItInTheCycle()
        {
            var first =
                new MonitoringCycleTracker(
                    new[] { A, B });

            first.TargetCompleted(A.DeviceId, T0, false);
            first.TargetCompleted(B.DeviceId, T0, true);

            var second =
                new MonitoringCycleTracker(
                    new[] { A, C },
                    first.Outcomes);

            Assert.AreEqual(0, second.Current.Done, "Carried outcomes do not count in the new cycle.");

            second.TargetCompleted(A.DeviceId, T0.AddMinutes(5), false);

            var outcome =
                second.Outcomes.Single(item => item.DeviceId == A.DeviceId);

            Assert.AreEqual(2, outcome.ConsecutiveFailures, "Restarting monitoring must not reset the streak.");
            Assert.IsFalse(
                second.Outcomes.Any(item => item.DeviceId == B.DeviceId),
                "Devices outside the new set are not carried over.");
        }

        [TestMethod]
        public void TwoPollsWithoutAnswerRaiseOneUnreachableWarningMergedWithTopologyAlerts()
        {
            var tracker =
                new MonitoringCycleTracker(
                    new[] { A, B });

            tracker.TargetCompleted(A.DeviceId, T0, false);
            tracker.TargetCompleted(B.DeviceId, T0, false);
            tracker.TargetCompleted(B.DeviceId, T0.AddMinutes(1), false);

            var alerts =
                MonitoringAlertProjection.BuildUnreachableAlerts(
                    "0",
                    tracker.Outcomes);

            Assert.AreEqual(1, alerts.Count, "One missed answer is not an alert; two in a row are.");
            Assert.AreEqual(TopologyAlertKind.DeviceUnreachable, alerts[0].Kind);
            Assert.AreEqual(TopologyAlertSeverity.Warning, alerts[0].Severity);
            CollectionAssert.AreEqual(new[] { B.DeviceId }, alerts[0].DeviceIds.ToArray());
            Assert.AreEqual(0, alerts[0].PhysicalLinkIds.Count);

            var ring =
                new TopologyAlert(
                    "ring",
                    TopologyAlertKind.RingProtectionDegraded,
                    TopologyAlertSeverity.Warning,
                    "0",
                    new[] { "region" },
                    new[] { Guid.NewGuid() },
                    new[] { TopologyAlertReason.DisabledRingLink });

            var topology =
                new TopologyAlertSnapshot(
                    T0,
                    "0",
                    new[] { ring });

            var merged =
                MonitoringAlertProjection.Merge(
                    topology,
                    tracker.Outcomes);

            Assert.AreEqual(2, merged.Alerts.Count);
            Assert.AreEqual("0", merged.InstanceId);
            Assert.AreSame(
                topology,
                MonitoringAlertProjection.Merge(
                    topology,
                    new MonitoringTargetOutcome[0]),
                "Without unreachable devices the topology snapshot is used as is.");

            // Прежние виды по-прежнему обязаны ссылаться на связь; новый — ровно на одно устройство.
            Assert.Throws<ArgumentException>(
                () => new TopologyAlert(
                    "ring-without-link",
                    TopologyAlertKind.RingProtectionDegraded,
                    TopologyAlertSeverity.Warning,
                    "0",
                    new[] { "region" },
                    new Guid[0],
                    new[] { TopologyAlertReason.DisabledRingLink }));

            Assert.Throws<ArgumentException>(
                () => new TopologyAlert(
                    "unreachable-without-device",
                    TopologyAlertKind.DeviceUnreachable,
                    TopologyAlertSeverity.Warning,
                    "0",
                    new string[0],
                    new Guid[0],
                    new[] { TopologyAlertReason.NoPollResponse }));
        }

        private static MonitoringTarget Target(
            int index)
        {
            return new MonitoringTarget(
                new Guid(index, 0, 0, new byte[8]),
                IPAddress.Parse("198.51.100." + index));
        }
    }
}
