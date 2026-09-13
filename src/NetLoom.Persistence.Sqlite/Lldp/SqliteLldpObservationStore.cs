using System;
using System.Collections.Generic;
using System.Globalization;
using NetLoom.Application.Observations.Lldp;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Lldp;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Lldp
{
    public sealed class SqliteLldpObservationStore
        : ILldpObservationStore
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        public SqliteLldpObservationStore(
            SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ??
                throw new ArgumentNullException(nameof(connectionFactory));
        }

        public void Save(LldpObservation lldpObservation)
        {
            if (lldpObservation == null)
            {
                throw new ArgumentNullException(
                    nameof(lldpObservation));
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            using (var transaction =
                connection.BeginTransaction())
            {
                using (var delete =
                    connection.CreateCommand())
                {
                    delete.Transaction = transaction;

                    delete.CommandText = @"
DELETE FROM lldp_observations
WHERE observation_id = @observationId;";

                    delete.Parameters.AddWithValue(
                        "@observationId",
                        lldpObservation.Observation.Id
                            .ToString("D"));

                    delete.ExecuteNonQuery();
                }

                using (var deleteLocal =
                    connection.CreateCommand())
                {
                    deleteLocal.Transaction = transaction;

                    deleteLocal.CommandText = @"
DELETE FROM lldp_local_system
WHERE observation_id = @observationId;";

                    deleteLocal.Parameters.AddWithValue(
                        "@observationId",
                        lldpObservation.Observation.Id
                            .ToString("D"));

                    deleteLocal.ExecuteNonQuery();
                }

                if (lldpObservation.LocalSystem != null)
                {
                    InsertLocalSystem(
                        connection,
                        transaction,
                        lldpObservation.Observation.Id,
                        lldpObservation.LocalSystem);
                }

                foreach (var neighbor in
                    lldpObservation.Neighbors)
                {
                    Insert(
                        connection,
                        transaction,
                        lldpObservation.Observation.Id,
                        neighbor);
                }

                transaction.Commit();
            }
        }

        public LldpObservation Get(Guid observationId)
        {
            if (observationId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Observation id is required.",
                    nameof(observationId));
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            {
                Observation observation;

                using (var command =
                    connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT
    source_address,
    captured_utc
FROM observations
WHERE observation_id = @id
  AND observation_kind = @kind;";

                    command.Parameters.AddWithValue(
                        "@id",
                        observationId.ToString("D"));

                    command.Parameters.AddWithValue(
                        "@kind",
                        ObservationKind.Lldp.ToString());

                    using (var reader =
                        command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        observation =
                            new Observation(
                                observationId,
                                ObservationKind.Lldp,
                                reader.GetString(0),
                                DateTime.Parse(
                                    reader.GetString(1),
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.RoundtripKind));
                    }
                }

                var localSystem =
                    LoadLocalSystem(
                        connection,
                        observationId);

                var neighbors =
                    LoadNeighbors(
                        connection,
                        observationId);

                return new LldpObservation(
                    observation,
                    neighbors,
                    localSystem);
            }
        }

        private static void InsertLocalSystem(
            System.Data.SQLite.SQLiteConnection connection,
            System.Data.SQLite.SQLiteTransaction transaction,
            Guid observationId,
            LldpLocalSystem localSystem)
        {
            using (var command =
                connection.CreateCommand())
            {
                command.Transaction = transaction;

                command.CommandText = @"
INSERT INTO lldp_local_system
(
    observation_id,
    chassis_id_subtype,
    chassis_id,
    system_name
)
VALUES
(
    @observationId,
    @chassisIdSubtype,
    @chassisId,
    @systemName
);";

                command.Parameters.AddWithValue(
                    "@observationId",
                    observationId.ToString("D"));

                AddNullable(
                    command,
                    "@chassisIdSubtype",
                    localSystem.ChassisIdSubtype);

                AddNullable(
                    command,
                    "@chassisId",
                    localSystem.ChassisId);

                AddNullable(
                    command,
                    "@systemName",
                    localSystem.SystemName);

                command.ExecuteNonQuery();
            }
        }

        private static LldpLocalSystem LoadLocalSystem(
            System.Data.SQLite.SQLiteConnection connection,
            Guid observationId)
        {
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    chassis_id_subtype,
    chassis_id,
    system_name
FROM lldp_local_system
WHERE observation_id = @id;";

                command.Parameters.AddWithValue(
                    "@id",
                    observationId.ToString("D"));

                using (var reader =
                    command.ExecuteReader())
                {
                    return reader.Read()
                        ? new LldpLocalSystem(
                            NullableInt(reader, 0),
                            NullableString(reader, 1),
                            NullableString(reader, 2))
                        : null;
                }
            }
        }

        private static void Insert(
            System.Data.SQLite.SQLiteConnection connection,
            System.Data.SQLite.SQLiteTransaction transaction,
            Guid observationId,
            LldpRemoteNeighbor neighbor)
        {
            using (var command =
                connection.CreateCommand())
            {
                command.Transaction = transaction;

                command.CommandText = @"
INSERT INTO lldp_observations
(
    observation_id,
    time_mark,
    local_port_number,
    remote_index,
    chassis_id_subtype,
    chassis_id,
    port_id_subtype,
    port_id,
    port_description,
    system_name,
    system_description,
    system_capabilities_supported,
    system_capabilities_enabled,
    local_port_present,
    local_port_id_subtype,
    local_port_id,
    local_port_description
)
VALUES
(
    @observationId,
    @timeMark,
    @localPortNumber,
    @remoteIndex,
    @chassisIdSubtype,
    @chassisId,
    @portIdSubtype,
    @portId,
    @portDescription,
    @systemName,
    @systemDescription,
    @capSupported,
    @capEnabled,
    @localPortPresent,
    @localPortIdSubtype,
    @localPortId,
    @localPortDescription
);";

                command.Parameters.AddWithValue(
                    "@observationId",
                    observationId.ToString("D"));
                command.Parameters.AddWithValue(
                    "@timeMark",
                    neighbor.TimeMark);
                command.Parameters.AddWithValue(
                    "@localPortNumber",
                    neighbor.LocalPortNumber);
                command.Parameters.AddWithValue(
                    "@remoteIndex",
                    neighbor.RemoteIndex);

                AddNullable(
                    command,
                    "@chassisIdSubtype",
                    neighbor.ChassisIdSubtype);
                AddNullable(
                    command,
                    "@chassisId",
                    neighbor.ChassisId);
                AddNullable(
                    command,
                    "@portIdSubtype",
                    neighbor.PortIdSubtype);
                AddNullable(
                    command,
                    "@portId",
                    neighbor.PortId);
                AddNullable(
                    command,
                    "@portDescription",
                    neighbor.PortDescription);
                AddNullable(
                    command,
                    "@systemName",
                    neighbor.SystemName);
                AddNullable(
                    command,
                    "@systemDescription",
                    neighbor.SystemDescription);
                AddNullable(
                    command,
                    "@capSupported",
                    neighbor.SystemCapabilitiesSupported);
                AddNullable(
                    command,
                    "@capEnabled",
                    neighbor.SystemCapabilitiesEnabled);

                command.Parameters.AddWithValue(
                    "@localPortPresent",
                    neighbor.LocalPort == null ? 0 : 1);

                AddNullable(
                    command,
                    "@localPortIdSubtype",
                    neighbor.LocalPort == null
                        ? null
                        : neighbor.LocalPort.PortIdSubtype);

                AddNullable(
                    command,
                    "@localPortId",
                    neighbor.LocalPort == null
                        ? null
                        : neighbor.LocalPort.PortId);

                AddNullable(
                    command,
                    "@localPortDescription",
                    neighbor.LocalPort == null
                        ? null
                        : neighbor.LocalPort.PortDescription);

                command.ExecuteNonQuery();
            }
        }

        private static IReadOnlyList<LldpRemoteNeighbor>
            LoadNeighbors(
                System.Data.SQLite.SQLiteConnection connection,
                Guid observationId)
        {
            var result =
                new List<LldpRemoteNeighbor>();

            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    time_mark,
    local_port_number,
    remote_index,
    chassis_id_subtype,
    chassis_id,
    port_id_subtype,
    port_id,
    port_description,
    system_name,
    system_description,
    system_capabilities_supported,
    system_capabilities_enabled,
    local_port_present,
    local_port_id_subtype,
    local_port_id,
    local_port_description
FROM lldp_observations
WHERE observation_id = @id
ORDER BY
    time_mark,
    local_port_number,
    remote_index;";

                command.Parameters.AddWithValue(
                    "@id",
                    observationId.ToString("D"));

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        LldpLocalPort localPort = null;

                        if (reader.GetInt32(12) != 0)
                        {
                            localPort =
                                new LldpLocalPort(
                                    reader.GetInt32(1),
                                    NullableInt(reader, 13),
                                    NullableString(reader, 14),
                                    NullableString(reader, 15));
                        }

                        result.Add(
                            new LldpRemoteNeighbor(
                                reader.GetInt64(0),
                                reader.GetInt32(1),
                                reader.GetInt32(2),
                                NullableInt(reader, 3),
                                NullableString(reader, 4),
                                NullableInt(reader, 5),
                                NullableString(reader, 6),
                                NullableString(reader, 7),
                                NullableString(reader, 8),
                                NullableString(reader, 9),
                                NullableString(reader, 10),
                                NullableString(reader, 11),
                                localPort));
                    }
                }
            }

            return result;
        }

        private static void AddNullable(
            System.Data.SQLite.SQLiteCommand command,
            string name,
            object value)
        {
            command.Parameters.AddWithValue(
                name,
                value ?? DBNull.Value);
        }

        private static int? NullableInt(
            System.Data.SQLite.SQLiteDataReader reader,
            int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? (int?)null
                : reader.GetInt32(ordinal);
        }

        private static string NullableString(
            System.Data.SQLite.SQLiteDataReader reader,
            int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? null
                : reader.GetString(ordinal);
        }
    }
}
