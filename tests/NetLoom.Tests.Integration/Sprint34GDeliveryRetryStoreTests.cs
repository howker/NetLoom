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
    public sealed class Sprint34GDeliveryRetryStoreTests
    {
        private static readonly DateTime T0 =
            new DateTime(
                2026, 1, 15, 12, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void FailureStateSurvivesRestartAndSuppressesBeforeEligibility()
        {
            WithDatabase(
                factory =>
                {
                    CreatePendingEvent(
                        factory);

                    var outbox =
                        new SqliteInterfaceDegradationEventOutbox(
                            factory);

                    var initial =
                        outbox.ReadReady(
                            10,
                            T0);

                    Assert.AreEqual(
                        1,
                        initial.Count);

                    Assert.AreEqual(
                        0,
                        initial[0].FailureCount);

                    Assert.IsFalse(
                        initial[0].LastFailureUtc.HasValue);

                    Assert.IsFalse(
                        initial[0].NextAttemptUtc.HasValue);

                    Assert.IsTrue(
                        outbox.MarkDeliveryFailed(
                            initial[0].Event.EventKey,
                            0,
                            T0.AddMinutes(1),
                            T0.AddMinutes(2)));

                    var restarted =
                        new SqliteInterfaceDegradationEventOutbox(
                            factory);

                    Assert.AreEqual(
                        1,
                        restarted.ReadPending(
                            10)
                            .Count);

                    Assert.AreEqual(
                        0,
                        restarted.ReadReady(
                            10,
                            T0.AddMinutes(1))
                            .Count);

                    var eligible =
                        restarted.ReadReady(
                            10,
                            T0.AddMinutes(2));

                    Assert.AreEqual(
                        1,
                        eligible.Count);

                    Assert.AreEqual(
                        1,
                        eligible[0].FailureCount);

                    Assert.AreEqual(
                        T0.AddMinutes(1),
                        eligible[0].LastFailureUtc);

                    Assert.AreEqual(
                        T0.AddMinutes(2),
                        eligible[0].NextAttemptUtc);
                });
        }

        [TestMethod]
        public void SecondFailureAdvancesDurableCountAndEligibility()
        {
            WithDatabase(
                factory =>
                {
                    CreatePendingEvent(
                        factory);

                    var first =
                        new SqliteInterfaceDegradationEventOutbox(
                            factory);

                    var eventKey =
                        first.ReadReady(
                            1,
                            T0)[0]
                            .Event
                            .EventKey;

                    Assert.IsTrue(
                        first.MarkDeliveryFailed(
                            eventKey,
                            0,
                            T0.AddMinutes(1),
                            T0.AddMinutes(2)));

                    var second =
                        new SqliteInterfaceDegradationEventOutbox(
                            factory);

                    Assert.IsTrue(
                        second.MarkDeliveryFailed(
                            eventKey,
                            1,
                            T0.AddMinutes(2),
                            T0.AddMinutes(4)));

                    var restarted =
                        new SqliteInterfaceDegradationEventOutbox(
                            factory);

                    Assert.AreEqual(
                        0,
                        restarted.ReadReady(
                            10,
                            T0.AddMinutes(3))
                            .Count);

                    var ready =
                        restarted.ReadReady(
                            10,
                            T0.AddMinutes(4));

                    Assert.AreEqual(
                        1,
                        ready.Count);

                    Assert.AreEqual(
                        2,
                        ready[0].FailureCount);

                    Assert.AreEqual(
                        T0.AddMinutes(2),
                        ready[0].LastFailureUtc);

                    Assert.AreEqual(
                        T0.AddMinutes(4),
                        ready[0].NextAttemptUtc);
                });
        }

        [TestMethod]
        public void DeliveredEventDoesNotReturnWithScheduledRetry()
        {
            WithDatabase(
                factory =>
                {
                    CreatePendingEvent(
                        factory);

                    var outbox =
                        new SqliteInterfaceDegradationEventOutbox(
                            factory);

                    var eventKey =
                        outbox.ReadReady(
                            1,
                            T0)[0]
                            .Event
                            .EventKey;

                    Assert.IsTrue(
                        outbox.MarkDeliveryFailed(
                            eventKey,
                            0,
                            T0.AddMinutes(1),
                            T0.AddMinutes(2)));

                    Assert.IsTrue(
                        outbox.MarkDelivered(
                            eventKey,
                            T0.AddMinutes(1)));

                    var restarted =
                        new SqliteInterfaceDegradationEventOutbox(
                            factory);

                    Assert.AreEqual(
                        0,
                        restarted.ReadPending(
                            10)
                            .Count);

                    Assert.AreEqual(
                        0,
                        restarted.ReadReady(
                            10,
                            T0.AddHours(1))
                            .Count);

                    Assert.IsFalse(
                        restarted.MarkDeliveryFailed(
                            eventKey,
                            1,
                            T0.AddHours(1),
                            T0.AddHours(2)));
                });
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
                    "cccccccc-dddd-eeee-ffff-000000000001"),
                11,
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
                    "interface-degradation-delivery-retry.db");

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
