using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.TopologyMap;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class
        TopologyRefreshStateTrackerTests
    {
        private static readonly DateTime T1 =
            new DateTime(
                2026,
                9,
                11,
                10,
                0,
                0,
                DateTimeKind.Utc);

        private static readonly DateTime T2 =
            T1.AddMinutes(5);

        [TestMethod]
        public void
            FirstFailureHasNoLastKnownGoodSnapshot()
        {
            var tracker =
                new TopologyRefreshStateTracker();

            var state =
                tracker.ObserveFailure();

            Assert.AreEqual(
                TopologyRefreshStateKind.InitialFailure,
                state.Kind);

            Assert.IsNull(
                state.Snapshot);

            Assert.IsFalse(
                state.LastSuccessUtc.HasValue);

            Assert.AreEqual(
                TopologyRefreshStateKind.InitialFailure,
                tracker.Current.Kind);
        }

        [TestMethod]
        public void
            FailureAfterSuccessKeepsLastKnownGoodSnapshot()
        {
            var tracker =
                new TopologyRefreshStateTracker();

            var snapshot =
                Snapshot(
                    T1);

            tracker.ObserveSuccess(
                snapshot,
                T1);

            var stale =
                tracker.ObserveFailure();

            Assert.AreEqual(
                TopologyRefreshStateKind.Stale,
                stale.Kind);

            Assert.AreSame(
                snapshot,
                stale.Snapshot);

            Assert.AreEqual(
                T1,
                stale.LastSuccessUtc.Value);

            Assert.AreEqual(
                TopologyRefreshStateKind.Stale,
                tracker.Current.Kind);

            Assert.AreSame(
                snapshot,
                tracker.Current.Snapshot);
        }

        [TestMethod]
        public void
            SuccessAfterFailureClearsStaleState()
        {
            var tracker =
                new TopologyRefreshStateTracker();

            var first =
                Snapshot(
                    T1);

            tracker.ObserveSuccess(
                first,
                T1);

            tracker.ObserveFailure();

            var second =
                Snapshot(
                    T2);

            var current =
                tracker.ObserveSuccess(
                    second,
                    T2);

            Assert.AreEqual(
                TopologyRefreshStateKind.Current,
                current.Kind);

            Assert.AreSame(
                second,
                current.Snapshot);

            Assert.AreEqual(
                T2,
                current.LastSuccessUtc.Value);

            Assert.AreEqual(
                TopologyRefreshStateKind.Current,
                tracker.Current.Kind);
        }

        [TestMethod]
        public void
            HealthyEmptySuccessIsNotInitialFailure()
        {
            var tracker =
                new TopologyRefreshStateTracker();

            var state =
                tracker.ObserveSuccess(
                    Snapshot(
                        T1),
                    T1);

            Assert.AreEqual(
                TopologyRefreshStateKind.Current,
                state.Kind);

            Assert.IsNotNull(
                state.Snapshot);

            Assert.AreEqual(
                0,
                state.Snapshot.MapSnapshot.Nodes.Count);

            Assert.AreEqual(
                0,
                state.Snapshot.AlertSnapshot.Alerts.Count);
        }

        private static TopologyRefreshSnapshot
            Snapshot(
                DateTime capturedUtc)
        {
            return new TopologyRefreshSnapshot(
                new MapSnapshot(
                    capturedUtc,
                    new MapNode[0],
                    new MapLink[0]),
                new TopologyAlertSnapshot(
                    capturedUtc,
                    "cist",
                    new TopologyAlert[0]));
        }
    }
}
