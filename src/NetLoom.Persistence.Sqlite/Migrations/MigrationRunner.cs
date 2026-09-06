using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class MigrationRunner
    {
        private readonly IReadOnlyList<IMigration> _migrations;

        public MigrationRunner(IEnumerable<IMigration> migrations)
        {
            if (migrations == null)
            {
                throw new ArgumentNullException(nameof(migrations));
            }

            _migrations = migrations
                .OrderBy(migration => migration.Version)
                .ToArray();

            ValidateVersions();
        }

        public void ApplyPending(SQLiteConnection connection)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            EnsureSchemaMigrationsTable(connection);

            NetLoom.Persistence.Sqlite.Database.SqliteImmediateWrite.Execute(
                connection,
                () =>
                {
                    var appliedVersions =
                        LoadAppliedVersions(connection);

                    foreach (var migration in _migrations)
                    {
                        if (appliedVersions.Contains(
                            migration.Version))
                        {
                            continue;
                        }

                        ApplyMigration(
                            connection,
                            migration);
                    }
                });
}

        private void ValidateVersions()
        {
            var duplicate = _migrations
                .GroupBy(migration => migration.Version)
                .FirstOrDefault(group => group.Count() > 1);

            if (duplicate != null)
            {
                throw new InvalidOperationException(
                    "Duplicate migration version: " + duplicate.Key);
            }
        }

        private static void EnsureSchemaMigrationsTable(
            SQLiteConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
CREATE TABLE IF NOT EXISTS schema_migrations
(
    version INTEGER NOT NULL PRIMARY KEY,
    name TEXT NOT NULL,
    applied_utc TEXT NOT NULL
);";

                command.ExecuteNonQuery();
            }
        }

        private static HashSet<int> LoadAppliedVersions(
            SQLiteConnection connection)
        {
            var versions = new HashSet<int>();

            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT version FROM schema_migrations ORDER BY version;";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        versions.Add(Convert.ToInt32(reader.GetValue(0)));
                    }
                }
            }

            return versions;
        }

        private static void ApplyMigration(
            SQLiteConnection connection,
            IMigration migration)
        {
            foreach (var statement in migration.Statements)
            {
                using (var command =
                    connection.CreateCommand())
                {
                    command.CommandText =
                        statement;

                    command.ExecuteNonQuery();
                }
            }

            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
INSERT INTO schema_migrations
(
    version,
    name,
    applied_utc
)
VALUES
(
    @version,
    @name,
    @appliedUtc
);";

                command.Parameters.AddWithValue(
                    "@version",
                    migration.Version);

                command.Parameters.AddWithValue(
                    "@name",
                    migration.Name);

                command.Parameters.AddWithValue(
                    "@appliedUtc",
                    DateTime.UtcNow.ToString(
                        "o",
                        CultureInfo.InvariantCulture));

                command.ExecuteNonQuery();
            }
}
    }
}
