using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Migrations;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class MigrationRunnerTests
    {
        [TestMethod]
        public void ConstructorRejectsDuplicateMigrationVersions()
        {
            try
            {
                new MigrationRunner(
                    new IMigration[]
                    {
                        new TestMigration(
                            7,
                            "First",
                            new[] { "SELECT 1;" }),
                        new TestMigration(
                            7,
                            "Second",
                            new[] { "SELECT 1;" })
                    });

                Assert.Fail("Duplicate migration versions must be rejected.");
            }
            catch (InvalidOperationException exception)
            {
                StringAssert.Contains(
                    exception.Message,
                    "Duplicate migration version");
            }
        }

        [TestMethod]
        public void FailedMigrationIsRolledBack()
        {
            var tempDirectory = Path.Combine(
                Path.GetTempPath(),
                "NetLoom.Tests",
                Guid.NewGuid().ToString("N"));

            var databasePath = Path.Combine(
                tempDirectory,
                "rollback.db");

            try
            {
                var connectionFactory =
                    new SqliteConnectionFactory(databasePath);

                using (var connection = connectionFactory.OpenConnection())
                {
                    var runner = new MigrationRunner(
                        new IMigration[]
                        {
                            new TestMigration(
                                2,
                                "Failing migration",
                                new[]
                                {
                                    @"
CREATE TABLE rollback_probe
(
    id INTEGER NOT NULL PRIMARY KEY
);",
                                    "THIS IS NOT VALID SQL;"
                                })
                        });

                    try
                    {
                        runner.ApplyPending(connection);
                        Assert.Fail("Failing migration must throw.");
                    }
                    catch (SQLiteException)
                    {
                    }

                    Assert.AreEqual(
                        0L,
                        ExecuteScalarInt64(
                            connection,
                            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'rollback_probe';"));

                    Assert.AreEqual(
                        0L,
                        ExecuteScalarInt64(
                            connection,
                            "SELECT COUNT(*) FROM schema_migrations WHERE version = 2;"));
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

        [TestMethod]
        public void FreshDatabaseIncludesManagementAddressAndIfTypeColumns()
        {
            var tempDirectory = Path.Combine(
                Path.GetTempPath(),
                "NetLoom.Tests",
                Guid.NewGuid().ToString("N"));

            var databasePath = Path.Combine(
                tempDirectory,
                "migration20.db");

            try
            {
                var connectionFactory =
                    new SqliteConnectionFactory(databasePath);

                new DatabaseInitializer(
                    connectionFactory)
                    .Initialize();

                using (var connection = connectionFactory.OpenConnection())
                {
                    Assert.AreEqual(
                        1L,
                        ExecuteScalarInt64(
                            connection,
                            "SELECT COUNT(*) FROM pragma_table_info('devices') WHERE name = 'management_address';"));

                    Assert.AreEqual(
                        1L,
                        ExecuteScalarInt64(
                            connection,
                            "SELECT COUNT(*) FROM pragma_table_info('interfaces') WHERE name = 'if_type';"));

                    Assert.AreEqual(
                        1L,
                        ExecuteScalarInt64(
                            connection,
                            "SELECT COUNT(*) FROM schema_migrations WHERE version = 20;"));
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

        private sealed class TestMigration : IMigration
        {
            public TestMigration(
                int version,
                string name,
                IReadOnlyList<string> statements)
            {
                Version = version;
                Name = name;
                Statements = statements;
            }

            public int Version { get; }

            public string Name { get; }

            public IReadOnlyList<string> Statements { get; }
        }
    }
}
