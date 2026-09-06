using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Observations;
using NetLoom.Application.Snmp;
using NetLoom.Application.Topology;
using NetLoom.Domain.Observations;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Observations;
using NetLoom.Persistence.Sqlite.Topology;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint26BObservationRetentionTests
    {
        [TestMethod]
        public void RetentionIsTimeOnlyBoundedOldestFirstAndCutoffIsStrict()
        {
            var path =
                NewDatabasePath();

            try
            {
                var factory =
                    Initialize(path);

                var rawStore =
                    new SqliteObservationStore(
                        factory);

                var retention =
                    new SqliteObservationRetentionStore(
                        factory);

                var cutoff =
                    new DateTime(
                        2026,
                        9,
                        6,
                        12,
                        0,
                        0,
                        DateTimeKind.Utc);

                var oldest =
                    Guid.NewGuid();

                var second =
                    Guid.NewGuid();

                var atCutoff =
                    Guid.NewGuid();

                SaveRaw(
                    rawStore,
                    oldest,
                    ObservationKind.Fdb,
                    "10.10.10.1",
                    cutoff.AddHours(-3));

                SaveRaw(
                    rawStore,
                    second,
                    ObservationKind.Lldp,
                    "10.10.10.2",
                    cutoff.AddHours(-2));

                SaveRaw(
                    rawStore,
                    atCutoff,
                    ObservationKind.Stp,
                    "10.10.10.3",
                    cutoff);

                Assert.AreEqual(
                    1,
                    retention.DeleteOlderThan(
                        cutoff,
                        1));

                Assert.IsNull(
                    rawStore.GetSnmp(
                        oldest));

                Assert.IsNotNull(
                    rawStore.GetSnmp(
                        second));

                Assert.AreEqual(
                    1,
                    retention.DeleteOlderThan(
                        cutoff,
                        1));

                Assert.IsNull(
                    rawStore.GetSnmp(
                        second));

                Assert.IsNotNull(
                    rawStore.GetSnmp(
                        atCutoff));

                Assert.AreEqual(
                    0,
                    retention.DeleteOlderThan(
                        cutoff,
                        8));
            }
            finally
            {
                DeleteDatabaseFiles(path);
            }
        }

        [TestMethod]
        public void ManualObservationIsNotDeletedByRawRetention()
        {
            var path =
                NewDatabasePath();

            try
            {
                var factory =
                    Initialize(path);

                var rawStore =
                    new SqliteObservationStore(
                        factory);

                var retention =
                    new SqliteObservationRetentionStore(
                        factory);

                var id =
                    Guid.NewGuid();

                var now =
                    new DateTime(
                        2026,
                        9,
                        6,
                        12,
                        0,
                        0,
                        DateTimeKind.Utc);

                SaveRaw(
                    rawStore,
                    id,
                    ObservationKind.Manual,
                    "User",
                    now.AddDays(-30));

                Assert.AreEqual(
                    0,
                    retention.DeleteOlderThan(
                        now,
                        8));

                Assert.IsNotNull(
                    rawStore.GetSnmp(
                        id));
            }
            finally
            {
                DeleteDatabaseFiles(path);
            }
        }

        [TestMethod]
        public void RetentionDeletesRawVarbindsThroughExistingCascade()
        {
            var path =
                NewDatabasePath();

            try
            {
                var factory =
                    Initialize(path);

                var rawStore =
                    new SqliteObservationStore(
                        factory);

                var retention =
                    new SqliteObservationRetentionStore(
                        factory);

                var id =
                    Guid.NewGuid();

                var captured =
                    new DateTime(
                        2026,
                        9,
                        5,
                        12,
                        0,
                        0,
                        DateTimeKind.Utc);

                SaveRaw(
                    rawStore,
                    id,
                    ObservationKind.Fdb,
                    "10.20.30.40",
                    captured);

                Assert.AreEqual(
                    1L,
                    CountRows(
                        factory,
                        "snmp_varbinds"));

                Assert.AreEqual(
                    1,
                    retention.DeleteOlderThan(
                        captured.AddHours(1),
                        8));

                Assert.AreEqual(
                    0L,
                    CountRows(
                        factory,
                        "observations"));

                Assert.AreEqual(
                    0L,
                    CountRows(
                        factory,
                        "snmp_varbinds"));
            }
            finally
            {
                DeleteDatabaseFiles(path);
            }
        }

        [TestMethod]
        public void ExistingObservationCascadeGraphIsComplete()
        {
            var path =
                NewDatabasePath();

            try
            {
                var factory =
                    Initialize(path);

                AssertCascade(
                    factory,
                    "snmp_varbinds",
                    "observations");

                AssertCascade(
                    factory,
                    "lldp_observations",
                    "observations");

                AssertCascade(
                    factory,
                    "cdp_observations",
                    "observations");

                AssertCascade(
                    factory,
                    "bridge_port_mappings",
                    "observations");

                AssertCascade(
                    factory,
                    "fdb_observations",
                    "observations");

                AssertCascade(
                    factory,
                    "arp_observations",
                    "observations");

                AssertCascade(
                    factory,
                    "stp_observations",
                    "observations");

                AssertCascade(
                    factory,
                    "stp_port_states",
                    "stp_observations");
            }
            finally
            {
                DeleteDatabaseFiles(path);
            }
        }

        [TestMethod]
        public void CurrentEvidenceSurvivesRawExpiryAndReportsExpired()
        {
            var path =
                NewDatabasePath();

            try
            {
                var factory =
                    Initialize(path);

                var rawStore =
                    new SqliteObservationStore(
                        factory);

                var retention =
                    new SqliteObservationRetentionStore(
                        factory);

                IPhysicalLinkEvidenceExplanationReader reader =
                    new SqlitePhysicalLinkEvidenceExplanationReader(
                        factory);

                var linkId =
                    Guid.NewGuid();

                var observationId =
                    Guid.NewGuid();

                var captured =
                    new DateTime(
                        2026,
                        9,
                        5,
                        8,
                        0,
                        0,
                        DateTimeKind.Utc);

                SaveRaw(
                    rawStore,
                    observationId,
                    ObservationKind.Lldp,
                    "10.1.2.3",
                    captured);

                InsertEvidenceForReaderTest(
                    factory,
                    linkId,
                    observationId,
                    captured);

                var before =
                    reader.Get(
                        linkId);

                Assert.AreEqual(
                    1,
                    before.Count);

                Assert.AreEqual(
                    ObservationRawAvailability.Available,
                    before[0].RawAvailability);

                Assert.AreEqual(
                    observationId,
                    before[0].Evidence.ObservationId.Value);

                Assert.AreEqual(
                    "port:Gi1/0/1",
                    before[0].Evidence.SlotDiscriminator);

                Assert.AreEqual(
                    "lldp-neighbor",
                    before[0].Evidence.Detail);

                Assert.AreEqual(
                    1,
                    retention.DeleteOlderThan(
                        captured.AddHours(1),
                        8));

                Assert.IsNull(
                    rawStore.GetSnmp(
                        observationId));

                var after =
                    reader.Get(
                        linkId);

                Assert.AreEqual(
                    1,
                    after.Count);

                Assert.AreEqual(
                    ObservationRawAvailability.Expired,
                    after[0].RawAvailability);

                Assert.AreEqual(
                    observationId,
                    after[0].Evidence.ObservationId.Value);

                Assert.AreEqual(
                    captured,
                    after[0].Evidence.CapturedUtc.Value);

                Assert.AreEqual(
                    "10.1.2.3",
                    after[0].Evidence.SourceAddress);

                Assert.AreEqual(
                    "lldp-neighbor",
                    after[0].Evidence.Detail);
            }
            finally
            {
                DeleteDatabaseFiles(path);
            }
        }

        [TestMethod]
        public void WalReaderDoesNotBlockBoundedRetentionWriter()
        {
            var path =
                NewDatabasePath();

            try
            {
                var factory =
                    Initialize(path);

                var rawStore =
                    new SqliteObservationStore(
                        factory);

                var retention =
                    new SqliteObservationRetentionStore(
                        factory);

                var captured =
                    new DateTime(
                        2026,
                        9,
                        5,
                        6,
                        0,
                        0,
                        DateTimeKind.Utc);

                SaveRaw(
                    rawStore,
                    Guid.NewGuid(),
                    ObservationKind.Fdb,
                    "10.50.0.1",
                    captured);

                using (var readerConnection =
                    factory.OpenConnection())
                {
                    using (var begin =
                        readerConnection.CreateCommand())
                    {
                        begin.CommandText =
                            "BEGIN;";

                        begin.ExecuteNonQuery();
                    }

                    try
                    {
                        using (var command =
                            readerConnection.CreateCommand())
                        {
                            command.CommandText =
                                "SELECT COUNT(*) FROM observations;";

                            Assert.AreEqual(
                                1L,
                                Convert.ToInt64(
                                    command.ExecuteScalar(),
                                    CultureInfo.InvariantCulture));
                        }

                        var task =
                            Task.Run(
                                () =>
                                    retention.DeleteOlderThan(
                                        captured.AddHours(1),
                                        1));

                        Assert.IsTrue(
                            task.Wait(
                                TimeSpan.FromSeconds(10)),
                            "Retention writer was blocked by a deferred WAL reader.");

                        Assert.AreEqual(
                            1,
                            task.Result);
                    }
                    finally
                    {
                        using (var rollback =
                            readerConnection.CreateCommand())
                        {
                            rollback.CommandText =
                                "ROLLBACK;";

                            rollback.ExecuteNonQuery();
                        }
                    }
                }
            }
            finally
            {
                DeleteDatabaseFiles(path);
            }
        }

        private static SqliteConnectionFactory Initialize(
            string path)
        {
            var factory =
                new SqliteConnectionFactory(
                    path);

            new DatabaseInitializer(
                factory)
                .Initialize();

            return factory;
        }

        private static void SaveRaw(
            SqliteObservationStore store,
            Guid id,
            ObservationKind kind,
            string sourceAddress,
            DateTime capturedUtc)
        {
            store.SaveSnmp(
                new SnmpObservation(
                    new Observation(
                        id,
                        kind,
                        sourceAddress,
                        capturedUtc),
                    new[]
                    {
                        new SnmpVariable(
                            "1.3.6.1.2.1.1.1.0",
                            4,
                            "value",
                            new byte[]
                            {
                                1,
                                2,
                                3
                            })
                    }));
        }

        private static long CountRows(
            SqliteConnectionFactory factory,
            string tableName)
        {
            using (var connection =
                factory.OpenConnection())
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT COUNT(*) FROM " +
                    tableName +
                    ";";

                return Convert.ToInt64(
                    command.ExecuteScalar(),
                    CultureInfo.InvariantCulture);
            }
        }

        private static void AssertCascade(
            SqliteConnectionFactory factory,
            string tableName,
            string referencedTable)
        {
            using (var connection =
                factory.OpenConnection())
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText =
                    "PRAGMA foreign_key_list(" +
                    tableName +
                    ");";

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (string.Equals(
                                reader.GetString(2),
                                referencedTable,
                                StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(
                                reader.GetString(6),
                                "CASCADE",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }
                    }
                }
            }

            Assert.Fail(
                "Expected ON DELETE CASCADE from " +
                tableName +
                " to " +
                referencedTable +
                ".");
        }

        private static void InsertEvidenceForReaderTest(
            SqliteConnectionFactory factory,
            Guid linkId,
            Guid observationId,
            DateTime capturedUtc)
        {
            using (var connection =
                factory.OpenConnection())
            {
                using (var pragma =
                    connection.CreateCommand())
                {
                    pragma.CommandText =
                        "PRAGMA foreign_keys = OFF;";

                    pragma.ExecuteNonQuery();
                }

                using (var command =
                    connection.CreateCommand())
                {
                    command.CommandText = @"
INSERT INTO physical_link_evidence_current
(
    physical_link_id,
    evidence_kind,
    evidence_strength,
    source_address,
    slot_discriminator,
    observation_id,
    captured_utc,
    detail
)
VALUES
(
    @physicalLinkId,
    @kind,
    @strength,
    @sourceAddress,
    @slot,
    @observationId,
    @capturedUtc,
    @detail
);";

                    command.Parameters.AddWithValue(
                        "@physicalLinkId",
                        linkId.ToString("D"));

                    command.Parameters.AddWithValue(
                        "@kind",
                        "Lldp");

                    command.Parameters.AddWithValue(
                        "@strength",
                        "Strong");

                    command.Parameters.AddWithValue(
                        "@sourceAddress",
                        "10.1.2.3");

                    command.Parameters.AddWithValue(
                        "@slot",
                        "port:Gi1/0/1");

                    command.Parameters.AddWithValue(
                        "@observationId",
                        observationId.ToString("D"));

                    command.Parameters.AddWithValue(
                        "@capturedUtc",
                        capturedUtc.ToString(
                            "o",
                            CultureInfo.InvariantCulture));

                    command.Parameters.AddWithValue(
                        "@detail",
                        "lldp-neighbor");

                    command.ExecuteNonQuery();
                }
            }
        }

        private static string NewDatabasePath()
        {
            return Path.Combine(
                Path.GetTempPath(),
                "netloom-s26b-" +
                Guid.NewGuid().ToString("N") +
                ".db");
        }

        private static void DeleteDatabaseFiles(
            string path)
        {
            foreach (var candidate in new[]
            {
                path,
                path + "-wal",
                path + "-shm"
            })
            {
                try
                {
                    if (File.Exists(candidate))
                    {
                        File.Delete(candidate);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
