using System;
using System.Collections.Generic;
using System.Globalization;
using NetLoom.Domain.Access;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Repositories
{
    public sealed class AccessProfileRepository
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        public AccessProfileRepository(SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ??
                throw new ArgumentNullException(nameof(connectionFactory));
        }

        public void Save(AccessProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var now = DateTime.UtcNow.ToString(
                "o",
                CultureInfo.InvariantCulture);

            using (var connection = _connectionFactory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
INSERT INTO access_profiles
(
    access_profile_id,
    name,
    is_enabled,
    snmp_version,
    snmp_username,
    created_utc,
    updated_utc
)
VALUES
(
    @id,
    @name,
    @isEnabled,
    @snmpVersion,
    @snmpUsername,
    @createdUtc,
    @updatedUtc
)
ON CONFLICT(access_profile_id) DO UPDATE SET
    name = excluded.name,
    is_enabled = excluded.is_enabled,
    snmp_version = excluded.snmp_version,
    snmp_username = excluded.snmp_username,
    updated_utc = excluded.updated_utc;";

                command.Parameters.AddWithValue("@id", profile.Id.ToString("D"));
                command.Parameters.AddWithValue("@name", profile.Name);
                command.Parameters.AddWithValue(
                    "@isEnabled",
                    profile.IsEnabled ? 1 : 0);
                command.Parameters.AddWithValue(
                    "@snmpVersion",
                    profile.SnmpVersion.ToString());
                command.Parameters.AddWithValue(
                    "@snmpUsername",
                    (object)profile.SnmpUsername ?? DBNull.Value);
                command.Parameters.AddWithValue("@createdUtc", now);
                command.Parameters.AddWithValue("@updatedUtc", now);

                command.ExecuteNonQuery();
            }
        }

        public void Delete(Guid id)
        {
            using (var connection = _connectionFactory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
DELETE FROM access_profiles
WHERE access_profile_id = @id;";

                command.Parameters.AddWithValue("@id", id.ToString("D"));
                command.ExecuteNonQuery();
            }
        }

        public IReadOnlyList<AccessProfile> GetEnabled()
        {
            var profiles =
                new List<AccessProfile>();

            using (var connection = _connectionFactory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    access_profile_id,
    name,
    snmp_version,
    snmp_username
FROM access_profiles
WHERE is_enabled = 1
ORDER BY name COLLATE NOCASE, access_profile_id;";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        profiles.Add(
                            new AccessProfile(
                                Guid.Parse(
                                    reader.GetString(0)),
                                reader.GetString(1),
                                true,
                                (SnmpVersion)Enum.Parse(
                                    typeof(SnmpVersion),
                                    reader.GetString(2)),
                                reader.IsDBNull(3)
                                    ? null
                                    : reader.GetString(3)));
                    }
                }
            }

            return profiles;
        }

        public AccessProfile Get(Guid id)
        {
            using (var connection = _connectionFactory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    name,
    is_enabled,
    snmp_version,
    snmp_username
FROM access_profiles
WHERE access_profile_id = @id;";

                command.Parameters.AddWithValue("@id", id.ToString("D"));

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    return new AccessProfile(
                        id,
                        reader.GetString(0),
                        reader.GetInt32(1) != 0,
                        (SnmpVersion)Enum.Parse(
                            typeof(SnmpVersion),
                            reader.GetString(2)),
                        reader.IsDBNull(3)
                            ? null
                            : reader.GetString(3));
                }
            }
        }
    }
}
