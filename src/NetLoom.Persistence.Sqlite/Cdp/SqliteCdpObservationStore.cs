using System;
using System.Collections.Generic;
using System.Globalization;
using NetLoom.Application.Observations.Cdp;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Cdp;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Cdp
{
    public sealed class SqliteCdpObservationStore
        : ICdpObservationStore
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        public SqliteCdpObservationStore(
            SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ??
                throw new ArgumentNullException(nameof(connectionFactory));
        }

        public void Save(CdpObservation cdpObservation)
        {
            if (cdpObservation == null)
            {
                throw new ArgumentNullException(
                    nameof(cdpObservation));
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
DELETE FROM cdp_observations
WHERE observation_id = @observationId;";

                    delete.Parameters.AddWithValue(
                        "@observationId",
                        cdpObservation.Observation.Id
                            .ToString("D"));

                    delete.ExecuteNonQuery();
                }

                foreach (var neighbor in
                    cdpObservation.Neighbors)
                {
                    Insert(
                        connection,
                        transaction,
                        cdpObservation.Observation.Id,
                        neighbor);
                }

                transaction.Commit();
            }
        }

        public CdpObservation Get(Guid observationId)
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
                        ObservationKind.Cdp.ToString());

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
                                ObservationKind.Cdp,
                                reader.GetString(0),
                                DateTime.Parse(
                                    reader.GetString(1),
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.RoundtripKind));
                    }
                }

                return new CdpObservation(
                    observation,
                    LoadNeighbors(
                        connection,
                        observationId));
            }
        }

        private static void Insert(
            System.Data.SQLite.SQLiteConnection connection,
            System.Data.SQLite.SQLiteTransaction transaction,
            Guid observationId,
            CdpRemoteNeighbor neighbor)
        {
            using (var command =
                connection.CreateCommand())
            {
                command.Transaction = transaction;

                command.CommandText = @"
INSERT INTO cdp_observations
(
    observation_id,
    cache_if_index,
    device_index,
    address_type,
    address,
    version,
    device_id,
    device_port,
    platform,
    capabilities,
    native_vlan,
    duplex,
    system_name,
    system_object_id,
    primary_management_address_type,
    primary_management_address,
    physical_location,
    last_change
)
VALUES
(
    @observationId,
    @cacheIfIndex,
    @deviceIndex,
    @addressType,
    @address,
    @version,
    @deviceId,
    @devicePort,
    @platform,
    @capabilities,
    @nativeVlan,
    @duplex,
    @systemName,
    @systemObjectId,
    @primaryManagementAddressType,
    @primaryManagementAddress,
    @physicalLocation,
    @lastChange
);";

                command.Parameters.AddWithValue(
                    "@observationId",
                    observationId.ToString("D"));

                command.Parameters.AddWithValue(
                    "@cacheIfIndex",
                    neighbor.CacheIfIndex);

                command.Parameters.AddWithValue(
                    "@deviceIndex",
                    neighbor.DeviceIndex);

                AddNullable(command, "@addressType", neighbor.AddressType);
                AddNullable(command, "@address", neighbor.Address);
                AddNullable(command, "@version", neighbor.Version);
                AddNullable(command, "@deviceId", neighbor.DeviceId);
                AddNullable(command, "@devicePort", neighbor.DevicePort);
                AddNullable(command, "@platform", neighbor.Platform);
                AddNullable(command, "@capabilities", neighbor.Capabilities);
                AddNullable(command, "@nativeVlan", neighbor.NativeVlan);
                AddNullable(command, "@duplex", neighbor.Duplex);
                AddNullable(command, "@systemName", neighbor.SystemName);
                AddNullable(command, "@systemObjectId", neighbor.SystemObjectId);

                AddNullable(
                    command,
                    "@primaryManagementAddressType",
                    neighbor.PrimaryManagementAddressType);

                AddNullable(
                    command,
                    "@primaryManagementAddress",
                    neighbor.PrimaryManagementAddress);

                AddNullable(
                    command,
                    "@physicalLocation",
                    neighbor.PhysicalLocation);

                AddNullable(
                    command,
                    "@lastChange",
                    neighbor.LastChange);

                command.ExecuteNonQuery();
            }
        }

        private static IReadOnlyList<CdpRemoteNeighbor>
            LoadNeighbors(
                System.Data.SQLite.SQLiteConnection connection,
                Guid observationId)
        {
            var result =
                new List<CdpRemoteNeighbor>();

            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    cache_if_index,
    device_index,
    address_type,
    address,
    version,
    device_id,
    device_port,
    platform,
    capabilities,
    native_vlan,
    duplex,
    system_name,
    system_object_id,
    primary_management_address_type,
    primary_management_address,
    physical_location,
    last_change
FROM cdp_observations
WHERE observation_id = @id
ORDER BY
    cache_if_index,
    device_index;";

                command.Parameters.AddWithValue(
                    "@id",
                    observationId.ToString("D"));

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(
                            new CdpRemoteNeighbor(
                                reader.GetInt32(0),
                                reader.GetInt32(1),
                                NullableInt(reader, 2),
                                NullableString(reader, 3),
                                NullableString(reader, 4),
                                NullableString(reader, 5),
                                NullableString(reader, 6),
                                NullableString(reader, 7),
                                NullableString(reader, 8),
                                NullableInt(reader, 9),
                                NullableInt(reader, 10),
                                NullableString(reader, 11),
                                NullableString(reader, 12),
                                NullableInt(reader, 13),
                                NullableString(reader, 14),
                                NullableString(reader, 15),
                                NullableLong(reader, 16)));
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

        private static long? NullableLong(
            System.Data.SQLite.SQLiteDataReader reader,
            int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? (long?)null
                : reader.GetInt64(ordinal);
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
