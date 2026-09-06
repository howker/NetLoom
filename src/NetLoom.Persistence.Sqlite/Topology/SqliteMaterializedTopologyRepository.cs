using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using NetLoom.Application.Topology;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Topology
{
    public sealed class SqliteMaterializedTopologyRepository :
        IMaterializedTopologyRepository
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        public SqliteMaterializedTopologyRepository(
            SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ??
                throw new ArgumentNullException(
                    nameof(connectionFactory));
        }

        public void SaveDevice(TopologyDevice device)
        {
            if (device == null)
            {
                throw new ArgumentNullException(nameof(device));
            }

            using (var connection = _connectionFactory.OpenConnection())
            {
                ProtectManualDevice(connection, device);

                var now = FormatUtc(DateTime.UtcNow);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
INSERT INTO devices
(
    id, location_id, custom_name, category,
    discovery_origin, monitoring_capability,
    vendor_override, model_override, notes,
    is_hidden, is_archived,
    first_seen_utc, last_seen_utc, last_resolved_utc,
    created_at_utc, updated_at_utc
)
VALUES
(
    @id, @locationId, @customName, @category,
    @origin, @capability,
    @vendor, @model, @notes,
    @hidden, @archived,
    @firstSeen, @lastSeen, @lastResolved,
    @created, @updated
)
ON CONFLICT(id) DO UPDATE SET
    location_id = excluded.location_id,
    custom_name = excluded.custom_name,
    category = excluded.category,
    discovery_origin = excluded.discovery_origin,
    monitoring_capability = excluded.monitoring_capability,
    vendor_override = excluded.vendor_override,
    model_override = excluded.model_override,
    notes = excluded.notes,
    is_hidden = excluded.is_hidden,
    is_archived = excluded.is_archived,
    first_seen_utc = excluded.first_seen_utc,
    last_seen_utc = excluded.last_seen_utc,
    last_resolved_utc = excluded.last_resolved_utc,
    updated_at_utc = excluded.updated_at_utc;";

                    Add(command, "@id", device.Id.ToString("D"));
                    AddGuid(command, "@locationId", device.LocationId);
                    Add(command, "@customName", device.CustomName);
                    Add(command, "@category", device.Category.ToString());
                    Add(command, "@origin", device.DiscoveryOrigin.ToString());
                    Add(command, "@capability", device.MonitoringCapability.ToString());
                    Add(command, "@vendor", device.VendorOverride);
                    Add(command, "@model", device.ModelOverride);
                    Add(command, "@notes", device.Notes);
                    Add(command, "@hidden", device.IsHidden ? 1 : 0);
                    Add(command, "@archived", device.IsArchived ? 1 : 0);
                    AddDate(command, "@firstSeen", device.FirstSeenUtc);
                    AddDate(command, "@lastSeen", device.LastSeenUtc);
                    AddDate(command, "@lastResolved", device.LastResolvedUtc);
                    Add(command, "@created", now);
                    Add(command, "@updated", now);

                    command.ExecuteNonQuery();
                }
            }
        }

        public void SaveInterface(DeviceInterface networkInterface)
        {
            if (networkInterface == null)
            {
                throw new ArgumentNullException(nameof(networkInterface));
            }

            using (var connection = _connectionFactory.OpenConnection())
            {
                ProtectManualInterface(connection, networkInterface);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
INSERT INTO interfaces
(
    id, device_id, if_index,
    if_name, if_descr, if_alias, custom_name,
    mac_address, admin_status, oper_status,
    speed_bps, media_type_auto, media_type_override,
    is_manual, is_hidden,
    first_seen_utc, last_seen_utc
)
VALUES
(
    @id, @deviceId, @ifIndex,
    @ifName, @ifDescr, @ifAlias, @customName,
    @mac, @admin, @oper,
    @speed, @mediaAuto, @mediaOverride,
    @manual, @hidden,
    @firstSeen, @lastSeen
)
ON CONFLICT(id) DO UPDATE SET
    device_id = excluded.device_id,
    if_index = excluded.if_index,
    if_name = excluded.if_name,
    if_descr = excluded.if_descr,
    if_alias = excluded.if_alias,
    custom_name = excluded.custom_name,
    mac_address = excluded.mac_address,
    admin_status = excluded.admin_status,
    oper_status = excluded.oper_status,
    speed_bps = excluded.speed_bps,
    media_type_auto = excluded.media_type_auto,
    media_type_override = excluded.media_type_override,
    is_manual = excluded.is_manual,
    is_hidden = excluded.is_hidden,
    first_seen_utc = excluded.first_seen_utc,
    last_seen_utc = excluded.last_seen_utc;";

                    Add(command, "@id", networkInterface.Id.ToString("D"));
                    Add(command, "@deviceId", networkInterface.DeviceId.ToString("D"));
                    Add(command, "@ifIndex", networkInterface.IfIndex);
                    Add(command, "@ifName", networkInterface.IfName);
                    Add(command, "@ifDescr", networkInterface.IfDescription);
                    Add(command, "@ifAlias", networkInterface.IfAlias);
                    Add(command, "@customName", networkInterface.CustomName);
                    Add(command, "@mac", networkInterface.MacAddress);
                    Add(command, "@admin", networkInterface.AdminStatus);
                    Add(command, "@oper", networkInterface.OperStatus);
                    Add(command, "@speed", networkInterface.SpeedBps);
                    Add(command, "@mediaAuto", networkInterface.MediaTypeAuto);
                    Add(command, "@mediaOverride", networkInterface.MediaTypeOverride);
                    Add(command, "@manual", networkInterface.IsManual ? 1 : 0);
                    Add(command, "@hidden", networkInterface.IsHidden ? 1 : 0);
                    AddDate(command, "@firstSeen", networkInterface.FirstSeenUtc);
                    AddDate(command, "@lastSeen", networkInterface.LastSeenUtc);

                    command.ExecuteNonQuery();
                }
            }
        }

        public void SavePhysicalLink(PhysicalLink link)
        {
            if (link == null)
            {
                throw new ArgumentNullException(nameof(link));
            }

            using (var connection = _connectionFactory.OpenConnection())
            {
                ProtectManualLink(connection, link);

                ValidateInterfaceEndpoint(
                    connection,
                    link.DeviceAId,
                    link.InterfaceAId);

                ValidateInterfaceEndpoint(
                    connection,
                    link.DeviceBId,
                    link.InterfaceBId);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
INSERT INTO physical_links
(
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
)
VALUES
(
    @id, @linkKey,
    @deviceA, @interfaceA,
    @deviceB, @interfaceB,
    @strength, @freshness,
    @media, @speed,
    @source,
    @firstSeen, @lastSeen, @lastConfirmed,
    @resolver,
    @hidden, @archived,
    @notes
)
ON CONFLICT(id) DO UPDATE SET
    link_key = excluded.link_key,
    device_a_id = excluded.device_a_id,
    interface_a_id = excluded.interface_a_id,
    device_b_id = excluded.device_b_id,
    interface_b_id = excluded.interface_b_id,
    strength = excluded.strength,
    freshness = excluded.freshness,
    media_type_resolved = excluded.media_type_resolved,
    speed_bps_resolved = excluded.speed_bps_resolved,
    source_summary = excluded.source_summary,
    first_seen_utc = excluded.first_seen_utc,
    last_seen_utc = excluded.last_seen_utc,
    last_confirmed_utc = excluded.last_confirmed_utc,
    resolver_version = excluded.resolver_version,
    is_hidden = excluded.is_hidden,
    is_archived = excluded.is_archived,
    notes = excluded.notes;";

                    Add(command, "@id", link.Id.ToString("D"));
                    Add(command, "@linkKey", link.LinkKey);
                    Add(command, "@deviceA", link.DeviceAId.ToString("D"));
                    AddGuid(command, "@interfaceA", link.InterfaceAId);
                    Add(command, "@deviceB", link.DeviceBId.ToString("D"));
                    AddGuid(command, "@interfaceB", link.InterfaceBId);
                    Add(command, "@strength", link.Strength.ToString());
                    Add(command, "@freshness", link.Freshness.ToString());
                    Add(command, "@media", link.MediaTypeResolved);
                    Add(command, "@speed", link.SpeedBpsResolved);
                    Add(command, "@source", link.SourceSummary);
                    Add(command, "@firstSeen", FormatUtc(link.FirstSeenUtc));
                    Add(command, "@lastSeen", FormatUtc(link.LastSeenUtc));
                    AddDate(command, "@lastConfirmed", link.LastConfirmedUtc);
                    Add(command, "@resolver", link.ResolverVersion);
                    Add(command, "@hidden", link.IsHidden ? 1 : 0);
                    Add(command, "@archived", link.IsArchived ? 1 : 0);
                    Add(command, "@notes", link.Notes);

                    command.ExecuteNonQuery();
                }
            }
        }

        public TopologyDevice GetDevice(Guid id)
        {
            using (var connection = _connectionFactory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    location_id, custom_name, category,
    discovery_origin, monitoring_capability,
    vendor_override, model_override, notes,
    is_hidden, is_archived,
    first_seen_utc, last_seen_utc, last_resolved_utc
FROM devices
WHERE id = @id;";

                Add(command, "@id", id.ToString("D"));

                using (var reader = command.ExecuteReader())
                {
                    return reader.Read()
                        ? ReadDevice(id, reader, 0)
                        : null;
                }
            }
        }

        public IReadOnlyList<TopologyDevice> GetDevices()
        {
            var result = new List<TopologyDevice>();

            using (var connection = _connectionFactory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    id,
    location_id, custom_name, category,
    discovery_origin, monitoring_capability,
    vendor_override, model_override, notes,
    is_hidden, is_archived,
    first_seen_utc, last_seen_utc, last_resolved_utc
FROM devices
ORDER BY id;";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(
                            ReadDevice(
                                Guid.Parse(reader.GetString(0)),
                                reader,
                                1));
                    }
                }
            }

            return result;
        }

        public IReadOnlyList<DeviceInterface> GetInterfaces()
        {
            var result = new List<DeviceInterface>();

            using (var connection = _connectionFactory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    id, device_id, if_index,
    if_name, if_descr, if_alias, custom_name,
    mac_address, admin_status, oper_status,
    speed_bps, media_type_auto, media_type_override,
    is_manual, is_hidden,
    first_seen_utc, last_seen_utc
FROM interfaces
ORDER BY id;";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(ReadInterface(reader));
                    }
                }
            }

            return result;
        }

        public IReadOnlyList<PhysicalLink> GetPhysicalLinks()
        {
            var result = new List<PhysicalLink>();

            using (var connection = _connectionFactory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
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

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(ReadLink(reader));
                    }
                }
            }

            return result;
        }

        public void DeleteManualPhysicalLink(Guid id)
        {
            using (var connection = _connectionFactory.OpenConnection())
            {
                RequireManualValue(
                    connection,
                    "physical_links",
                    "strength",
                    id,
                    PhysicalLinkStrength.Manual.ToString());

                DeleteById(connection, "physical_links", id);
            }
        }

        public void DeleteManualInterface(Guid id)
        {
            using (var connection = _connectionFactory.OpenConnection())
            {
                var value = ScalarString(
                    connection,
                    "SELECT is_manual FROM interfaces WHERE id = @id;",
                    id);

                if (value == null)
                {
                    return;
                }

                if (value != "1")
                {
                    throw new InvalidOperationException(
                        "Only manual interfaces can be deleted.");
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT COUNT(*)
FROM physical_links
WHERE interface_a_id = @id
   OR interface_b_id = @id;";

                    Add(command, "@id", id.ToString("D"));

                    if (Convert.ToInt64(command.ExecuteScalar()) > 0)
                    {
                        throw new InvalidOperationException(
                            "Connected interface cannot be deleted.");
                    }
                }

                DeleteById(connection, "interfaces", id);
            }
        }

        public void DeleteManualDevice(Guid id)
        {
            using (var connection = _connectionFactory.OpenConnection())
            {
                RequireManualValue(
                    connection,
                    "devices",
                    "discovery_origin",
                    id,
                    DeviceDiscoveryOrigin.Manual.ToString());

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT COUNT(*)
FROM physical_links
WHERE device_a_id = @id
   OR device_b_id = @id;";

                    Add(command, "@id", id.ToString("D"));

                    if (Convert.ToInt64(command.ExecuteScalar()) > 0)
                    {
                        throw new InvalidOperationException(
                            "Connected device cannot be deleted.");
                    }
                }

                DeleteById(connection, "devices", id);
            }
        }

        private static void ProtectManualDevice(
            SQLiteConnection connection,
            TopologyDevice incoming)
        {
            var existing = ScalarString(
                connection,
                "SELECT discovery_origin FROM devices WHERE id = @id;",
                incoming.Id);

            if (existing == DeviceDiscoveryOrigin.Manual.ToString() &&
                incoming.DiscoveryOrigin != DeviceDiscoveryOrigin.Manual)
            {
                throw new InvalidOperationException(
                    "Automatic topology cannot replace a manual device.");
            }
        }

        private static void ProtectManualInterface(
            SQLiteConnection connection,
            DeviceInterface incoming)
        {
            var existing = ScalarString(
                connection,
                "SELECT is_manual FROM interfaces WHERE id = @id;",
                incoming.Id);

            if (existing == "1" &&
                !incoming.IsManual)
            {
                throw new InvalidOperationException(
                    "Automatic topology cannot replace a manual interface.");
            }
        }

        private static void ProtectManualLink(
            SQLiteConnection connection,
            PhysicalLink incoming)
        {
            var existing = ScalarString(
                connection,
                "SELECT strength FROM physical_links WHERE id = @id;",
                incoming.Id);

            if (existing == PhysicalLinkStrength.Manual.ToString() &&
                incoming.Strength != PhysicalLinkStrength.Manual)
            {
                throw new InvalidOperationException(
                    "Automatic topology cannot replace a manual physical link.");
            }
        }

        private static void ValidateInterfaceEndpoint(
            SQLiteConnection connection,
            Guid deviceId,
            Guid? interfaceId)
        {
            if (!interfaceId.HasValue)
            {
                return;
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT device_id
FROM interfaces
WHERE id = @id;";

                Add(command, "@id", interfaceId.Value.ToString("D"));

                var result = command.ExecuteScalar();

                if (result == null ||
                    result == DBNull.Value)
                {
                    throw new InvalidOperationException(
                        "Physical link interface does not exist.");
                }

                if (!string.Equals(
                    Convert.ToString(result, CultureInfo.InvariantCulture),
                    deviceId.ToString("D"),
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Physical link interface belongs to another device.");
                }
            }
        }

        private static void RequireManualValue(
            SQLiteConnection connection,
            string table,
            string column,
            Guid id,
            string requiredValue)
        {
            var value = ScalarString(
                connection,
                "SELECT " + column +
                " FROM " + table +
                " WHERE id = @id;",
                id);

            if (value == null)
            {
                return;
            }

            if (!string.Equals(
                value,
                requiredValue,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Only manual topology can be deleted by this operation.");
            }
        }

        private static TopologyDevice ReadDevice(
            Guid id,
            SQLiteDataReader reader,
            int offset)
        {
            return new TopologyDevice(
                id,
                GuidNullable(reader, offset + 0),
                StringNullable(reader, offset + 1),
                Parse<DeviceCategory>(reader.GetString(offset + 2)),
                Parse<DeviceDiscoveryOrigin>(reader.GetString(offset + 3)),
                Parse<MonitoringCapability>(reader.GetString(offset + 4)),
                StringNullable(reader, offset + 5),
                StringNullable(reader, offset + 6),
                StringNullable(reader, offset + 7),
                reader.GetInt32(offset + 8) != 0,
                reader.GetInt32(offset + 9) != 0,
                DateNullable(reader, offset + 10),
                DateNullable(reader, offset + 11),
                DateNullable(reader, offset + 12));
        }

        private static DeviceInterface ReadInterface(
            SQLiteDataReader reader)
        {
            return new DeviceInterface(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                StringNullable(reader, 3),
                StringNullable(reader, 4),
                StringNullable(reader, 5),
                StringNullable(reader, 6),
                StringNullable(reader, 7),
                StringNullable(reader, 8),
                StringNullable(reader, 9),
                reader.IsDBNull(10) ? (long?)null : reader.GetInt64(10),
                StringNullable(reader, 11),
                StringNullable(reader, 12),
                reader.GetInt32(13) != 0,
                reader.GetInt32(14) != 0,
                DateNullable(reader, 15),
                DateNullable(reader, 16));
        }

        private static PhysicalLink ReadLink(
            SQLiteDataReader reader)
        {
            return new PhysicalLink(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                Guid.Parse(reader.GetString(2)),
                GuidNullable(reader, 3),
                Guid.Parse(reader.GetString(4)),
                GuidNullable(reader, 5),
                Parse<PhysicalLinkStrength>(reader.GetString(6)),
                Parse<PhysicalLinkFreshness>(reader.GetString(7)),
                StringNullable(reader, 8),
                reader.IsDBNull(9) ? (long?)null : reader.GetInt64(9),
                StringNullable(reader, 10),
                DateRequired(reader, 11),
                DateRequired(reader, 12),
                DateNullable(reader, 13),
                StringNullable(reader, 14),
                reader.GetInt32(15) != 0,
                reader.GetInt32(16) != 0,
                StringNullable(reader, 17));
        }

        private static void DeleteById(
            SQLiteConnection connection,
            string table,
            Guid id)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "DELETE FROM " + table + " WHERE id = @id;";

                Add(command, "@id", id.ToString("D"));
                command.ExecuteNonQuery();
            }
        }

        private static string ScalarString(
            SQLiteConnection connection,
            string sql,
            Guid id)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                Add(command, "@id", id.ToString("D"));

                var value = command.ExecuteScalar();

                return value == null || value == DBNull.Value
                    ? null
                    : Convert.ToString(
                        value,
                        CultureInfo.InvariantCulture);
            }
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
                : Guid.Parse(reader.GetString(ordinal));
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
                : DateRequired(reader, ordinal);
        }

        private static T Parse<T>(string value)
        {
            return (T)Enum.Parse(typeof(T), value);
        }

        private static string FormatUtc(DateTime value)
        {
            return value.ToString(
                "o",
                CultureInfo.InvariantCulture);
        }

        private static void AddDate(
            SQLiteCommand command,
            string name,
            DateTime? value)
        {
            Add(
                command,
                name,
                value.HasValue
                    ? (object)FormatUtc(value.Value)
                    : DBNull.Value);
        }

        private static void AddGuid(
            SQLiteCommand command,
            string name,
            Guid? value)
        {
            Add(
                command,
                name,
                value.HasValue
                    ? (object)value.Value.ToString("D")
                    : DBNull.Value);
        }

        private static void Add(
            SQLiteCommand command,
            string name,
            object value)
        {
            command.Parameters.AddWithValue(
                name,
                value ?? DBNull.Value);
        }
    }
}
