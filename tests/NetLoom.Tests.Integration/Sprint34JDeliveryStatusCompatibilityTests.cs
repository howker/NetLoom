using System;
using System.Data.SQLite;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Monitoring;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint34JDeliveryStatusCompatibilityTests
    {
        [TestMethod]
        public void LegacyDatabaseIsRejectedWithoutChangingDatabaseBytes()
        {
            WithTemporaryPath(
                databasePath =>
                {
                    CreateDatabase(
                        databasePath,
                        "CREATE TABLE legacy_marker (id INTEGER PRIMARY KEY);");

                    SQLiteConnection.ClearAllPools();

                    var before =
                        File.ReadAllBytes(
                            databasePath);

                    var compatible =
                        new SqliteInterfaceDegradationDeliveryStatusSchemaProbe(
                            new SqliteConnectionFactory(
                                databasePath))
                            .IsCompatible();

                    SQLiteConnection.ClearAllPools();

                    var after =
                        File.ReadAllBytes(
                            databasePath);

                    Assert.IsFalse(
                        compatible);

                    CollectionAssert.AreEqual(
                        before,
                        after);
                });
        }

        [TestMethod]
        public void IncompleteOutboxSchemaIsRejectedAsUnsupported()
        {
            WithTemporaryPath(
                databasePath =>
                {
                    CreateDatabase(
                        databasePath,
                        "CREATE TABLE interface_degradation_outbox (event_key TEXT PRIMARY KEY);");

                    var compatible =
                        new SqliteInterfaceDegradationDeliveryStatusSchemaProbe(
                            new SqliteConnectionFactory(
                                databasePath))
                            .IsCompatible();

                    Assert.IsFalse(
                        compatible);
                });
        }

        [TestMethod]
        public void CurrentSchemaEmptyDatabaseIsCompatibleAndReturnsNoEvents()
        {
            WithTemporaryPath(
                databasePath =>
                {
                    var factory =
                        new SqliteConnectionFactory(
                            databasePath);

                    new DatabaseInitializer(
                        factory)
                        .Initialize();

                    SQLiteConnection.ClearAllPools();

                    Assert.IsTrue(
                        new SqliteInterfaceDegradationDeliveryStatusSchemaProbe(
                            factory)
                            .IsCompatible());

                    var statuses =
                        new SqliteInterfaceDegradationEventOutbox(
                            factory)
                            .ReadStatus(
                                50,
                                new DateTime(
                                    2026, 1, 16, 12, 0, 0,
                                    DateTimeKind.Utc));

                    Assert.AreEqual(
                        0,
                        statuses.Count);
                });
        }

        private static void CreateDatabase(
            string databasePath,
            string commandText)
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    databasePath));

            var builder =
                new SQLiteConnectionStringBuilder
                {
                    DataSource = databasePath
                };

            using (var connection =
                new SQLiteConnection(
                    builder.ConnectionString))
            {
                connection.Open();

                using (var command =
                    connection.CreateCommand())
                {
                    command.CommandText =
                        commandText;

                    command.ExecuteNonQuery();
                }
            }
        }

        private static void WithTemporaryPath(
            Action<string> action)
        {
            var directory =
                Path.Combine(
                    Path.GetTempPath(),
                    "NetLoom.Tests",
                    Guid.NewGuid().ToString("N"));

            var databasePath =
                Path.Combine(
                    directory,
                    "delivery-status-schema.db");

            try
            {
                action(
                    databasePath);
            }
            finally
            {
                SQLiteConnection.ClearAllPools();

                if (Directory.Exists(
                    directory))
                {
                    Directory.Delete(
                        directory,
                        recursive: true);
                }
            }
        }
    }
}
