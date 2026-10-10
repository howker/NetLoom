using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.StpTree;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint50FailurePredictionTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
        private static Guid Id(int value) => new Guid("00000000-0000-0000-0000-" + value.ToString("D12"));

        private static PhysicalLinkDiagnostic Link(int id, int a, int b,
            StpTreePortState state = StpTreePortState.Forwarding)
        {
            return new PhysicalLinkDiagnostic(Id(id), Id(a), Id(b), null, null,
                null, null, null, null, DiagnosticLinkStrength.Confirmed,
                MapFreshness.Fresh, null, null, null, Now, Now,
                state, state, new DiagnosticEvidenceItem[0], false, 0, 0, 0);
        }

        private static void Expect(FailurePredictionResult result,
            FailureImpactCategory category, params int[] devices)
        {
            CollectionAssert.AreEqual(devices.Select(Id).OrderBy(id => id).ToArray(),
                result.AffectedDevices.Keys.OrderBy(id => id).ToArray());
            Assert.IsTrue(result.AffectedDevices.Values.All(value => value == category));
        }

        [TestMethod]
        public void ChainExcludesFailedDeviceAndLink()
        {
            var links = new[] { Link(101, 1, 2), Link(102, 2, 3), Link(103, 3, 4) };
            Expect(FailurePrediction.PredictDeviceFailure(links, Id(1), Id(2)),
                FailureImpactCategory.CutOff, 3, 4);
            Expect(FailurePrediction.PredictLinkFailure(links, Id(1), Id(101)),
                FailureImpactCategory.CutOff, 2, 3, 4);
            Assert.AreEqual(0, FailurePrediction.PredictDeviceFailure(links, Id(1), Id(4)).CutOffCount);
            CollectionAssert.AreEqual(new[] { Id(2), Id(3) },
                FailurePrediction.SinglePointsOfFailureDevices(links, Id(1)).ToArray());
            CollectionAssert.AreEqual(new[] { Id(101), Id(102), Id(103) },
                FailurePrediction.SinglePointsOfFailureLinks(links, Id(1)).ToArray());
            var shortChain = new[] { Link(101, 1, 2), Link(102, 2, 3) };
            CollectionAssert.AreEqual(new[] { Id(2) },
                FailurePrediction.SinglePointsOfFailureDevices(shortChain, Id(1)).ToArray());
            CollectionAssert.AreEqual(new[] { Id(101), Id(102) },
                FailurePrediction.SinglePointsOfFailureLinks(shortChain, Id(1)).ToArray());
        }

        [TestMethod]
        public void StandbyRingDoesNotClaimWorkingBypass()
        {
            var links = new[] { Link(101, 1, 2), Link(102, 2, 3),
                Link(103, 3, 4), Link(104, 4, 1, StpTreePortState.Blocking) };
            var result = FailurePrediction.PredictDeviceFailure(links, Id(1), Id(2));
            Expect(result, FailureImpactCategory.StandbyOnly, 3, 4);
            Assert.AreEqual(0, result.CutOffCount);
            Assert.IsFalse(result.IsSinglePointOfFailure);
            Assert.AreEqual(0, FailurePrediction.PredictLinkFailure(links, Id(1), Id(104)).AffectedDevices.Count);
            Assert.AreEqual(0, FailurePrediction.SinglePointsOfFailureDevices(links, Id(1)).Count);
            Assert.AreEqual(0, FailurePrediction.SinglePointsOfFailureLinks(links, Id(1)).Count);
        }

        [TestMethod]
        public void UnavailableAndUnconfirmedEdgesKeepTheirDistinctMeaning()
        {
            var broken = new[] { Link(101, 1, 2), Link(102, 2, 3),
                Link(103, 3, 4, StpTreePortState.Broken), Link(104, 4, 1) };
            Expect(FailurePrediction.PredictDeviceFailure(broken, Id(1), Id(2)),
                FailureImpactCategory.CutOff, 3);

            var unknown = new[] { Link(101, 1, 2, StpTreePortState.Unknown),
                Link(102, 2, 3, StpTreePortState.Unknown),
                Link(103, 3, 4, StpTreePortState.Unknown),
                Link(104, 4, 1, StpTreePortState.Unknown) };
            Assert.AreEqual(0, FailurePrediction.PredictDeviceFailure(unknown, Id(1), Id(2)).AffectedDevices.Count);
            var oneUnknown = new[] { Link(101, 1, 2), Link(102, 2, 3),
                Link(103, 3, 4), Link(104, 4, 1, StpTreePortState.Unknown) };
            Expect(FailurePrediction.PredictDeviceFailure(oneUnknown, Id(1), Id(2)),
                FailureImpactCategory.Unconfirmed, 3, 4);
        }

        [TestMethod]
        public void ParallelCablesPreserveAlternativeAndExcludeFailedCable()
        {
            var working = new[] { Link(101, 1, 2), Link(102, 1, 2) };
            Assert.AreEqual(0, FailurePrediction.PredictLinkFailure(working, Id(1), Id(101)).AffectedDevices.Count);
            var standby = new[] { Link(101, 1, 2), Link(102, 1, 2, StpTreePortState.Blocking),
                Link(103, 2, 3) };
            Expect(FailurePrediction.PredictLinkFailure(standby, Id(1), Id(101)),
                FailureImpactCategory.StandbyOnly, 2, 3);
            Expect(FailurePrediction.PredictLinkFailure(new[] { Link(101, 1, 2) }, Id(1), Id(101)),
                FailureImpactCategory.CutOff, 2);
        }

        [TestMethod]
        public void UnknownPollingPointAndFailedPollingPointHaveNoDirection()
        {
            var links = new[] { Link(101, 1, 2) };
            var unknown = FailurePrediction.PredictLinkFailure(links, null, Id(101));
            Assert.IsFalse(unknown.IsDirectional);
            Assert.AreEqual(FailurePredictionReason.PollingPointUnknown, unknown.Reason);
            Assert.AreEqual(0, unknown.AffectedDevices.Count);
            var failedOrigin = FailurePrediction.PredictDeviceFailure(links, Id(1), Id(1));
            Assert.IsFalse(failedOrigin.IsDirectional);
            Assert.AreEqual(FailurePredictionReason.TargetIsPollingPoint, failedOrigin.Reason);
        }
    }
}
