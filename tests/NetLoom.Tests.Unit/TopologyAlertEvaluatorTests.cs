using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.GraphSafety;
using NetLoom.Contracts.Rings;
using NetLoom.Topology.Alerts;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class TopologyAlertEvaluatorTests
    {
        private static readonly DateTime Now =
            new DateTime(
                2026,
                9,
                8,
                12,
                0,
                0,
                DateTimeKind.Utc);

        [TestMethod]
        public void ConfirmedCycleCreatesOneCriticalAlertAndRelatesUnprotectedRing()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();

            var result =
                Evaluate(
                    new[]
                    {
                        a,
                        b,
                        c
                    },
                    new Guid[0],
                    Ring(
                        "ring-a",
                        RingProtectionStatus.Unprotected,
                        new[]
                        {
                            a,
                            b,
                            c
                        },
                        new Guid[0],
                        new Guid[0],
                        new Guid[0]));

            Assert.AreEqual(
                1,
                result.Alerts.Count);

            var alert =
                result.Alerts[0];

            Assert.AreEqual(
                TopologyAlertKind.ForwardingCycle,
                alert.Kind);

            Assert.AreEqual(
                TopologyAlertSeverity.Critical,
                alert.Severity);

            Assert.IsTrue(
                result.HasCritical);

            CollectionAssert.AreEqual(
                new[]
                {
                    "ring-a"
                },
                alert.RelatedRegionKeys
                    .ToArray());

            CollectionAssert.AreEqual(
                new[]
                {
                    a,
                    b,
                    c
                }.OrderBy(id => id).ToArray(),
                alert.PhysicalLinkIds
                    .ToArray());

            CollectionAssert.AreEqual(
                new[]
                {
                    TopologyAlertReason
                        .ConfirmedForwardingCycle
                },
                alert.Reasons.ToArray());
        }

        [TestMethod]
        public void ConfirmedCycleStillAlertsWhenOtherLinksAreUnresolved()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var unresolved =
                Guid.NewGuid();

            var result =
                Evaluate(
                    new[]
                    {
                        a,
                        b
                    },
                    new[]
                    {
                        unresolved
                    });

            Assert.AreEqual(
                1,
                result.Alerts.Count);

            Assert.AreEqual(
                TopologyAlertSeverity.Critical,
                result.Alerts[0].Severity);
        }

        [TestMethod]
        public void DisabledRingLinkCreatesWarning()
        {
            var disabled =
                Guid.NewGuid();

            var result =
                Evaluate(
                    new Guid[0],
                    new Guid[0],
                    Ring(
                        "ring-disabled",
                        RingProtectionStatus.Degraded,
                        new Guid[0],
                        new Guid[0],
                        new[]
                        {
                            disabled
                        },
                        new Guid[0]));

            Assert.AreEqual(
                1,
                result.Alerts.Count);

            var alert =
                result.Alerts[0];

            Assert.AreEqual(
                TopologyAlertKind.RingProtectionDegraded,
                alert.Kind);

            Assert.AreEqual(
                TopologyAlertSeverity.Warning,
                alert.Severity);

            CollectionAssert.AreEqual(
                new[]
                {
                    TopologyAlertReason.DisabledRingLink
                },
                alert.Reasons.ToArray());

            CollectionAssert.AreEqual(
                new[]
                {
                    disabled
                },
                alert.PhysicalLinkIds.ToArray());
        }

        [TestMethod]
        public void MultipleBlockingRingLinksCreateWarning()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();

            var result =
                Evaluate(
                    new Guid[0],
                    new Guid[0],
                    Ring(
                        "ring-blocked",
                        RingProtectionStatus.Degraded,
                        new Guid[0],
                        new[]
                        {
                            b,
                            a
                        },
                        new Guid[0],
                        new Guid[0]));

            Assert.AreEqual(
                1,
                result.Alerts.Count);

            CollectionAssert.AreEqual(
                new[]
                {
                    TopologyAlertReason
                        .MultipleBlockingRingLinks
                },
                result.Alerts[0]
                    .Reasons
                    .ToArray());

            CollectionAssert.AreEqual(
                new[]
                {
                    a,
                    b
                }.OrderBy(id => id).ToArray(),
                result.Alerts[0]
                    .PhysicalLinkIds
                    .ToArray());
        }

        [TestMethod]
        public void ProtectedUnresolvedAndNotApplicableDoNotAlert()
        {
            var result =
                Evaluate(
                    new Guid[0],
                    new[]
                    {
                        Guid.NewGuid()
                    },
                    Ring(
                        "protected",
                        RingProtectionStatus.Protected,
                        new[]
                        {
                            Guid.NewGuid()
                        },
                        new[]
                        {
                            Guid.NewGuid()
                        },
                        new Guid[0],
                        new Guid[0]),
                    Ring(
                        "unresolved",
                        RingProtectionStatus.Unresolved,
                        new Guid[0],
                        new Guid[0],
                        new Guid[0],
                        new[]
                        {
                            Guid.NewGuid()
                        }),
                    Ring(
                        "not-applicable",
                        RingProtectionStatus.NotApplicable,
                        new Guid[0],
                        new Guid[0],
                        new Guid[0],
                        new Guid[0]));

            Assert.AreEqual(
                0,
                result.Alerts.Count);

            Assert.IsFalse(
                result.HasCritical);

            Assert.IsFalse(
                result.HasWarning);
        }

        [TestMethod]
        public void UnprotectedRingRequiresIndependentForwardingCycleConfirmation()
        {
            var ringLink =
                Guid.NewGuid();

            try
            {
                Evaluate(
                    new Guid[0],
                    new Guid[0],
                    Ring(
                        "ring-mismatch",
                        RingProtectionStatus.Unprotected,
                        new[]
                        {
                            ringLink
                        },
                        new Guid[0],
                        new Guid[0],
                        new Guid[0]));

                Assert.Fail(
                    "Expected cross-analysis mismatch rejection.");
            }
            catch (ArgumentException)
            {
            }
        }

        [TestMethod]
        public void WrongInstanceIsRejected()
        {
            var ring =
                new RingProtectionAnalysis(
                    "ring-mst",
                    "mst-1",
                    RingProtectionStatus.Protected,
                    new[]
                    {
                        Guid.NewGuid(),
                        Guid.NewGuid()
                    },
                    new[]
                    {
                        Guid.NewGuid()
                    },
                    new Guid[0],
                    new Guid[0]);

            try
            {
                Evaluate(
                    new Guid[0],
                    new Guid[0],
                    ring);

                Assert.Fail(
                    "Expected STP instance mismatch rejection.");
            }
            catch (ArgumentException)
            {
            }
        }

        [TestMethod]
        public void DuplicateRegionAnalysisIsRejected()
        {
            var first =
                Ring(
                    "same-region",
                    RingProtectionStatus.Protected,
                    new[]
                    {
                        Guid.NewGuid(),
                        Guid.NewGuid()
                    },
                    new[]
                    {
                        Guid.NewGuid()
                    },
                    new Guid[0],
                    new Guid[0]);

            var second =
                Ring(
                    "same-region",
                    RingProtectionStatus.Unresolved,
                    new Guid[0],
                    new Guid[0],
                    new Guid[0],
                    new[]
                    {
                        Guid.NewGuid()
                    });

            try
            {
                Evaluate(
                    new Guid[0],
                    new Guid[0],
                    first,
                    second);

                Assert.Fail(
                    "Expected duplicate region rejection.");
            }
            catch (ArgumentException)
            {
            }
        }

        [TestMethod]
        public void AlertKeysAndOrderingAreDeterministic()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var disabled =
                Guid.NewGuid();

            var ring =
                Ring(
                    "ring-deterministic",
                    RingProtectionStatus.Degraded,
                    new Guid[0],
                    new Guid[0],
                    new[]
                    {
                        disabled
                    },
                    new Guid[0]);

            var first =
                Evaluate(
                    new[]
                    {
                        b,
                        a
                    },
                    new Guid[0],
                    ring);

            var second =
                Evaluate(
                    new[]
                    {
                        a,
                        b
                    },
                    new Guid[0],
                    ring);

            Assert.AreEqual(
                2,
                first.Alerts.Count);

            Assert.AreEqual(
                TopologyAlertSeverity.Critical,
                first.Alerts[0].Severity);

            Assert.AreEqual(
                TopologyAlertSeverity.Warning,
                first.Alerts[1].Severity);

            CollectionAssert.AreEqual(
                first.Alerts
                    .Select(
                        alert =>
                            alert.AlertKey)
                    .ToArray(),
                second.Alerts
                    .Select(
                        alert =>
                            alert.AlertKey)
                    .ToArray());
        }

        [TestMethod]
        public void DegradedStatusWithoutDegradationEvidenceIsRejected()
        {
            try
            {
                Evaluate(
                    new Guid[0],
                    new Guid[0],
                    Ring(
                        "invalid-degraded",
                        RingProtectionStatus.Degraded,
                        new[]
                        {
                            Guid.NewGuid()
                        },
                        new Guid[0],
                        new Guid[0],
                        new Guid[0]));

                Assert.Fail(
                    "Expected malformed degraded analysis rejection.");
            }
            catch (ArgumentException)
            {
            }
        }

        private static TopologyAlertSnapshot Evaluate(
            Guid[] cycleIds,
            Guid[] unresolvedIds,
            params RingProtectionAnalysis[] rings)
        {
            return new TopologyAlertEvaluator()
                .Evaluate(
                    Now,
                    new ForwardingCycleAnalysis(
                        "cist",
                        cycleIds,
                        unresolvedIds),
                    rings);
        }

        private static RingProtectionAnalysis Ring(
            string regionKey,
            RingProtectionStatus status,
            Guid[] forwarding,
            Guid[] blocking,
            Guid[] disabled,
            Guid[] unresolved)
        {
            return new RingProtectionAnalysis(
                regionKey,
                "cist",
                status,
                forwarding,
                blocking,
                disabled,
                unresolved);
        }
    }
}