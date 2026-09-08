using System;
using System.Collections.Generic;
using System.Globalization;
using NetLoom.Application.Observations.Stp;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Stp;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Stp
{
    public sealed class SqliteStpObservationStore :
        IStpObservationStore,
        ILatestStpObservationReader
    {
        private readonly SqliteConnectionFactory
            _connectionFactory;

        public SqliteStpObservationStore(
            SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory =
                connectionFactory ??
                throw new ArgumentNullException(
                    nameof(connectionFactory));
        }

        public void Save(
            StpObservation stpObservation)
        {
            if (stpObservation == null)
            {
                throw new ArgumentNullException(
                    nameof(stpObservation));
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            using (var transaction =
                connection.BeginTransaction())
            {
                DeleteExisting(
                    connection,
                    transaction,
                    stpObservation.Observation.Id);

                InsertObservation(
                    connection,
                    transaction,
                    stpObservation);

                foreach (var port in
                    stpObservation.Ports)
                {
                    InsertPort(
                        connection,
                        transaction,
                        stpObservation.Observation.Id,
                        stpObservation.InstanceId,
                        port);
                }

                transaction.Commit();
            }
        }

        public StpObservation Get(
            Guid observationId)
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
                        ObservationKind.Stp.ToString());

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
                                ObservationKind.Stp,
                                reader.GetString(0),
                                DateTime.Parse(
                                    reader.GetString(1),
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.RoundtripKind));
                    }
                }

                using (var command =
                    connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT
    instance_id,
    protocol_specification,
    designated_root,
    root_cost,
    root_bridge_port_index,
    root_if_index
FROM stp_observations
WHERE observation_id = @id
ORDER BY instance_id
LIMIT 1;";

                    command.Parameters.AddWithValue(
                        "@id",
                        observationId.ToString("D"));

                    using (var reader =
                        command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        var instanceId =
                            reader.GetString(0);

                        var protocol =
                            NullableInt(reader, 1);

                        var designatedRoot =
                            NullableString(reader, 2);

                        var rootCost =
                            NullableLong(reader, 3);

                        var rootBridgePort =
                            NullableInt(reader, 4);

                        var rootIfIndex =
                            NullableInt(reader, 5);

                        return new StpObservation(
                            observation,
                            instanceId,
                            protocol,
                            designatedRoot,
                            rootCost,
                            rootBridgePort,
                            rootIfIndex,
                            LoadPorts(
                                connection,
                                observationId,
                                instanceId));
                    }
                }
            }
        }

        public IReadOnlyList<BoundStpObservation>
            GetLatest(
                string instanceId)
        {
            if (string.IsNullOrWhiteSpace(
                instanceId))
            {
                throw new ArgumentException(
                    "STP instance id is required.",
                    nameof(instanceId));
            }

            var normalizedInstanceId =
                instanceId.Trim();

            var selected =
                new List<LatestObservationId>();

            using (var connection =
                _connectionFactory.OpenConnection())
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    b.device_id,
    s.observation_id
FROM observation_device_bindings b
JOIN observations o
    ON o.observation_id = b.observation_id
JOIN stp_observations s
    ON s.observation_id = o.observation_id
WHERE o.observation_kind = @kind
  AND s.instance_id = @instanceId
ORDER BY
    b.device_id,
    o.captured_utc DESC,
    s.observation_id DESC;";

                command.Parameters.AddWithValue(
                    "@kind",
                    ObservationKind.Stp.ToString());

                command.Parameters.AddWithValue(
                    "@instanceId",
                    normalizedInstanceId);

                var seenDevices =
                    new HashSet<Guid>();

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var deviceId =
                            Guid.Parse(
                                reader.GetString(0));

                        if (!seenDevices.Add(
                            deviceId))
                        {
                            continue;
                        }

                        selected.Add(
                            new LatestObservationId(
                                deviceId,
                                Guid.Parse(
                                    reader.GetString(1))));
                    }
                }
            }

            var result =
                new List<BoundStpObservation>();

            foreach (var item in selected)
            {
                var observation =
                    Get(
                        item.ObservationId,
                        normalizedInstanceId);

                if (observation == null)
                {
                    continue;
                }

                result.Add(
                    new BoundStpObservation(
                        item.DeviceId,
                        observation));
            }

            return result;
        }

        private StpObservation Get(
            Guid observationId,
            string instanceId)
        {
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
                        ObservationKind.Stp.ToString());

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
                                ObservationKind.Stp,
                                reader.GetString(0),
                                DateTime.Parse(
                                    reader.GetString(1),
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.RoundtripKind));
                    }
                }

                using (var command =
                    connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT
    protocol_specification,
    designated_root,
    root_cost,
    root_bridge_port_index,
    root_if_index
FROM stp_observations
WHERE observation_id = @id
  AND instance_id = @instanceId
LIMIT 1;";

                    command.Parameters.AddWithValue(
                        "@id",
                        observationId.ToString("D"));

                    command.Parameters.AddWithValue(
                        "@instanceId",
                        instanceId);

                    using (var reader =
                        command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        return new StpObservation(
                            observation,
                            instanceId,
                            NullableInt(reader, 0),
                            NullableString(reader, 1),
                            NullableLong(reader, 2),
                            NullableInt(reader, 3),
                            NullableInt(reader, 4),
                            LoadPorts(
                                connection,
                                observationId,
                                instanceId));
                    }
                }
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
DELETE FROM stp_port_states
WHERE observation_id = @id;

