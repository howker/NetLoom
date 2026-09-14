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
    public sealed class Sprint34BInterfaceCounterBaselineStoreTests
    {
        private static readonly Guid DeviceId =
            new Guid(
                "22222222-3333-4444-5555-666666666666");

        private static readonly DateTime T1 =
            new DateTime(
                2026, 1, 6, 12, 0, 0,
                DateTimeKind.Utc);

        private static readonly DateTime T2 =
            T1.AddSeconds(30);

        private static readonly DateTime T3 =
            T2.AddSeconds(30);

        [TestMethod]
        public void RestartLoadsPersistedBaselineAndComputesValidDelta()
        {
            WithDatabase(
                connectionFactory =>
                {
                    var firstStore =
                        new SqliteInterfaceCounterBaselineStore(
                            connectionFactory);

                    var first =
                        Snapshot(
                            T1,
                            10,
                            20,
                            30,
                            40,
                            500);

                    Assert.IsNull(
                        firstStore.ReplaceAndGetPrevious(
                            first));

                    var restartedStore =
                        new SqliteInterfaceCounterBaselineStore(
                            connectionFactory);

                    var second =
                        Snapshot(
                            T2,
                            13,
                            27,
                            35,
                            49,
                            500);

                    var previous =
                        restartedStore.ReplaceAndGetPrevious(
                            second);

                    Assert.IsNotNull(
                        previous);

                    Assert.AreEqual(
                        T1,
                        previous.CapturedUtc);

                    var delta =
                        new InterfaceCounterDeltaEvaluator()
                            .Evaluate(
                                previous,
                                second);

                    Assert.AreEqual(
                        InterfaceCounterDeltaStatus.Valid,
                        delta.Status);

                    Assert.AreEqual(
                        (ulong)3,
                        delta.InErrors);

                    Assert.AreEqual(
                        (ulong)7,
                        delta.OutErrors);

                    Assert.AreEqual(
                        (ulong)5,
                        delta.InDiscards);

                    Assert.AreEqual(
                        (ulong)9,
                        delta.OutDiscards);
                });
        }

        [TestMethod]
        public void DiscontinuitySampleBecomesNextTrustedBaseline()
        {
            WithDatabase(
                connectionFactory =>
                {
                    var store =
                        new SqliteInterfaceCounterBaselineStore(
                            connectionFactory);

                    store.ReplaceAndGetPrevious(
                        Snapshot(
                            T1,
                            4000,
                            5000,
                            6000,
                            7000,
                            500));

                    var reset =
                        Snapshot(
                            T2,
                            2,
                            3,
                            4,
                            5,
                            800);

                    var beforeReset =
                        store.ReplaceAndGetPrevious(
                            reset);

                    var discontinuity =
                        new InterfaceCounterDeltaEvaluator()
                            .Evaluate(
                                beforeReset,
                                reset);

                    Assert.AreEqual(
                        InterfaceCounterDeltaStatus.Discontinuity,
                        discontinuity.Status);

                    var afterReset =
                        Snapshot(
                            T3,
                            5,
                            8,
                            10,
                            14,
                            800);

                    var resetBaseline =
                        new SqliteInterfaceCounterBaselineStore(
                            connectionFactory)
                            .ReplaceAndGetPrevious(
                                afterReset);

                    Assert.AreEqual(
                        T2,
                        resetBaseline.CapturedUtc);

                    var nextDelta =
                        new InterfaceCounterDeltaEvaluator()
                            .Evaluate(
                                resetBaseline,
                                afterReset);

                    Assert.AreEqual(
                        InterfaceCounterDeltaStatus.Valid,
                        nextDelta.Status);

                    Assert.AreEqual(
                        (ulong)3,
                        nextDelta.InErrors);
                });
        }

        [TestMethod]
        public void StaleSampleIsRejectedWithoutReplacingBaseline()
        {
            WithDatabase(
                connectionFactory =>
                {
                    var store =
                        new SqliteInterfaceCounterBaselineStore(
                            connectionFactory);

                    store.ReplaceAndGetPrevious(
                        Snapshot(
                            T2,
                            20,
                            30,
                            40,
                            50,
                            500));

                    try
                    {
                        store.ReplaceAndGetPrevious(
                            Snapshot(
                                T1,
                                10,
                                20,
                                30,
                                40,
                                500));

                        Assert.Fail(
                            "Stale interface counter baseline must be rejected.");
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    var previous =
                        new SqliteInterfaceCounterBaselineStore(
                            connectionFactory)
                            .ReplaceAndGetPrevious(
                                Snapshot(
                                    T3,
                                    25,
                                    35,
                                    45,
                                    55,
                                    500));

                    Assert.AreEqual(
                        T2,
                        previous.CapturedUtc);

                    Assert.AreEqual(
                        (uint)20,
                        previous.InErrors);
                });
        }

        private static InterfaceMonitoringSnapshot Snapshot(
            DateTime capturedUtc,
            uint? inErrors,
            uint? outErrors,
            uint? inDiscards,
            uint? outDiscards,
            uint? discontinuity)
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
                discontinuity);
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
                    "interface-counter-baseline.db");

            try
            {
                var connectionFactory =
                    new SqliteConnectionFactory(
                        databasePath);

                new DatabaseInitializer(
                    connectionFactory)
                    .Initialize();

                action(
                    connectionFactory);
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
