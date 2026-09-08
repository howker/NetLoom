using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Alerts;
using NetLoom.Contracts.Alerts;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class
        TopologyAlertTransitionTrackerTests
    {
        private static readonly DateTime Now =
            new DateTime(
                2026,
                9,
                8,
                15,
                30,
                0,
                DateTimeKind.Utc);

        [TestMethod]
        public void
            TracksFirstUnchangedChangedResolvedAndRestartSemantics()
        {
            var tracker =
                new TopologyAlertTransitionTracker();

            Assert.AreEqual(
                TopologyAlertTransitionKind.Unchanged,
                tracker.Observe(
                    Snapshot(
                        "cist")));

            Assert.AreEqual(
                TopologyAlertTransitionKind.FirstAppearance,
                tracker.Observe(
                    Snapshot(
                        "cist",
                        "alert-a")));

            Assert.AreEqual(
                TopologyAlertTransitionKind.Unchanged,
                tracker.Observe(
                    Snapshot(
                        "cist",
                        "alert-a")));

            Assert.AreEqual(
                TopologyAlertTransitionKind.Changed,
                tracker.Observe(
                    Snapshot(
                        "cist",
                        "alert-a",
                        "alert-b")));

            Assert.AreEqual(
                TopologyAlertTransitionKind.Resolved,
                tracker.Observe(
                    Snapshot(
                        "cist")));

            var restarted =
                new TopologyAlertTransitionTracker();

            Assert.AreEqual(
                TopologyAlertTransitionKind.FirstAppearance,
                restarted.Observe(
                    Snapshot(
                        "cist",
                        "alert-a")));
        }

        [TestMethod]
        public void
            SuppressionStateIsIndependentPerExplicitInstanceId()
        {
            var tracker =
                new TopologyAlertTransitionTracker();

            Assert.AreEqual(
                TopologyAlertTransitionKind.FirstAppearance,
                tracker.Observe(
                    Snapshot(
                        "cist",
                        "alert-a")));

            Assert.AreEqual(
                TopologyAlertTransitionKind.FirstAppearance,
                tracker.Observe(
                    Snapshot(
                        "mst-1",
                        "alert-b")));

            Assert.AreEqual(
                TopologyAlertTransitionKind.Unchanged,
                tracker.Observe(
                    Snapshot(
                        "cist",
                        "alert-a")));
        }

        private static TopologyAlertSnapshot Snapshot(
            string instanceId,
            params string[] alertKeys)
        {
            var alerts =
                new TopologyAlert[
                    alertKeys.Length];

            for (var index = 0;
                index < alertKeys.Length;
                index++)
            {
                alerts[index] =
                    new TopologyAlert(
                        alertKeys[index],
                        TopologyAlertKind
                            .ForwardingCycle,
                        TopologyAlertSeverity
                            .Critical,
                        instanceId,
                        new string[0],
                        new[]
                        {
                            Guid.NewGuid()
                        },
                        new[]
                        {
                            TopologyAlertReason
                                .ConfirmedForwardingCycle
                        });
            }

            return new TopologyAlertSnapshot(
                Now,
                instanceId,
                alerts);
        }
    }
}
