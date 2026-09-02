using System;
using System.Collections.Generic;
using System.Globalization;
using NetLoom.Application.Observations.Arp;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Arp;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Arp
{
    public sealed class SqliteArpObservationStore
        : IArpObservationStore
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        public SqliteArpObservationStore(
            SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ??
                throw new ArgumentNullException(nameof(connectionFactory));
        }

        public void Save(ArpObservation arpObservation)
        {
            if (arpObservation == null)
            {
                throw new ArgumentNullException(
                    nameof(arpObservation));
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
DELETE FROM arp_observations
WHERE observation_id = @observationId;";

                    delete.Parameters.AddWithValue(
                        "@observationId",
                        arpObservation.Observation.Id
                            .ToString("D"));

                    delete.ExecuteNonQuery();
                }

                foreach (var entry in
                    arpObservation.Entries)
                {
                    Insert(
                        connection,
                        transaction,
                        arpObservation.Observation.Id,
                        entry);
                }

                transaction.Commit();
            }
        }

        public ArpObservation Get(Guid observationId)
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
                        ObservationKind.Arp.ToString());

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
                                ObservationKind.Arp,
                                reader.GetString(0),
                                DateTime.Parse(
                                    reader.GetString(1),
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.RoundtripKind));
                    }
                }

                return new ArpObservation(
                    observation,
                    LoadEntries(
                        connection,
                        observationId));
            }
        }

        private static void Insert(
            System.Data.SQLite.SQLiteConnection connection,
            System.Data.SQLite.SQLiteTransaction transaction,
            Guid observationId,
            ArpEntry entry)
        {
            using (var command =
                connection.CreateCommand())
            {
                command.Transaction = transaction;

                command.CommandText = @"
INSERT INTO arp_observations
(
    observation_id,
    if_index,
    address_type,
    ip_address,
    physical_address,
    entry_type,
    entry_state,
    table_kind
)
VALUES
(
    @observationId,
    @ifIndex,
    @addressType,
    @ipAddress,
    @physicalAddress,
    @entryType,
    @entryState,
    @tableKind
);";

                command.Parameters.AddWithValue(
                    "@observationId",
                    observationId.ToString("D"));

                command.Parameters.AddWithValue(
                    "@ifIndex",
                    entry.IfIndex);

                command.Parameters.AddWithValue(
                    "@addressType",
                    entry.AddressType);

                command.Parameters.AddWithValue(
                    "@ipAddress",
                    entry.IpAddress);

                AddNullable(
                    command,
                    "@physicalAddress",
                    entry.PhysicalAddress);

                AddNullable(
                    command,
                    "@entryType",
                    entry.Type);

                AddNullable(
                    command,
                    "@entryState",
                    entry.State);

                command.Parameters.AddWithValue(
                    "@tableKind",
                    (int)entry.TableKind);

                command.ExecuteNonQuery();
            }
        }

        private static IReadOnlyList<ArpEntry>
            LoadEntries(
                System.Data.SQLite.SQLiteConnection connection,
                Guid observationId)
        {
            var result =
                new List<ArpEntry>();

            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    if_index,
    address_type,
    ip_address,
    physical_address,
    entry_type,
    entry_state,
    table_kind
FROM arp_observations
WHERE observation_id = @id
ORDER BY
    if_index,
    address_type,
    ip_address,
    table_kind;";

                command.Parameters.AddWithValue(
                    "@id",
                    observationId.ToString("D"));

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(
                            new ArpEntry(
                                reader.GetInt32(0),
                                reader.GetInt32(1),
                                reader.GetString(2),
                                NullableString(reader, 3),
                                NullableInt(reader, 4),
                                NullableInt(reader, 5),
                                (ArpTableKind)
                                    reader.GetInt32(6)));
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
