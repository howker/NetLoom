using System;
using System.Data.SQLite;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class DatabaseInitializationTests
    {
        [TestMethod]
        public void InitializeCreatesSchemaAndIsIdempotent()
        {
            var tempDirectory = Path.Combine(
                Path.GetTempPath(),
                "NetLoom.Tests",
                Guid.NewGuid().ToString("N"));

            var databasePath = Path.Combine(
                tempDirectory,
                "netloom.db");

            try
            {
                var connectionFactory =
                    new SqliteConnectionFactory(databasePath);

                var initializer =
                    new DatabaseInitializer(connectionFactory);

                initializer.Initialize();

                Assert.IsTrue(File.Exists(databasePath));

                initializer.Initialize();

                using (var connection = connectionFactory.OpenConnection())
                {
                    Assert.AreEqual(
                        1L,
                        ExecuteScalarInt64(
                            connection,
                            "PRAGMA foreign_keys;"));

                    Assert.AreEqual(
                        1L,
                        ExecuteScalarInt64(
                            connection,
                            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'schema_migrations';"));

                    Assert.AreEqual(
                        1L,
                        ExecuteScalarInt64(
                            connection,
                            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'app_settings';"));

                    Assert.AreEqual(
                        1L,
                        ExecuteScalarInt64(
                            connection,
                            "SELECT COUNT(*) FROM schema_migrations WHERE version = 1;"));

                    Assert.AreEqual(
                        1L,
                        ExecuteScalarInt64(
                            connection,
                            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'interface_counter_baselines';"));

                    Assert.AreEqual(14L,
                        ExecuteScalarInt64(
                            connection,
                            "SELECT COUNT(*) FROM schema_migrations;"));
                }
            }
            finally
            {
                SQLiteConnection.ClearAllPools();

                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(
                        tempDirectory,
                        recursive: true);
                }
            }
        }

        private static long ExecuteScalarInt64(
            SQLiteConnection connection,
            string sql)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;

                return Convert.ToInt64(
                    command.ExecuteScalar());
            }
        }
    }
}
