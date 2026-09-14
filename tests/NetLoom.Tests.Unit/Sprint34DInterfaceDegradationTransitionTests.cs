using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring.Interfaces;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint34DInterfaceDegradationTransitionTests
    {
        private static readonly Guid DeviceId =
            new Guid(
                "66666666-7777-8888-9999-aaaaaaaaaaaa");

        private static readonly DateTime T0 =
            new DateTime(
                2026, 1, 10, 12, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void TracksFirstUnchangedChangedAndResolved()
        {
            var store =
                new MemoryStateStore();

            var tracker =
                new InterfaceDegradationTransitionTracker(
                    store);

            Assert.AreEqual(
                InterfaceDegradationTransitionKind.Unchanged,
                tracker.Observe(
                    Healthy(
                        T0))
                    .Kind);

            Assert.AreEqual(
                InterfaceDegradationTransitionKind.FirstAppearance,
                tracker.Observe(
                    ErrorDegraded(
                        T0.AddMinutes(1)))
                    .Kind);

            Assert.AreEqual(
                InterfaceDegradationTransitionKind.Unchanged,
                tracker.Observe(
                    ErrorDegraded(
                        T0.AddMinutes(2)))
                    .Kind);

            Assert.AreEqual(
                InterfaceDegradationTransitionKind.Changed,
                tracker.Observe(
                    ErrorAndDiscardDegraded(
                        T0.AddMinutes(3)))
                    .Kind);

            Assert.AreEqual(
                InterfaceDegradationTransitionKind.Resolved,
                tracker.Observe(
                    Healthy(
                        T0.AddMinutes(4)))
                    .Kind);
        }

        [TestMethod]
        public void RestartWithSameDegradedEvidenceIsUnchanged()
        {
            var store =
                new MemoryStateStore();

            Assert.AreEqual(
                InterfaceDegradationTransitionKind.FirstAppearance,
                new InterfaceDegradationTransitionTracker(
                    store)
                    .Observe(
                        ErrorDegraded(
                            T0))
                    .Kind);

            var restarted =
                new InterfaceDegradationTransitionTracker(
                    store)
                    .Observe(
                        ErrorDegraded(
                            T0.AddMinutes(1)));

            Assert.AreEqual(
                InterfaceDegradationTransitionKind.Unchanged,
                restarted.Kind);

            Assert.IsFalse(
                restarted.HasStateChange);
        }

        [TestMethod]
        public void IndeterminateDoesNotResolveOrReplaceDegradedState()
        {
            var store =
                new MemoryStateStore();

            var tracker =
                new InterfaceDegradationTransitionTracker(
                    store);

            tracker.Observe(
                ErrorDegraded(
                    T0));

            var before =
                store.Current;

            var unknown =
                tracker.Observe(
                    NoBaseline(
                        T0.AddMinutes(1)));

            Assert.AreEqual(
                InterfaceDegradationTransitionKind.Indeterminate,
                unknown.Kind);

            Assert.IsFalse(
                unknown.HasStateChange);

            Assert.AreSame(
                before,
                store.Current);

            Assert.AreEqual(
                InterfaceDegradationTransitionKind.Unchanged,
                tracker.Observe(
                    ErrorDegraded(
                        T0.AddMinutes(2)))
                    .Kind);
        }

        [TestMethod]
        public void NoPriorIndeterminateDoesNotCreateDurableState()
        {
            var store =
                new MemoryStateStore();

            var tracker =
                new InterfaceDegradationTransitionTracker(
                    store);

            Assert.AreEqual(
                InterfaceDegradationTransitionKind.Indeterminate,
                tracker.Observe(
                    NoBaseline(
                        T0))
                    .Kind);

            Assert.IsNull(
                store.Current);

            Assert.AreEqual(
                InterfaceDegradationTransitionKind.FirstAppearance,
                tracker.Observe(
                    ErrorDegraded(
                        T0.AddMinutes(1)))
                    .Kind);
        }

        private static InterfaceDegradationClassification Healthy(
            DateTime capturedUtc)
        {
            return Classify(
                capturedUtc,
                0,
                0,
                5.0,
                null);
        }

        private static InterfaceDegradationClassification ErrorDegraded(
            DateTime capturedUtc)
        {
            return Classify(
                capturedUtc,
                3,
                3,
                5.0,
                null);
        }

        private static InterfaceDegradationClassification
            ErrorAndDiscardDegraded(
                DateTime capturedUtc)
        {
            return Classify(
                capturedUtc,
                3,
                3,
                5.0,
                1.0,
                1,
                1);
        }

        private static InterfaceDegradationClassification NoBaseline(
            DateTime capturedUtc)
        {
            var current =
                Snapshot(
                    capturedUtc,
                    10,
                    20,
                    30,
                    40);

            return new InterfaceDegradationClassifier()
                .Classify(
                    new InterfaceCounterEvaluation(
                        current,
                        new InterfaceCounterDeltaEvaluator()
                            .Evaluate(
                                null,
                                current)),
                    new InterfaceDegradationPolicy(
                        5.0,
                        null));
        }

        private static InterfaceDegradationClassification Classify(
            DateTime capturedUtc,
            uint inErrorDelta,
            uint outErrorDelta,
            double? errorThreshold,
            double? discardThreshold,
            uint inDiscardDelta = 0,
            uint outDiscardDelta = 0)
        {
            var previous =
                Snapshot(
                    capturedUtc.AddMinutes(-1),
                    10,
                    20,
                    30,
                    40);

            var current =
                Snapshot(
                    capturedUtc,
                    10 + inErrorDelta,
                    20 + outErrorDelta,
                    30 + inDiscardDelta,
                    40 + outDiscardDelta);

            var evaluation =
                new InterfaceCounterEvaluation(
                    current,
                    new InterfaceCounterDeltaEvaluator()
                        .Evaluate(
                            previous,
                            current));

            return new InterfaceDegradationClassifier()
                .Classify(
                    evaluation,
                    new InterfaceDegradationPolicy(
                        errorThreshold,
                        discardThreshold));
        }

        private static InterfaceMonitoringSnapshot Snapshot(
            DateTime capturedUtc,
            uint inErrors,
            uint outErrors,
            uint inDiscards,
            uint outDiscards)
        {
            return new InterfaceMonitoringSnapshot(
                DeviceId,
                7,
                1,
                1,
                capturedUtc,
                inErrors,
                outErrors,
                inDiscards,
                outDiscards,
                500);
        }

        private sealed class MemoryStateStore :
            IInterfaceDegradationStateStore
        {
            public InterfaceDegradationState
                Current { get; private set; }

            public InterfaceDegradationState Load(
                Guid deviceId,
                int ifIndex)
            {
                if (Current == null ||
                    Current.DeviceId != deviceId ||
                    Current.IfIndex != ifIndex)
                {
                    return null;
                }

                return Current;
            }

            public InterfaceDegradationState ReplaceAndGetPrevious(
                InterfaceDegradationState current)
            {
                var previous =
                    Load(
                        current.DeviceId,
                        current.IfIndex);

                Current =
                    current;

                return previous;
            }
        }
    }
}
