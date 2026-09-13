using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using NetLoom.Application.Observations.Stp;
using NetLoom.Application.Topology;
using NetLoom.Domain.Locations;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Stp;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Topology
{
    public sealed class
        SqliteMaterializedTopologyReadSetReader :
        IMaterializedTopologyReadSetReader
    {
        private readonly SqliteConnectionFactory
            _connectionFactory;

        private readonly Action
            _afterDevicesRead;

        public SqliteMaterializedTopologyReadSetReader(
            SqliteConnectionFactory connectionFactory)
            : this(
                connectionFactory,
                null)
        {
        }

        internal SqliteMaterializedTopologyReadSetReader(
            SqliteConnectionFactory connectionFactory,
            Action afterDevicesRead)
        {
            _connectionFactory =
                connectionFactory ??
                throw new ArgumentNullException(
                    nameof(connectionFactory));

            _afterDevicesRead =
                afterDevicesRead;
        }

        public MaterializedTopologyReadSet Read(
            string stpInstanceId)
        {
            if (string.IsNullOrWhiteSpace(
                stpInstanceId))
            {
                throw new ArgumentException(
                    "STP instance id is required.",
                    nameof(stpInstanceId));
            }

            var normalizedInstanceId =
                stpInstanceId.Trim();

            using (var connection =
                _connectionFactory.OpenConnection())
            using (var transaction =
                connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted))
            {
                var devices =
                    ReadDevices(
                        connection,
                        transaction);

                _afterDevicesRead?.Invoke();

                var interfaces =
                    ReadInterfaces(
                        connection,
                        transaction);

                var links =
                    ReadPhysicalLinks(
                        connection,
                        transaction);

                var evidence =
                    ReadPhysicalLinkEvidence(
                        connection,
                        transaction);

                var locations =
                    ReadLocations(
                        connection,
                        transaction);

                var latestStp =
                    ReadLatestStp(
                        connection,
                        transaction,
                        normalizedInstanceId);

                transaction.Commit();

                return new MaterializedTopologyReadSet(
                    devices,
                    interfaces,
                    links,
                    evidence,
                    locations,
                    latestStp);
            }
        }

        private static IReadOnlyList<TopologyDevice>
            ReadDevices(
                SQLiteConnection connection,
                SQLiteTransaction transaction)
        {
            var result =
                new List<TopologyDevice>();

            using (var command =
                connection.CreateCommand())
            {
                command.Transaction =
                    transaction;

                command.CommandText = @"
SELECT
    id,
    location_id, custom_name, category,
    discovery_origin, monitoring_capability,
    vendor_override, model_override, notes,
    discovered_name, lldp_chassis_id,
    is_hidden, is_archived,
    first_seen_utc, last_seen_utc, last_resolved_utc
FROM devices
ORDER BY id;";

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(
                            new TopologyDevice(
                                Guid.Parse(
                                    reader.GetString(0)),
                                GuidNullable(
                                    reader,
                                    1),
                                StringNullable(
                                    reader,
                                    2),
                                Parse<DeviceCategory>(
                                    reader.GetString(3)),
                                Parse<DeviceDiscoveryOrigin>(
                                    reader.GetString(4)),
                                Parse<MonitoringCapability>(
                                    reader.GetString(5)),
                                StringNullable(
                                    reader,
                                    6),
                                StringNullable(
                                    reader,
                                    7),
                                StringNullable(
                                    reader,
                                    8),
                                reader.GetInt32(11) != 0,
                                reader.GetInt32(12) != 0,
                                DateNullable(
                                    reader,
                                    13),
                                DateNullable(
                                    reader,
                                    14),
                                DateNullable(
                                    reader,
                                    15),
                                StringNullable(
                                    reader,
                                    9),
                                StringNullable(
                                    reader,
                                    10)));
                    }
                }
            }

            return result;
        }

        private static IReadOnlyList<DeviceInterface>
            ReadInterfaces(
                SQLiteConnection connection,
                SQLiteTransaction transaction)
        {
            var result =
                new List<DeviceInterface>();

            using (var command =
                connection.CreateCommand())
            {
                command.Transaction =
                    transaction;

                command.CommandText = @"
SELECT
    id, device_id, if_index,
    if_name, if_descr, if_alias, custom_name,
    mac_address, admin_status, oper_status,
    speed_bps, media_type_auto, media_type_override,
    is_manual, is_hidden,
    first_seen_utc, last_seen_utc,
    lldp_port_id, lldp_port_description
FROM interfaces
ORDER BY id;";

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(
                            new DeviceInterface(
                                Guid.Parse(
                                    reader.GetString(0)),
                                Guid.Parse(
                                    reader.GetString(1)),
                                NullableInt(
                                    reader,
                                    2),
                                StringNullable(
                                    reader,
                                    3),
                                StringNullable(
                                    reader,
                                    4),
                                StringNullable(
                                    reader,
                                    5),
                                StringNullable(
                                    reader,
                                    6),
                                StringNullable(
                                    reader,
                                    7),
                                StringNullable(
                                    reader,
                                    8),
                                StringNullable(
                                    reader,
                                    9),
                                NullableLong(
                                    reader,
                                    10),
                                StringNullable(
                                    reader,
                                    11),
                                StringNullable(
                                    reader,
                                    12),
                                reader.GetInt32(13) != 0,
                                reader.GetInt32(14) != 0,
                                DateNullable(
                                    reader,
                                    15),
                                DateNullable(
                                    reader,
                                    16),
                                StringNullable(
                                    reader,
                                    17),
                                StringNullable(
                                    reader,
                                    18)));
                    }
                }
            }

            return result;
        }

        private static IReadOnlyList<PhysicalLink>
            ReadPhysicalLinks(
                SQLiteConnection connection,
                SQLiteTransaction transaction)
        {
            var result =
                new List<PhysicalLink>();

            using (var command =
                connection.CreateCommand())
            {
                command.Transaction =
                    transaction;

                command.CommandText = @"
SELECT
    id, link_key,
    device_a_id, interface_a_id,
    device_b_id, interface_b_id,
    strength, freshness,
    media_type_resolved, speed_bps_resolved,
    source_summary,
    first_seen_utc, last_seen_utc, last_confirmed_utc,
    resolver_version,
    is_hidden, is_archived,
    notes
FROM physical_links
ORDER BY id;";

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(
                            new PhysicalLink(
                                Guid.Parse(
                                    reader.GetString(0)),
                                Guid.Parse(
                                    reader.GetString(2)),
                                GuidNullable(
                                    reader,
                                    3),
                                Guid.Parse(
                                    reader.GetString(4)),
                                GuidNullable(
                                    reader,
                                    5),
                                Parse<PhysicalLinkStrength>(
                                    reader.GetString(6)),
                                Parse<PhysicalLinkFreshness>(
                                    reader.GetString(7)),
                                StringNullable(
                                    reader,
                                    8),
                                NullableLong(
                                    reader,
                                    9),
                                StringNullable(
                                    reader,
                                    10),
                                DateRequired(
                                    reader,
                                    11),
                                DateRequired(
                                    reader,
                                    12),
                                DateNullable(
                                    reader,
                                    13),
                                StringNullable(
                                    reader,
                                    14),
                                reader.GetInt32(15) != 0,
                                reader.GetInt32(16) != 0,
                                StringNullable(
                                    reader,
                                    17)));
                    }
                }
            }

            return result;
        }

        private static IReadOnlyList<PhysicalLinkEvidence>
            ReadPhysicalLinkEvidence(
                SQLiteConnection connection,
                SQLiteTransaction transaction)
        {
            var result =
                new List<PhysicalLinkEvidence>();

            using (var command =
                connection.CreateCommand())
            {
                command.Transaction =
                    transaction;

                command.CommandText = @"
SELECT
    physical_link_id,
    evidence_kind,
    evidence_strength,
    source_address,
    slot_discriminator,
    observation_id,
    captured_utc,
    detail
FROM physical_link_evidence_current
ORDER BY
    physical_link_id,
    evidence_kind,
    source_address,
    slot_discriminator;";

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(
                            new PhysicalLinkEvidence(
                                Guid.Parse(
                                    reader.GetString(0)),
                                Parse<PhysicalLinkEvidenceKind>(
                                    reader.GetString(1)),
                                Parse<PhysicalLinkEvidenceStrength>(
                                    reader.GetString(2)),
                                reader.GetString(3),
                                reader.GetString(4),
                                GuidNullable(
                                    reader,
                                    5),
                                DateNullable(
                                    reader,
                                    6),
                                StringNullable(
                                    reader,
                                    7)));
                    }
                }
            }

            return result;
        }

        private static IReadOnlyList<Location>
            ReadLocations(
                SQLiteConnection connection,
                SQLiteTransaction transaction)
        {
            var result =
                new List<Location>();

            using (var command =
                connection.CreateCommand())
            {
                command.Transaction =
                    transaction;

                command.CommandText = @"
SELECT
    location_id,
    parent_location_id,
    name,
    description
FROM locations
ORDER BY name, location_id;";

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(
                            new Location(
                                Guid.Parse(
                                    reader.GetString(0)),
                                GuidNullable(
                                    reader,
                                    1),
                                reader.GetString(2),
                                StringNullable(
                                    reader,
                                    3)));
                    }
                }
            }

            return result;
        }

        private static IReadOnlyList<BoundStpObservation>
            ReadLatestStp(
                SQLiteConnection connection,
                SQLiteTransaction transaction,
                string instanceId)
        {
            var selected =
                new List<LatestObservationId>();

            using (var command =
                connection.CreateCommand())
            {
                command.Transaction =
                    transaction;

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
                    instanceId);

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
                    ReadStpObservation(
                        connection,
                        transaction,
                        item.ObservationId,
                        instanceId);

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

        private static StpObservation
            ReadStpObservation(
                SQLiteConnection connection,
                SQLiteTransaction transaction,
                Guid observationId,
                string instanceId)
        {
            Observation observation;

            using (var command =
                connection.CreateCommand())
            {
                command.Transaction =
                    transaction;

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
                command.Transaction =
                    transaction;

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
                        NullableInt(
                            reader,
                            0),
                        StringNullable(
                            reader,
                            1),
                        NullableLong(
                            reader,
                            2),
                        NullableInt(
                            reader,
                            3),
                        NullableInt(
                            reader,
                            4),
                        LoadPorts(
                            connection,
                            transaction,
                            observationId,
                            instanceId));
                }
            }
        }

        private static IReadOnlyList<StpPortState>
            LoadPorts(
                SQLiteConnection connection,
                SQLiteTransaction transaction,
                Guid observationId,
                string instanceId)
        {
            var result =
                new List<StpPortState>();

            using (var command =
                connection.CreateCommand())
            {
                command.Transaction =
                    transaction;

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
                                NullableInt(
                                    reader,
                                    1),
                                NullableInt(
                                    reader,
                                    2),
                                NullableInt(
                                    reader,
                                    3),
                                NullableInt(
                                    reader,
                                    4),
                                NullableLong(
                                    reader,
                                    5),
                                StringNullable(
                                    reader,
                                    6),
                                NullableLong(
                                    reader,
                                    7),
                                StringNullable(
                                    reader,
                                    8),
                                StringNullable(
                                    reader,
                                    9),
                                NullableLong(
                                    reader,
                                    10)));
                    }
                }
            }

            return result;
        }

        private static string StringNullable(
            SQLiteDataReader reader,
            int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? null
                : reader.GetString(ordinal);
        }

        private static Guid? GuidNullable(
            SQLiteDataReader reader,
            int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? (Guid?)null
                : Guid.Parse(
                    reader.GetString(ordinal));
        }

        private static int? NullableInt(
            SQLiteDataReader reader,
            int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? (int?)null
                : reader.GetInt32(ordinal);
        }

        private static long? NullableLong(
            SQLiteDataReader reader,
            int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? (long?)null
                : reader.GetInt64(ordinal);
        }

        private static DateTime DateRequired(
            SQLiteDataReader reader,
            int ordinal)
        {
            return DateTime.Parse(
                reader.GetString(ordinal),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
        }

        private static DateTime? DateNullable(
            SQLiteDataReader reader,
            int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? (DateTime?)null
                : DateRequired(
                    reader,
                    ordinal);
        }

        private static T Parse<T>(
            string value)
        {
            return (T)Enum.Parse(
                typeof(T),
                value);
        }

        private sealed class LatestObservationId
        {
            public LatestObservationId(
                Guid deviceId,
                Guid observationId)
            {
                DeviceId =
                    deviceId;

                ObservationId =
                    observationId;
            }

            public Guid DeviceId { get; }

            public Guid ObservationId { get; }
        }
    }
}
