using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Topology.Lifecycle;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint12LifecycleTests
    {
        private static readonly DateTime T0 =
            new DateTime(
                2026,
                1,
                1,
                12,
                0,
                0,
                DateTimeKind.Utc);

        private static TopologyLifecyclePolicy Policy()
        {
            return new TopologyLifecyclePolicy(
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(15));
        }

        [TestMethod]
        public void FreshnessTransitionsAreDeterministic()
        {
            var policy = Policy();

            var created =
                policy.CreateDiscovered(
                    "link-a",
                    T0,
                    T0);

            Assert.AreEqual(
                TopologyFreshness.Fresh,
                created.State.Freshness);

            var aging =
                policy.RecordNoEvidence(
                    created.State,
                    T0.AddMinutes(5));

            Assert.AreEqual(
                TopologyFreshness.Aging,
                aging.State.Freshness);

            var stale =
                policy.RecordNoEvidence(
                    aging.State,
                    T0.AddMinutes(15));

            Assert.AreEqual(
                TopologyFreshness.Stale,
                stale.State.Freshness);
        }

        [TestMethod]
        public void PollFailureNeverDeletesTopology()
        {
            var policy = Policy();

            var current =
                policy.CreateDiscovered(
                    "device-a",
                    T0,
                    T0).State;

            var result =
                policy.RecordPollFailure(
                    current,
                    T0.AddHours(24));

            Assert.AreEqual(
                TopologyFreshness.Stale,
                result.State.Freshness);

            Assert.IsFalse(
                result.ShouldDelete);

            Assert.AreEqual(
                "device-a",
                result.State.SubjectKey);

            Assert.AreEqual(
                T0,
                result.State.LastSeenUtc);
        }

        [TestMethod]
        public void MissingEvidenceNeverDeletesLink()
        {
            var policy = Policy();

            var current =
                policy.CreateDiscovered(
                    "link-a",
                    T0,
                    T0).State;

            var result =
                policy.RecordNoEvidence(
                    current,
                    T0.AddDays(30));

            Assert.AreEqual(
                TopologyFreshness.Stale,
                result.State.Freshness);

            Assert.IsFalse(
                result.ShouldDelete);

            Assert.AreEqual(
                T0,
                result.State.FirstSeenUtc);

            Assert.AreEqual(
                T0,
                result.State.LastSeenUtc);
        }

        [TestMethod]
        public void NewEvidenceRefreshesStaleTopology()
        {
            var policy = Policy();

            var current =
                policy.CreateDiscovered(
                    "link-a",
                    T0,
                    T0).State;

            current =
                policy.RecordNoEvidence(
                    current,
                    T0.AddHours(1)).State;

            Assert.AreEqual(
                TopologyFreshness.Stale,
                current.Freshness);

            var observedUtc =
                T0.AddHours(1);

            var refreshed =
                policy.RecordEvidence(
                    current,
                    observedUtc,
                    observedUtc);

            Assert.AreEqual(
                TopologyFreshness.Fresh,
                refreshed.State.Freshness);

            Assert.AreEqual(
                T0,
                refreshed.State.FirstSeenUtc);

            Assert.AreEqual(
                observedUtc,
                refreshed.State.LastSeenUtc);

            Assert.IsFalse(
                refreshed.ShouldDelete);
        }

        [TestMethod]
        public void OlderEvidenceCannotMoveLastSeenBackwards()
        {
            var policy = Policy();

            var current =
                policy.CreateDiscovered(
                    "link-a",
                    T0,
                    T0).State;

            current =
                policy.RecordEvidence(
                    current,
                    T0.AddMinutes(10),
                    T0.AddMinutes(10)).State;

            var result =
                policy.RecordEvidence(
                    current,
                    T0.AddMinutes(3),
                    T0.AddMinutes(10));

            Assert.AreEqual(
                T0.AddMinutes(10),
                result.State.LastSeenUtc);

            Assert.AreEqual(
                TopologyFreshness.Fresh,
                result.State.Freshness);
        }

        [TestMethod]
        public void ManualTopologyDoesNotAgeFromDiscovery()
        {
            var policy = Policy();

            var manual =
                policy.CreateManual(
                    "manual-node-a",
                    T0).State;

            var noEvidence =
                policy.RecordNoEvidence(
                    manual,
                    T0.AddDays(365));

            Assert.AreEqual(
                TopologyLifecycleOrigin.Manual,
                noEvidence.State.Origin);

            Assert.AreEqual(
                TopologyFreshness.Fresh,
                noEvidence.State.Freshness);

            Assert.IsFalse(
                noEvidence.ShouldDelete);

            Assert.AreEqual(
                T0,
                noEvidence.State.LastSeenUtc);
        }

        [TestMethod]
        public void DiscoveryEvidenceDoesNotRewriteManualLifecycle()
        {
            var policy = Policy();

            var manual =
                policy.CreateManual(
                    "manual-link-a",
                    T0).State;

            var result =
                policy.RecordEvidence(
                    manual,
                    T0.AddHours(5),
                    T0.AddHours(5));

            Assert.AreSame(
                manual,
                result.State);

            Assert.AreEqual(
                T0,
                result.State.LastSeenUtc);

            Assert.IsFalse(
                result.ShouldDelete);
        }

        [TestMethod]
        public void NonUtcTimeIsRejected()
        {
            var policy = Policy();

            var local =
                DateTime.SpecifyKind(
                    T0,
                    DateTimeKind.Local);

            try
            {
                policy.CreateDiscovered(
                    "link-a",
                    local,
                    T0);

                Assert.Fail(
                    "ArgumentException was expected.");
            }
            catch (ArgumentException)
            {
            }
        }
    }
}
