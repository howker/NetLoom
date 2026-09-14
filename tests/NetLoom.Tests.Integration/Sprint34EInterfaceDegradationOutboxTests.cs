using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring.Interfaces;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Monitoring;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint34EInterfaceDegradationOutboxTests
    {
        private static readonly Guid DeviceId =
            new Guid(
                "99999999-aaaa-bbbb-cccc-dddddddddddd");

        private static readonly DateTime T0 =
            new DateTime(
                2026, 1, 13, 12, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void FirstAppearancePersistsEventAndRestartRepeatDoesNotDuplicate()
        {
            WithDatabase(
                factory =>
                {
                    var first =
                        new SqliteInterfaceDegradationTransitionProcessor(
                            factory)
                            .Observe(
                                ErrorDegraded(
                                    T0));

                    Assert.AreEqual(
                        InterfaceDegradationTransitionKind.FirstAppearance,
                        first.Kind);

                    var outbox =
                        new SqliteInterfaceDegradationEventOutbox(
                            factory);

                    var pending =
                        outbox.ReadPending(
                            10);

                    Assert.AreEqual(
                        1,
                        pending.Count);

                    Assert.AreEqual(
                        DeviceId,
                        pending[0].DeviceId);

                    Assert.AreEqual(
                        7,
                        pending[0].IfIndex);

                    Assert.AreEqual(
                        T0,
                        pending[0].CapturedUtc);

                    Assert.AreEqual(
                        InterfaceDegradationTransitionKind.FirstAppearance,
                        pending[0].TransitionKind);

                    Assert.IsFalse(
                        pending[0].PreviousStatus.HasValue);

                    Assert.AreEqual(
                        InterfaceDegradationStatus.Degraded,
                        pending[0].CurrentStatus);

                    Assert.IsTrue(
                        pending[0].Reasons.Contains(
                            InterfaceDegradationReason
                                .ErrorRateThresholdExceeded));

                    var restarted =
                        new SqliteInterfaceDegradationTransitionProcessor(
                            factory)
                            .Observe(
                                ErrorDegraded(
                                    T0.AddMinutes(1)));

                    Assert.AreEqual(
                        InterfaceDegradationTransitionKind.Unchanged,
                        restarted.Kind);

                    Assert.AreEqual(
                        1,
                        outbox.ReadPending(
                            10)
                            .Count);
                });
        }

        [TestMethod]
        public void ChangedAndResolvedPersistImmutableEventsInOrder()
        {
            WithDatabase(
                factory =>
                {
                    var processor =
                        new SqliteInterfaceDegradationTransitionProcessor(
                            factory);

                    processor.Observe(
                        ErrorDegraded(
                            T0));

                    processor.Observe(
                        ErrorAndDiscardDegraded(
                            T0.AddMinutes(1)));

                    processor.Observe(
                        Healthy(
                            T0.AddMinutes(2)));

                    var pending =
                        new SqliteInterfaceDegradationEventOutbox(
                            factory)
                            .ReadPending(
                                10);

                    Assert.AreEqual(
                        3,
                        pending.Count);

                    Assert.AreEqual(
                        InterfaceDegradationTransitionKind.FirstAppearance,
                        pending[0].TransitionKind);

                    Assert.AreEqual(
                        InterfaceDegradationTransitionKind.Changed,
                        pending[1].TransitionKind);

                    Assert.IsTrue(
                        pending[1].PreviousStatus.HasValue);

                    Assert.AreEqual(
                        InterfaceDegradationStatus.Degraded,
                        pending[1].PreviousStatus.Value);

                    Assert.AreEqual(
                        InterfaceDegradationStatus.Degraded,
                        pending[1].CurrentStatus);

                    Assert.AreNotEqual(
                        pending[1].PreviousEvidenceFingerprint,
                        pending[1].CurrentEvidenceFingerprint);

                    Assert.AreEqual(
                        InterfaceDegradationTransitionKind.Resolved,
                        pending[2].TransitionKind);

                    Assert.IsTrue(
                        pending[2].PreviousStatus.HasValue);

                    Assert.AreEqual(
                        InterfaceDegradationStatus.Degraded,
                        pending[2].PreviousStatus.Value);

                    Assert.AreEqual(
                        InterfaceDegradationStatus.Healthy,
                        pending[2].CurrentStatus);

                    Assert.AreEqual(
                        0,
                        pending[2].Reasons.Count);
                });
        }

        [TestMethod]
        public void IndeterminateAndInitialHealthyDoNotEnqueue()
        {
            WithDatabase(
                factory =>
                {
                    var processor =
                        new SqliteInterfaceDegradationTransitionProcessor(
                            factory);

                    var indeterminate =
                        processor.Observe(
                            NoBaseline(
                                T0));

                    Assert.AreEqual(
                        InterfaceDegradationTransitionKind.Indeterminate,
                        indeterminate.Kind);

                    Assert.AreEqual(
                        0,
                        new SqliteInterfaceDegradationEventOutbox(
                            factory)
                            .ReadPending(
                                10)
                            .Count);

                    var healthy =
                        processor.Observe(
                            Healthy(
                                T0.AddMinutes(1)));

                    Assert.AreEqual(
                        InterfaceDegradationTransitionKind.Unchanged,
                        healthy.Kind);

                    Assert.AreEqual(
                        0,
                        new SqliteInterfaceDegradationEventOutbox(
                            factory)
                            .ReadPending(
                                10)
                            .Count);

                    var persisted =
                        new SqliteInterfaceDegradationStateStore(
                            factory)
                            .Load(
                                DeviceId,
                                7);

                    Assert.AreEqual(
                        InterfaceDegradationStatus.Healthy,
                        persisted.Status);
                });
        }

        [TestMethod]
        public void OutboxInsertFailureRollsBackStateAndRetryCreatesOneEvent()
        {
            WithDatabase(
                factory =>
                {
                    using (var connection =
                        factory.OpenConnection())
                    using (var command =
                        connection.CreateCommand())
                    {
                        command.CommandText = @"
CREATE TRIGGER reject_interface_degradation_outbox
BEFORE INSERT ON interface_degradation_outbox
BEGIN
    SELECT RAISE(ABORT, 'synthetic outbox failure');
END;";

                        command.ExecuteNonQuery();
                    }

                    try
                    {
                        new SqliteInterfaceDegradationTransitionProcessor(
                            factory)
                            .Observe(
                                ErrorDegraded(
                                    T0));

                        Assert.Fail(
                            "Synthetic outbox failure must abort the atomic transition write.");
                    }
                    catch (SQLiteException)
                    {
                    }

                    Assert.IsNull(
                        new SqliteInterfaceDegradationStateStore(
                            factory)
                            .Load(
                                DeviceId,
                                7));

                    Assert.AreEqual(
                        0,
                        new SqliteInterfaceDegradationEventOutbox(
                            factory)
                            .ReadPending(
                                10)
                            .Count);

                    using (var connection =
                        factory.OpenConnection())
                    using (var command =
                        connection.CreateCommand())
                    {
                        command.CommandText =
                            "DROP TRIGGER reject_interface_degradation_outbox;";

                        command.ExecuteNonQuery();
                    }

                    var retried =
                        new SqliteInterfaceDegradationTransitionProcessor(
                            factory)
                            .Observe(
                                ErrorDegraded(
                                    T0));

                    Assert.AreEqual(
                        InterfaceDegradationTransitionKind.FirstAppearance,
                        retried.Kind);

                    Assert.AreEqual(
                        1,
                        new SqliteInterfaceDegradationEventOutbox(
                            factory)
                            .ReadPending(
                                10)
                            .Count);
                });
        }

        [TestMethod]
        public void ReadPendingHonorsLimitAndStableEventKeys()
        {
            WithDatabase(
                factory =>
                {
                    var processor =
                        new SqliteInterfaceDegradationTransitionProcessor(
                            factory);

                    processor.Observe(
                        ErrorDegraded(
                            T0));

                    processor.Observe(
                        ErrorAndDiscardDegraded(
                            T0.AddMinutes(1)));

                    processor.Observe(
                        Healthy(
                            T0.AddMinutes(2)));

                    var outbox =
                        new SqliteInterfaceDegradationEventOutbox(
                            factory);

                    var firstTwo =
                        outbox.ReadPending(
                            2);

                    Assert.AreEqual(
                        2,
                        firstTwo.Count);

                    Assert.AreEqual(
                        T0,
                        firstTwo[0].CapturedUtc);

                    Assert.AreEqual(
                        T0.AddMinutes(1),
                        firstTwo[1].CapturedUtc);

                    Assert.IsFalse(
                        string.IsNullOrWhiteSpace(
                            firstTwo[0].EventKey));

                    Assert.AreEqual(
                        firstTwo[0].EventKey,
                        outbox.ReadPending(
                            1)[0]
                            .EventKey);
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
                1.0);
        }

        private static InterfaceDegradationClassification ErrorDegraded(
            DateTime capturedUtc)
        {
            return Classify(
                capturedUtc,
                3,
                3,
                5.0,
                1.0);
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
                        1.0));
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
                    "interface-degradation-outbox.db");

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