DELETE FROM stp_observations
WHERE observation_id = @id;";

                command.Parameters.AddWithValue(
                    "@id",
                    observationId.ToString("D"));

                command.ExecuteNonQuery();
            }
        }

        private static void InsertObservation(
            System.Data.SQLite.SQLiteConnection connection,
            System.Data.SQLite.SQLiteTransaction transaction,
            StpObservation observation)
        {
            using (var command =
                connection.CreateCommand())
            {
                command.Transaction = transaction;

                command.CommandText = @"
INSERT INTO stp_observations
(
    observation_id,
    instance_id,
    protocol_specification,
    designated_root,
    root_cost,
    root_bridge_port_index,
    root_if_index
)
VALUES
(
    @observationId,
    @instanceId,
    @protocol,
    @designatedRoot,
    @rootCost,
    @rootBridgePort,
    @rootIfIndex
);";

                command.Parameters.AddWithValue(
                    "@observationId",
                    observation.Observation.Id.ToString("D"));

                command.Parameters.AddWithValue(
                    "@instanceId",
                    observation.InstanceId);

                AddNullable(
                    command,
                    "@protocol",
                    observation.ProtocolSpecification);

                AddNullable(
                    command,
                    "@designatedRoot",
                    observation.DesignatedRoot);

                AddNullable(
                    command,
                    "@rootCost",
                    observation.RootCost);

                AddNullable(
                    command,
                    "@rootBridgePort",
                    observation.RootPortBridgePortIndex);

                AddNullable(
                    command,
                    "@rootIfIndex",
                    observation.RootPortIfIndex);

                command.ExecuteNonQuery();
            }
        }

        private static void InsertPort(
            System.Data.SQLite.SQLiteConnection connection,
            System.Data.SQLite.SQLiteTransaction transaction,
            Guid observationId,
            string instanceId,
            StpPortState port)
        {
            using (var command =
                connection.CreateCommand())
            {
                command.Transaction = transaction;

                command.CommandText = @"
INSERT INTO stp_port_states
(
    observation_id,
    instance_id,
    bridge_port_index,
    if_index,
    priority,
    state,
    enabled,
    path_cost,
    designated_root,
    designated_cost,
    designated_bridge,
    designated_port,
    forward_transitions
)
VALUES
(
    @observationId,
    @instanceId,
    @bridgePortIndex,
    @ifIndex,
    @priority,
    @state,
    @enabled,
    @pathCost,
    @designatedRoot,
    @designatedCost,
    @designatedBridge,
    @designatedPort,
    @forwardTransitions
);";

                command.Parameters.AddWithValue(
                    "@observationId",
                    observationId.ToString("D"));

                command.Parameters.AddWithValue(
                    "@instanceId",
                    instanceId);

                command.Parameters.AddWithValue(
                    "@bridgePortIndex",
                    port.BridgePortIndex);

                AddNullable(
                    command,
                    "@ifIndex",
                    port.IfIndex);

                AddNullable(
                    command,
                    "@priority",
                    port.Priority);

                AddNullable(
                    command,
                    "@state",
                    port.State);

                AddNullable(
                    command,
                    "@enabled",
                    port.Enabled);

                AddNullable(
                    command,
                    "@pathCost",
                    port.PathCost);

                AddNullable(
                    command,
                    "@designatedRoot",
                    port.DesignatedRoot);

                AddNullable(
                    command,
                    "@designatedCost",
                    port.DesignatedCost);

                AddNullable(
                    command,
                    "@designatedBridge",
                    port.DesignatedBridge);

                AddNullable(
                    command,
                    "@designatedPort",
                    port.DesignatedPort);

                AddNullable(
                    command,
                    "@forwardTransitions",
                    port.ForwardTransitions);

                command.ExecuteNonQuery();
            }
        }

        private static IReadOnlyList<StpPortState>
            LoadPorts(
                System.Data.SQLite.SQLiteConnection connection,
                Guid observationId,
                string instanceId)
        {
            var result =
                new List<StpPortState>();

            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    bridge_port_index,
    if_index,
    priority,
    state,
    enabled,
    path_cost,
    designated_root,
    designated_cost,
    designated_bridge,
    designated_port,
    forward_transitions
FROM stp_port_states
WHERE observation_id = @id
  AND instance_id = @instanceId
ORDER BY bridge_port_index;";

                command.Parameters.AddWithValue(
                    "@id",
                    observationId.ToString("D"));

                command.Parameters.AddWithValue(
                    "@instanceId",
                    instanceId);

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(
                            new StpPortState(
                                reader.GetInt32(0),
                                NullableInt(reader, 1),
                                NullableInt(reader, 2),
                                NullableInt(reader, 3),
                                NullableInt(reader, 4),
                                NullableLong(reader, 5),
                                NullableString(reader, 6),
                                NullableLong(reader, 7),
                                NullableString(reader, 8),
                                NullableString(reader, 9),
                                NullableLong(reader, 10)));
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


        private sealed class LatestObservationId
        {
            public LatestObservationId(
                Guid deviceId,
                Guid observationId)
            {
                DeviceId = deviceId;
                ObservationId = observationId;
            }

            public Guid DeviceId { get; }

            public Guid ObservationId { get; }
        }
    }
}
