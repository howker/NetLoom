using System;
using System.Collections.Generic;
using System.Globalization;
using NetLoom.Application.Observations.Fdb;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Fdb;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Fdb
{
    public sealed class SqliteFdbObservationStore
        : IFdbObservationStore
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        public SqliteFdbObservationStore(
            SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ??
                throw new ArgumentNullException(nameof(connectionFactory));
        }

        public void Save(FdbObservation fdbObservation)
        {
            if (fdbObservation == null)
            {
                throw new ArgumentNullException(
                    nameof(fdbObservation));
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            using (var transaction =
                connection.BeginTransaction())
            {
                DeleteExisting(
                    connection,
                    transaction,
                    fdbObservation.Observation.Id);

                foreach (var mapping in
                    fdbObservation.BridgePortMappings)
                {
                    InsertMapping(
                        connection,
                        transaction,
                        fdbObservation.Observation.Id,
                        mapping);
                }

                foreach (var entry in
                    fdbObservation.Entries)
                {
                    InsertEntry(
                        connection,
                        transaction,
                        fdbObservation.Observation.Id,
                        entry);
                }

                transaction.Commit();
            }
        }

        public FdbObservation Get(Guid observationId)
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
                        ObservationKind.Fdb.ToString());

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
                                ObservationKind.Fdb,
                                reader.GetString(0),
                                DateTime.Parse(
                                    reader.GetString(1),
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.RoundtripKind));
                    }
                }

                return new FdbObservation(
                    observation,
                    LoadMappings(
                        connection,
                        observationId),
                    LoadEntries(
                        connection,
                        observationId));
            }
        }

        private static void DeleteExisting(
            System.Data.SQLite.SQLiteConnection connection,
            System.Data.SQLite.SQLiteTransaction transaction,
            Guid observationId)
        {
            using (var command =
                connection.CreateCommand())
            {
                command.Transaction = transaction;

                command.CommandText = @"
DELETE FROM bridge_port_mappings
WHERE observation_id = @id;

DELETE FROM fdb_observations
WHERE observation_id = @id;";

                command.Parameters.AddWithValue(
                    "@id",
                    observationId.ToString("D"));

                command.ExecuteNonQuery();
            }
        }

        private static void InsertMapping(
            System.Data.SQLite.SQLiteConnection connection,
            System.Data.SQLite.SQLiteTransaction transaction,
            Guid observationId,
            BridgePortMapping mapping)
        {
            using (var command =
                connection.CreateCommand())
            {
                command.Transaction = transaction;

                command.CommandText = @"
INSERT INTO bridge_port_mappings
(
    observation_id,
    bridge_port_index,
    if_index
)
VALUES
(
    @observationId,
    @bridgePortIndex,
    @ifIndex
);";

                command.Parameters.AddWithValue(
                    "@observationId",
                    observationId.ToString("D"));

                command.Parameters.AddWithValue(
                    "@bridgePortIndex",
                    mapping.BridgePortIndex);

                command.Parameters.AddWithValue(
                    "@ifIndex",
                    mapping.IfIndex);

                command.ExecuteNonQuery();
            }
        }

        private static void InsertEntry(
            System.Data.SQLite.SQLiteConnection connection,
            System.Data.SQLite.SQLiteTransaction transaction,
            Guid observationId,
            FdbEntry entry)
        {
            using (var command =
                connection.CreateCommand())
            {
                command.Transaction = transaction;

                command.CommandText = @"
INSERT INTO fdb_observations
(
    observation_id,
    mac_address,
    bridge_port_index,
    status
)
VALUES
(
    @observationId,
    @macAddress,
    @bridgePortIndex,
    @status
);";

                command.Parameters.AddWithValue(
                    "@observationId",
                    observationId.ToString("D"));

                command.Parameters.AddWithValue(
                    "@macAddress",
                    entry.MacAddress);

                AddNullable(
                    command,
                    "@bridgePortIndex",
                    entry.BridgePortIndex);

                AddNullable(
                    command,
                    "@status",
                    entry.Status);

                command.ExecuteNonQuery();
            }
        }

        private static IReadOnlyList<BridgePortMapping>
            LoadMappings(
                System.Data.SQLite.SQLiteConnection connection,
                Guid observationId)
        {
            var result =
                new List<BridgePortMapping>();

            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    bridge_port_index,
    if_index
FROM bridge_port_mappings
WHERE observation_id = @id
ORDER BY
    bridge_port_index,
    if_index;";

                command.Parameters.AddWithValue(
                    "@id",
                    observationId.ToString("D"));

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(
                            new BridgePortMapping(
                                reader.GetInt32(0),
                                reader.GetInt32(1)));
                    }
                }
            }

            return result;
        }

        private static IReadOnlyList<FdbEntry>
            LoadEntries(
                System.Data.SQLite.SQLiteConnection connection,
                Guid observationId)
        {
            var result =
                new List<FdbEntry>();

            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    mac_address,
    bridge_port_index,
    status
FROM fdb_observations
WHERE observation_id = @id
ORDER BY mac_address;";

                command.Parameters.AddWithValue(
                    "@id",
                    observationId.ToString("D"));

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(
                            new FdbEntry(
                                reader.GetString(0),
                                NullableInt(reader, 1),
                                NullableInt(reader, 2)));
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
    }
}
