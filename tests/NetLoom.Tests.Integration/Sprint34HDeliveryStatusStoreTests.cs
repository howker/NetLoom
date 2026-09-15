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
    public sealed class Sprint34HDeliveryStatusStoreTests
    {
        private static readonly DateTime T0 =
            new DateTime(
                2026, 1, 16, 12, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void NewPendingEventIsOperatorReady()
        {
            WithDatabase(
                factory =>
                {
                    CreatePendingEvent(
                        factory);

                    var status =
                        ReadSingle(
                            factory,
                            T0);

                    Assert.AreEqual(
                        InterfaceDegradationDeliveryStatusKind
                            .Ready,
                        status.Kind);

                    Assert.AreEqual(
                        0,
                        status.FailureCount);

                    Assert.IsFalse(
                        status.DeliveredUtc.HasValue);
                });
        }

        [TestMethod]
        public void FailedEventIsDeferredBeforeNextAttempt()
        {
            WithDatabase(
                factory =>
                {
                    CreatePendingEvent(
                        factory);

                    var outbox =
                        new SqliteInterfaceDegradationEventOutbox(
                            factory);

                    var pending =
                        outbox.ReadReady(
                            1,
                            T0)[0];

                    Assert.IsTrue(
                        outbox.MarkDeliveryFailed(
                            pending.Event.EventKey,
                            0,
                            T0.AddMinutes(1),
                            T0.AddMinutes(2)));

                    var status =
                        ReadSingle(
                            factory,
                            T0.AddMinutes(1));

                    Assert.AreEqual(
                        InterfaceDegradationDeliveryStatusKind
                            .Deferred,
                        status.Kind);

                    Assert.AreEqual(
                        1,
                        status.FailureCount);

                    Assert.AreEqual(
                        T0.AddMinutes(2),
                        status.NextAttemptUtc);
                });
        }

        [TestMethod]
        public void DeferredEventBecomesReadyAtEligibility()
        {
            WithDatabase(
                factory =>
                {
                    CreatePendingEvent(
                        factory);

                    var outbox =
                        new SqliteInterfaceDegradationEventOutbox(
                            factory);

                    var pending =
                        outbox.ReadReady(
                            1,
                            T0)[0];

                    Assert.IsTrue(
                        outbox.MarkDeliveryFailed(
                            pending.Event.EventKey,
                            0,
                            T0.AddMinutes(1),
                            T0.AddMinutes(2)));

                    var status =
                        ReadSingle(
                            factory,
                            T0.AddMinutes(2));

                    Assert.AreEqual(
                        InterfaceDegradationDeliveryStatusKind
                            .Ready,
                        status.Kind);

                    Assert.AreEqual(
                        1,
                        status.FailureCount);
                });
        }

        [TestMethod]
        public void DeliveredHistoryRetainsFailureEvidenceAcrossRestart()
        {
            WithDatabase(
                factory =>
                {
                    CreatePendingEvent(
                        factory);

                    var outbox =
                        new SqliteInterfaceDegradationEventOutbox(
                            factory);

                    var pending =
                        outbox.ReadReady(
                            1,
                            T0)[0];

                    Assert.IsTrue(
                        outbox.MarkDeliveryFailed(
                            pending.Event.EventKey,
                            0,
                            T0.AddMinutes(1),
                            T0.AddMinutes(2)));

                    Assert.IsTrue(
                        outbox.MarkDelivered(
                            pending.Event.EventKey,
                            T0.AddMinutes(3)));

                    var restarted =
                        new SqliteInterfaceDegradationEventOutbox(
                            factory);

                    var statuses =
                        restarted.ReadStatus(
                            10,
                            T0.AddMinutes(4));

                    Assert.AreEqual(
                        1,
                        statuses.Count);

                    Assert.AreEqual(
                        InterfaceDegradationDeliveryStatusKind
                            .Delivered,
                        statuses[0].Kind);

                    Assert.AreEqual(
                        1,
                        statuses[0].FailureCount);

                    Assert.AreEqual(
                        T0.AddMinutes(1),
                        statuses[0].LastFailureUtc);

                    Assert.AreEqual(
                        T0.AddMinutes(2),
                        statuses[0].NextAttemptUtc);

                    Assert.AreEqual(
                        T0.AddMinutes(3),
                        statuses[0].DeliveredUtc);
                });
        }

        private static InterfaceDegradationDeliveryStatus
            ReadSingle(
                SqliteConnectionFactory factory,
                DateTime nowUtc)
        {
            var statuses =
                new SqliteInterfaceDegradationEventOutbox(
                    factory)
                    .ReadStatus(
                        10,
                        nowUtc);

            Assert.AreEqual(
                1,
                statuses.Count);

            return statuses[0];
        }

        private static void CreatePendingEvent(
            SqliteConnectionFactory factory)
        {
            new SqliteInterfaceDegradationTransitionProcessor(
                factory)
                .Observe(
                    ErrorDegraded(
                        T0));
        }

        private static InterfaceDegradationClassification
            ErrorDegraded(
                DateTime capturedUtc)
        {
            var previous =
                Snapshot(
                    capturedUtc.AddMinutes(-1),
                    10,
                    20);

            var current =
                Snapshot(
                    capturedUtc,
                    13,
                    23);

            return new InterfaceDegradationClassifier()
                .Classify(
                    new InterfaceCounterEvaluation(
                        current,
                        new InterfaceCounterDeltaEvaluator()
                            .Evaluate(
                                previous,
                                current)),
                    new InterfaceDegradationPolicy(
                        5.0,
                        null));
        }

        private static InterfaceMonitoringSnapshot Snapshot(
            DateTime capturedUtc,
            uint inErrors,
            uint outErrors)
        {
            return new InterfaceMonitoringSnapshot(
                new Guid(
                    "dddddddd-eeee-ffff-0000-000000000001"),
                13,
                1,
                1,
                capturedUtc,
                inErrors,
                outErrors,
                0,
                0,
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
                    "interface-degradation-delivery-status.db");

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
