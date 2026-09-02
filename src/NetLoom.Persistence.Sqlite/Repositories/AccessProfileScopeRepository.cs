using System;
using NetLoom.Domain.Access;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Repositories
{
    public sealed class AccessProfileScopeRepository
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        public AccessProfileScopeRepository(
            SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ??
                throw new ArgumentNullException(nameof(connectionFactory));
        }

        public void AddTarget(
            Guid accessProfileId,
            AccessTargetKind kind,
            string value,
            int priority)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Target value is required.",
                    nameof(value));
            }

            if (priority < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(priority));
            }

            using (var connection = _connectionFactory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
INSERT OR IGNORE INTO access_profile_targets
(
    target_id,
    access_profile_id,
    target_kind,
    target_value,
    priority
)
VALUES
(
    @targetId,
    @accessProfileId,
    @targetKind,
    @targetValue,
    @priority
);";

                command.Parameters.AddWithValue(
                    "@targetId",
                    Guid.NewGuid().ToString("D"));
                command.Parameters.AddWithValue(
                    "@accessProfileId",
                    accessProfileId.ToString("D"));
                command.Parameters.AddWithValue(
                    "@targetKind",
                    kind.ToString());
                command.Parameters.AddWithValue(
                    "@targetValue",
                    value);
                command.Parameters.AddWithValue(
                    "@priority",
                    priority);

                command.ExecuteNonQuery();
            }
        }

        public void AddExclusion(
            Guid accessProfileId,
            AccessTargetKind kind,
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Exclusion value is required.",
                    nameof(value));
            }

            using (var connection = _connectionFactory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
INSERT OR IGNORE INTO access_profile_exclusions
(
    exclusion_id,
    access_profile_id,
    target_kind,
    target_value
)
VALUES
(
    @exclusionId,
    @accessProfileId,
    @targetKind,
    @targetValue
);";

                command.Parameters.AddWithValue(
                    "@exclusionId",
                    Guid.NewGuid().ToString("D"));
                command.Parameters.AddWithValue(
                    "@accessProfileId",
                    accessProfileId.ToString("D"));
                command.Parameters.AddWithValue(
                    "@targetKind",
                    kind.ToString());
                command.Parameters.AddWithValue(
                    "@targetValue",
                    value);

                command.ExecuteNonQuery();
            }
        }

        public void AddTcpPort(
            Guid accessProfileId,
            int port)
        {
            if (port < 1 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(port));
            }

            using (var connection = _connectionFactory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
INSERT OR IGNORE INTO access_profile_tcp_ports
(
    access_profile_id,
    port
)
VALUES
(
    @accessProfileId,
    @port
);";

                command.Parameters.AddWithValue(
                    "@accessProfileId",
                    accessProfileId.ToString("D"));
                command.Parameters.AddWithValue(
                    "@port",
                    port);

                command.ExecuteNonQuery();
            }
        }
    }
}
