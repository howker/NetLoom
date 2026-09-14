using System;
using System.Data.SQLite;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring.Interfaces;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Monitoring;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint34DInterfaceDegradationStateStoreTests
    {
        private static readonly Guid DeviceId =
            new Guid(
                "77777777-8888-9999-aaaa-bbbbbbbbbbbb");

        private static readonly DateTime T0 =
            new DateTime(
                2026, 1, 11, 12, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void RestartSuppressesUnchangedDegradedState()
        {
            WithDatabase(
                factory =>
                {
                    var first =
                        new InterfaceDegradationTransitionTracker(
                            new SqliteInterfaceDegradationStateStore(
                                factory))
                            .Observe(
                                ErrorDegraded(
                                    T0));

                    Assert.AreEqual(
                        InterfaceDegradationTransitionKind.FirstAppearance,
                        first.Kind);

                    var restarted =
                        new InterfaceDegradationTransitionTracker(
                            new SqliteInterfaceDegradationStateStore(
                                factory))
                            .Observe(
                                ErrorDegraded(
                                    T0.AddMinutes(1)));

                    Assert.AreEqual(
                        InterfaceDegradationTransitionKind.Unchanged,
                        restarted.Kind);

                    Assert.IsFalse(
                        restarted.HasStateChange);
                });
        }

        [TestMethod]
        public void ChangedEvidenceAcrossRestartIsChanged()
        {
            WithDatabase(
                factory =>
                {
                    new InterfaceDegradationTransitionTracker(
                        new SqliteInterfaceDegradationStateStore(
                            factory))
                        .Observe(
                            ErrorDegraded(
                                T0));

                    var changed =
                        new InterfaceDegradationTransitionTracker(
                            new SqliteInterfaceDegradationStateStore(
                                factory))
                            .Observe(
                                ErrorAndDiscardDegraded(
                                    T0.AddMinutes(1)));

                    Assert.AreEqual(
                        InterfaceDegradationTransitionKind.Changed,
                        changed.Kind);

                    Assert.IsTrue(
                        changed.HasStateChange);
                });
        }

        [TestMethod]
        public void IndeterminateDoesNotReplacePersistedDegradedState()
        {
            WithDatabase(
                factory =>
                {
                    var firstStore =
                        new SqliteInterfaceDegradationStateStore(
                            factory);

                    new InterfaceDegradationTransitionTracker(
                        firstStore)
                        .Observe(
                            ErrorDegraded(
                                T0));

                    var unknown =
                        new InterfaceDegradationTransitionTracker(
                            new SqliteInterfaceDegradationStateStore(
                                factory))
                            .Observe(
                                NoBaseline(
                                    T0.AddMinutes(1)));

                    Assert.AreEqual(
                        InterfaceDegradationTransitionKind.Indeterminate,
                        unknown.Kind);

                    var persisted =
                        new SqliteInterfaceDegradationStateStore(
                            factory)
                            .Load(
                                DeviceId,
                                7);

                    Assert.AreEqual(
                        T0,
                        persisted.CapturedUtc);

                    Assert.AreEqual(
                        InterfaceDegradationStatus.Degraded,
                        persisted.Status);

                    var repeated =
                        new InterfaceDegradationTransitionTracker(
                            new SqliteInterfaceDegradationStateStore(
                                factory))
                            .Observe(
                                ErrorDegraded(
                                    T0.AddMinutes(2)));

                    Assert.AreEqual(
                        InterfaceDegradationTransitionKind.Unchanged,
                        repeated.Kind);
                });
        }

        [TestMethod]
        public void HealthyAfterRestartResolvesAndThenSuppressesRepeat()
        {
            WithDatabase(
                factory =>
                {
                    new InterfaceDegradationTransitionTracker(
                        new SqliteInterfaceDegradationStateStore(
                            factory))
                        .Observe(
                            ErrorDegraded(
                                T0));

                    var resolved =
                        new InterfaceDegradationTransitionTracker(
                            new SqliteInterfaceDegradationStateStore(
                                factory))
                            .Observe(
                                Healthy(
                                    T0.AddMinutes(1)));

                    Assert.AreEqual(
                        InterfaceDegradationTransitionKind.Resolved,
                        resolved.Kind);

                    var repeatedHealthy =
                        new InterfaceDegradationTransitionTracker(
                            new SqliteInterfaceDegradationStateStore(
                                factory))
                            .Observe(
                                Healthy(
                                    T0.AddMinutes(2)));

                    Assert.AreEqual(
                        InterfaceDegradationTransitionKind.Unchanged,
                        repeatedHealthy.Kind);
                });
        }

        [TestMethod]
        public void StaleDeterminateStateIsRejectedWithoutReplacement()
        {
            WithDatabase(
                factory =>
                {
                    var store =
                        new SqliteInterfaceDegradationStateStore(
                            factory);

                    store.ReplaceAndGetPrevious(
                        new InterfaceDegradationState(
                            DeviceId,
                            7,
                            T0.AddMinutes(1),
                            InterfaceDegradationStatus.Degraded,
                            "4"));

                    try
                    {
                        store.ReplaceAndGetPrevious(
                            new InterfaceDegradationState(
                                DeviceId,
                                7,
                                T0,
                                InterfaceDegradationStatus.Healthy,
                                string.Empty));

                        Assert.Fail(
                            "Stale interface degradation state must be rejected.");
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    var persisted =
                        new SqliteInterfaceDegradationStateStore(
                            factory)
                            .Load(
                                DeviceId,
                                7);

                    Assert.AreEqual(
                        T0.AddMinutes(1),
                        persisted.CapturedUtc);

                    Assert.AreEqual(
                        InterfaceDegradationStatus.Degraded,
                        persisted.Status);
                });
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

            return new InterfaceDegradationClassifier()
                .Classify(
                    new InterfaceCounterEvaluation(
                        current,
                        new InterfaceCounterDeltaEvaluator()
                            .Evaluate(
                                previous,
                                current)),
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

        private static void WithDatabase(
            Action<SqliteConnectionFactory> action)
        {
            var tempDirectory =
                Path.Combine(
                    Path.GetTempPath(),
                    "NetLoom.Tests",
                    Guid.NewGuid().ToString("N"));

            var databasePath =
                Path.Combine(
                    tempDirectory,
                    "interface-degradation-state.db");

            try
            {
                var factory =
                    new SqliteConnectionFactory(
                        databasePath);

                new DatabaseInitializer(
                    factory)
                    .Initialize();

                action(
                    factory);
            }
            finally
            {
                SQLiteConnection.ClearAllPools();

                if (Directory.Exists(
                    tempDirectory))
                {
                    Directory.Delete(
                        tempDirectory,
                        recursive: true);
                }
            }
        }
    }
}
