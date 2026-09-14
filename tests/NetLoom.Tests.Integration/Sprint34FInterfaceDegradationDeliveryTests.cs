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
    public sealed class Sprint34FInterfaceDegradationDeliveryTests
    {
        private static readonly DateTime T0 =
            new DateTime(
                2026, 1, 14, 12, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void MarkDeliveredRemovesEventFromPendingAcrossRestart()
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

                    var firstOutbox =
                        new SqliteInterfaceDegradationEventOutbox(
                            factory);

                    var pending =
                        firstOutbox.ReadPending(
                            10);

                    Assert.AreEqual(
                        1,
                        pending.Count);

                    Assert.IsTrue(
                        firstOutbox.MarkDelivered(
                            pending[0].EventKey,
                            T0.AddMinutes(1)));

                    Assert.AreEqual(
                        0,
                        new SqliteInterfaceDegradationEventOutbox(
                            factory)
                            .ReadPending(
                                10)
                            .Count);
                });
        }

        [TestMethod]
        public void RepeatedAcknowledgementIsIdempotent()
        {
            WithDatabase(
                factory =>
                {
                    new SqliteInterfaceDegradationTransitionProcessor(
                        factory)
                        .Observe(
                            ErrorDegraded(
                                T0));

                    var outbox =
                        new SqliteInterfaceDegradationEventOutbox(
                            factory);

                    var eventKey =
                        outbox.ReadPending(
                            1)[0]
                            .EventKey;

                    Assert.IsTrue(
                        outbox.MarkDelivered(
                            eventKey,
                            T0.AddMinutes(1)));

                    Assert.IsFalse(
                        outbox.MarkDelivered(
                            eventKey,
                            T0.AddMinutes(2)));

                    Assert.AreEqual(
                        0,
                        outbox.ReadPending(
                            10)
                            .Count);
                });
        }

        [TestMethod]
        public void UnknownEventCannotBeAcknowledged()
        {
            WithDatabase(
                factory =>
                {
                    try
                    {
                        new SqliteInterfaceDegradationEventOutbox(
                            factory)
                            .MarkDelivered(
                                "missing-event",
                                T0);

                        Assert.Fail(
                            "Unknown event acknowledgement must fail.");
                    }
                    catch (InvalidOperationException)
                    {
                    }
                });
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
                    "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                7,
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
                    "interface-degradation-delivery.db");

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
