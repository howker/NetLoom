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
        IMaterializedTopologyRepository,
        IAutomaticInterfaceReferenceReconciler
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
                SqliteImmediateWrite.Execute(
                    connection,
                    () =>
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
        discovered_name, lldp_chassis_id,
        is_hidden, is_archived,
        first_seen_utc, last_seen_utc, last_resolved_utc,
        management_address,
        created_at_utc, updated_at_utc
    )
    VALUES
    (
        @id, @locationId, @customName, @category,
        @origin, @capability,
        @vendor, @model, @notes,
        @discoveredName, @lldpChassisId,
        @hidden, @archived,
        @firstSeen, @lastSeen, @lastResolved,
        @managementAddress,
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
        discovered_name =
            COALESCE(excluded.discovered_name, devices.discovered_name),
        lldp_chassis_id =
            COALESCE(excluded.lldp_chassis_id, devices.lldp_chassis_id),
        management_address =
            COALESCE(excluded.management_address, devices.management_address),
        is_hidden = excluded.is_hidden,
        is_archived = excluded.is_archived,
        first_seen_utc =
            CASE
                WHEN devices.first_seen_utc IS NULL THEN excluded.first_seen_utc
                WHEN excluded.first_seen_utc IS NULL THEN devices.first_seen_utc
                WHEN excluded.first_seen_utc < devices.first_seen_utc THEN excluded.first_seen_utc
                ELSE devices.first_seen_utc
            END,
        last_seen_utc =
            CASE
                WHEN devices.last_seen_utc IS NULL THEN excluded.last_seen_utc
                WHEN excluded.last_seen_utc IS NULL THEN devices.last_seen_utc
                WHEN excluded.last_seen_utc > devices.last_seen_utc THEN excluded.last_seen_utc
                ELSE devices.last_seen_utc
            END,
        last_resolved_utc =
            CASE
                WHEN devices.last_resolved_utc IS NULL THEN excluded.last_resolved_utc
                WHEN excluded.last_resolved_utc IS NULL THEN devices.last_resolved_utc
                WHEN excluded.last_resolved_utc > devices.last_resolved_utc THEN excluded.last_resolved_utc
                ELSE devices.last_resolved_utc
            END,
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
                        Add(command, "@discoveredName", device.DiscoveredName);
                        Add(command, "@lldpChassisId", device.LldpChassisId);
                        Add(command, "@managementAddress", device.ManagementAddress);
                        Add(command, "@hidden", device.IsHidden ? 1 : 0);
                        Add(command, "@archived", device.IsArchived ? 1 : 0);
                        AddDate(command, "@firstSeen", device.FirstSeenUtc);
                        AddDate(command, "@lastSeen", device.LastSeenUtc);
                        AddDate(command, "@lastResolved", device.LastResolvedUtc);
                        Add(command, "@created", now);
                        Add(command, "@updated", now);

                        command.ExecuteNonQuery();
                    }

                    });
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
                SqliteImmediateWrite.Execute(
                    connection,
                    () =>
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
        first_seen_utc, last_seen_utc,
        lldp_port_id, lldp_port_description,
        if_type
    )
    VALUES
    (
        @id, @deviceId, @ifIndex,
        @ifName, @ifDescr, @ifAlias, @customName,
        @mac, @admin, @oper,
        @speed, @mediaAuto, @mediaOverride,
        @manual, @hidden,
        @firstSeen, @lastSeen,
        @lldpPortId, @lldpPortDescription,
        @ifType
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
        first_seen_utc =
            CASE
                WHEN interfaces.first_seen_utc IS NULL THEN excluded.first_seen_utc
                WHEN excluded.first_seen_utc IS NULL THEN interfaces.first_seen_utc
                WHEN excluded.first_seen_utc < interfaces.first_seen_utc THEN excluded.first_seen_utc
                ELSE interfaces.first_seen_utc
            END,
        last_seen_utc =
            CASE
                WHEN interfaces.last_seen_utc IS NULL THEN excluded.last_seen_utc
                WHEN excluded.last_seen_utc IS NULL THEN interfaces.last_seen_utc
                WHEN excluded.last_seen_utc > interfaces.last_seen_utc THEN excluded.last_seen_utc
                ELSE interfaces.last_seen_utc
            END,
        lldp_port_id =
            COALESCE(excluded.lldp_port_id, interfaces.lldp_port_id),
        lldp_port_description =
            COALESCE(
                excluded.lldp_port_description,
                interfaces.lldp_port_description),
        if_type =
            COALESCE(
                excluded.if_type,
                interfaces.if_type);";

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
                        Add(command, "@lldpPortId", networkInterface.LldpPortId);
                        Add(command, "@lldpPortDescription", networkInterface.LldpPortDescription);
                        Add(command, "@ifType", networkInterface.IfType);

                        command.ExecuteNonQuery();
                    }

                    });
            }
        }

        public PhysicalLink SavePhysicalLink(PhysicalLink link)
        {
            if (link == null)
            {
                throw new ArgumentNullException(nameof(link));
            }

            using (var connection = _connectionFactory.OpenConnection())
            {
                return SqliteImmediateWrite.Execute(
                    connection,
                    () =>
                    {
                    var existing =
                        ResolvePhysicalLinkTarget(
                            connection,
                            link);

                    ProtectManualLink(
                        existing,
                        link);

                    var persisted =
                        MergePhysicalLink(
                            existing,
                            link);

                    ValidateInterfaceEndpoint(
                        connection,
                        persisted.DeviceAId,
                        persisted.InterfaceAId);

                    ValidateInterfaceEndpoint(
                        connection,
                        persisted.DeviceBId,
                        persisted.InterfaceBId);

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
        first_seen_utc =
            CASE
                WHEN excluded.first_seen_utc < physical_links.first_seen_utc
                    THEN excluded.first_seen_utc
                ELSE physical_links.first_seen_utc
            END,
        last_seen_utc =
            CASE
                WHEN excluded.last_seen_utc > physical_links.last_seen_utc
                    THEN excluded.last_seen_utc
                ELSE physical_links.last_seen_utc
            END,
        last_confirmed_utc =
            CASE
                WHEN physical_links.last_confirmed_utc IS NULL
                    THEN excluded.last_confirmed_utc
                WHEN excluded.last_confirmed_utc IS NULL
                    THEN physical_links.last_confirmed_utc
                WHEN excluded.last_confirmed_utc > physical_links.last_confirmed_utc
                    THEN excluded.last_confirmed_utc
                ELSE physical_links.last_confirmed_utc
            END,
        resolver_version = excluded.resolver_version,
        is_hidden = excluded.is_hidden,
        is_archived = excluded.is_archived,
        notes = excluded.notes;";

                        Add(command, "@id", persisted.Id.ToString("D"));
                        Add(command, "@linkKey", persisted.LinkKey);
                        Add(command, "@deviceA", persisted.DeviceAId.ToString("D"));
                        AddGuid(command, "@interfaceA", persisted.InterfaceAId);
                        Add(command, "@deviceB", persisted.DeviceBId.ToString("D"));
                        AddGuid(command, "@interfaceB", persisted.InterfaceBId);
                        Add(command, "@strength", persisted.Strength.ToString());
                        Add(command, "@freshness", persisted.Freshness.ToString());
                        Add(command, "@media", persisted.MediaTypeResolved);
                        Add(command, "@speed", persisted.SpeedBpsResolved);
                        Add(command, "@source", persisted.SourceSummary);
                        Add(command, "@firstSeen", FormatUtc(persisted.FirstSeenUtc));
                        Add(command, "@lastSeen", FormatUtc(persisted.LastSeenUtc));
                        AddDate(command, "@lastConfirmed", persisted.LastConfirmedUtc);
                        Add(command, "@resolver", persisted.ResolverVersion);
                        Add(command, "@hidden", persisted.IsHidden ? 1 : 0);
                        Add(command, "@archived", persisted.IsArchived ? 1 : 0);
                        Add(command, "@notes", persisted.Notes);

                        command.ExecuteNonQuery();
                    }

                    return persisted;

                    });
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
    discovered_name, lldp_chassis_id,
    is_hidden, is_archived,
    first_seen_utc, last_seen_utc, last_resolved_utc,
    management_address
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
    discovered_name, lldp_chassis_id,
    is_hidden, is_archived,
    first_seen_utc, last_seen_utc, last_resolved_utc,
    management_address
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
    first_seen_utc, last_seen_utc,
    lldp_port_id, lldp_port_description,
    if_type
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

        public void ReplacePhysicalLinkEvidence(
            Guid physicalLinkId,
            IEnumerable<PhysicalLinkEvidence> evidence)
        {
            if (physicalLinkId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Physical link id is required.",
                    nameof(physicalLinkId));
            }

            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            var currentBySlot =
                new Dictionary<string, PhysicalLinkEvidence>(
                    StringComparer.Ordinal);

            foreach (var item in evidence)
            {
                if (item == null)
                {
                    throw new ArgumentException(
                        "Evidence item cannot be null.",
                        nameof(evidence));
                }

                if (item.PhysicalLinkId != physicalLinkId)
                {
                    throw new InvalidOperationException(
                        "Evidence belongs to another physical link.");
                }

                currentBySlot[EvidenceSlotKey(item)] = item;
            }

            using (var connection = _connectionFactory.OpenConnection())
            {
                var existing =
                    ScalarString(
                        connection,
                        "SELECT id FROM physical_links WHERE id = @id;",
                        physicalLinkId);

                if (existing == null)
                {
                    throw new InvalidOperationException(
                        "Physical link does not exist.");
                }

                using (var transaction = connection.BeginTransaction())
                {
                    using (var delete = connection.CreateCommand())
                    {
                        delete.Transaction = transaction;
                        delete.CommandText = @"
DELETE FROM physical_link_evidence_current
WHERE physical_link_id = @physicalLinkId;";

                        Add(
                            delete,
                            "@physicalLinkId",
                            physicalLinkId.ToString("D"));

                        delete.ExecuteNonQuery();
                    }

                    foreach (var item in currentBySlot.Values)
                    {
                        using (var insert = connection.CreateCommand())
                        {
                            insert.Transaction = transaction;
                            insert.CommandText = @"
INSERT INTO physical_link_evidence_current
(
    physical_link_id,
    evidence_kind,
    evidence_strength,
    source_address,
    slot_discriminator,
    observation_id,
    captured_utc,
    detail
)
VALUES
(
    @physicalLinkId,
    @kind,
    @strength,
    @sourceAddress,
    @slotDiscriminator,
    @observationId,
    @capturedUtc,
    @detail
);";

                            Add(
                                insert,
                                "@physicalLinkId",
                                item.PhysicalLinkId.ToString("D"));

                            Add(
                                insert,
                                "@kind",
                                item.Kind.ToString());

                            Add(
                                insert,
                                "@strength",
                                item.Strength.ToString());

                            Add(
                                insert,
                                "@sourceAddress",
                                item.SourceAddress);

                            Add(
                                insert,
                                "@slotDiscriminator",
                                item.SlotDiscriminator);

                            AddGuid(
                                insert,
                                "@observationId",
                                item.ObservationId);

                            AddDate(
                                insert,
                                "@capturedUtc",
                                item.CapturedUtc);

                            Add(
                                insert,
                                "@detail",
                                item.Detail);

                            insert.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
            }
        }

        public IReadOnlyList<PhysicalLinkEvidence>
            GetPhysicalLinkEvidence()
        {
            using (var connection = _connectionFactory.OpenConnection())
            {
                return ReadPhysicalLinkEvidence(
                    connection,
                    null);
            }
        }

        public IReadOnlyList<PhysicalLinkEvidence>
            GetPhysicalLinkEvidence(Guid physicalLinkId)
        {
            if (physicalLinkId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Physical link id is required.",
                    nameof(physicalLinkId));
            }

            using (var connection = _connectionFactory.OpenConnection())
            {
                return ReadPhysicalLinkEvidence(
                    connection,
                    physicalLinkId);
            }
        }

        public void ReconcileAutomaticInterfaceReferences(
            Guid obsoleteInterfaceId,
            Guid canonicalInterfaceId)
        {
            if (obsoleteInterfaceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Obsolete interface id is required.",
                    nameof(obsoleteInterfaceId));
            }

            if (canonicalInterfaceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Canonical interface id is required.",
                    nameof(canonicalInterfaceId));
            }

            if (obsoleteInterfaceId ==
                canonicalInterfaceId)
            {
                return;
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            {
                SqliteImmediateWrite.Execute(
                    connection,
                    () =>
                    {
                        var obsoleteDeviceId =
                            ScalarString(
                                connection,
                                "SELECT device_id FROM interfaces WHERE id = @id;",
                                obsoleteInterfaceId);

                        if (obsoleteDeviceId == null)
                        {
                            return;
                        }

                        var canonicalDeviceId =
                            ScalarString(
                                connection,
                                "SELECT device_id FROM interfaces WHERE id = @id;",
                                canonicalInterfaceId);

                        if (canonicalDeviceId == null)
                        {
                            throw new InvalidOperationException(
                                "Canonical interface does not exist.");
                        }

                        if (!string.Equals(
                            obsoleteDeviceId,
                            canonicalDeviceId,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException(
                                "Automatic interface reconciliation cannot cross devices.");
                        }

                        var obsoleteManual =
                            ScalarString(
                                connection,
                                "SELECT is_manual FROM interfaces WHERE id = @id;",
                                obsoleteInterfaceId);

                        if (obsoleteManual != "0")
                        {
                            throw new InvalidOperationException(
                                "Manual interface cannot be reconciled automatically.");
                        }

                        var affected =
                            GetPhysicalLinksForInterface(
                                connection,
                                obsoleteInterfaceId);

                        var replacements =
                            new List<PhysicalLink>();

                        var replacementKeys =
                            new HashSet<string>(
                                StringComparer.Ordinal);

                        foreach (var existing in affected)
                        {
                            if (existing.Strength ==
                                PhysicalLinkStrength.Manual)
                            {
                                throw new InvalidOperationException(
                                    "Automatic interface reconciliation cannot move a manual physical link.");
                            }

                            var interfaceA =
                                existing.InterfaceAId;

                            if (interfaceA.HasValue &&
                                interfaceA.Value ==
                                    obsoleteInterfaceId)
                            {
                                interfaceA =
                                    canonicalInterfaceId;
                            }

                            var interfaceB =
                                existing.InterfaceBId;

                            if (interfaceB.HasValue &&
                                interfaceB.Value ==
                                    obsoleteInterfaceId)
                            {
                                interfaceB =
                                    canonicalInterfaceId;
                            }

                            var replacement =
                                new PhysicalLink(
                                    existing.Id,
                                    existing.DeviceAId,
                                    interfaceA,
                                    existing.DeviceBId,
                                    interfaceB,
                                    existing.Strength,
                                    existing.Freshness,
                                    existing.MediaTypeResolved,
                                    existing.SpeedBpsResolved,
                                    existing.SourceSummary,
                                    existing.FirstSeenUtc,
                                    existing.LastSeenUtc,
                                    existing.LastConfirmedUtc,
                                    existing.ResolverVersion,
                                    existing.IsHidden,
                                    existing.IsArchived,
                                    existing.Notes);

                            if (!replacementKeys.Add(
                                replacement.LinkKey))
                            {
                                throw new InvalidOperationException(
                                    "Automatic interface reconciliation would collapse multiple physical links.");
                            }

                            replacements.Add(
                                replacement);
                        }

                        foreach (var replacement in
                            replacements)
                        {
                            using (var conflict =
                                connection.CreateCommand())
                            {
                                conflict.CommandText = @"
SELECT id
FROM physical_links
WHERE link_key = @linkKey
  AND id <> @id
LIMIT 1;";

                                Add(
                                    conflict,
                                    "@linkKey",
                                    replacement.LinkKey);

                                Add(
                                    conflict,
                                    "@id",
                                    replacement.Id.ToString("D"));

                                var conflictingId =
                                    conflict.ExecuteScalar();

                                if (conflictingId != null &&
                                    conflictingId != DBNull.Value)
                                {
                                    throw new InvalidOperationException(
                                        "Automatic interface reconciliation conflicts with an existing physical link.");
                                }
                            }
                        }

                        foreach (var existing in affected)
                        {
                            using (var temporary =
                                connection.CreateCommand())
                            {
                                temporary.CommandText = @"
UPDATE physical_links
SET link_key = @temporaryKey
WHERE id = @id;";

                                Add(
                                    temporary,
                                    "@temporaryKey",
                                    "reconcile-temp-" +
                                    existing.Id.ToString("N"));

                                Add(
                                    temporary,
                                    "@id",
                                    existing.Id.ToString("D"));

                                temporary.ExecuteNonQuery();
                            }
                        }

                        foreach (var replacement in
                            replacements)
                        {
                            using (var update =
                                connection.CreateCommand())
                            {
                                update.CommandText = @"
UPDATE physical_links
SET link_key = @linkKey,
    device_a_id = @deviceA,
    interface_a_id = @interfaceA,
    device_b_id = @deviceB,
    interface_b_id = @interfaceB
WHERE id = @id;";

                                Add(
                                    update,
                                    "@linkKey",
                                    replacement.LinkKey);

                                Add(
                                    update,
                                    "@deviceA",
                                    replacement.DeviceAId.ToString("D"));

                                AddGuid(
                                    update,
                                    "@interfaceA",
                                    replacement.InterfaceAId);

                                Add(
                                    update,
                                    "@deviceB",
                                    replacement.DeviceBId.ToString("D"));

                                AddGuid(
                                    update,
                                    "@interfaceB",
                                    replacement.InterfaceBId);

                                Add(
                                    update,
                                    "@id",
                                    replacement.Id.ToString("D"));

                                update.ExecuteNonQuery();
                            }
                        }

                        using (var delete =
                            connection.CreateCommand())
                        {
                            delete.CommandText = @"
DELETE FROM interfaces
WHERE id = @id
  AND is_manual = 0
  AND NOT EXISTS
      (
          SELECT 1
          FROM physical_links
          WHERE interface_a_id = @id
             OR interface_b_id = @id
      );";

                            Add(
                                delete,
                                "@id",
                                obsoleteInterfaceId.ToString("D"));

                            delete.ExecuteNonQuery();
                        }
                    });
            }
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
            PhysicalLink existing,
            PhysicalLink incoming)
        {
            if (existing != null &&
                existing.Strength == PhysicalLinkStrength.Manual &&
                incoming.Strength != PhysicalLinkStrength.Manual)
            {
                throw new InvalidOperationException(
                    "Automatic topology cannot replace a manual physical link.");
            }
        }

        private static PhysicalLink ResolvePhysicalLinkTarget(
            SQLiteConnection connection,
            PhysicalLink incoming)
        {
            var byId =
                GetPhysicalLinkById(
                    connection,
                    incoming.Id);

            if (byId != null &&
                !SameDevicePair(byId, incoming))
            {
                throw new InvalidOperationException(
                    "Physical link id cannot be reused for another device pair.");
            }

            var pairLinks =
                GetPhysicalLinksForDevicePair(
                    connection,
                    incoming.DeviceAId,
                    incoming.DeviceBId);

            PhysicalLink exact = null;
            var exactCount = 0;

            foreach (var existing in pairLinks)
            {
                if (SameEndpoints(
                    existing,
                    incoming))
                {
                    exact = existing;
                    exactCount++;
                }
            }

            if (exactCount > 1)
            {
                throw new InvalidOperationException(
                    "Multiple physical links have identical canonical endpoints.");
            }

            if (exact != null)
            {
                if (byId != null &&
                    byId.Id != exact.Id)
                {
                    throw new InvalidOperationException(
                        "Physical link id conflicts with an existing canonical link.");
                }

                return exact;
            }

            if (byId != null)
            {
                if (!EndpointsCompatible(
                    byId,
                    incoming))
                {
                    throw new InvalidOperationException(
                        "Physical link id cannot move to incompatible endpoints.");
                }

                return byId;
            }

            PhysicalLink compatible = null;
            var compatibleCount = 0;

            foreach (var existing in pairLinks)
            {
                if (EndpointsCompatible(
                    existing,
                    incoming))
                {
                    compatible = existing;
                    compatibleCount++;
                }
            }

            if (compatibleCount > 1)
            {
                throw new InvalidOperationException(
                    "Physical link refinement is ambiguous.");
            }

            return compatible;
        }

        private static PhysicalLink MergePhysicalLink(
            PhysicalLink existing,
            PhysicalLink incoming)
        {
            if (existing == null)
            {
                return incoming;
            }

            var interfaceA =
                MergeInterface(
                    existing.InterfaceAId,
                    incoming.InterfaceAId);

            var interfaceB =
                MergeInterface(
                    existing.InterfaceBId,
                    incoming.InterfaceBId);

            return new PhysicalLink(
                existing.Id,
                existing.DeviceAId,
                interfaceA,
                existing.DeviceBId,
                interfaceB,
                incoming.Strength,
                incoming.Freshness,
                incoming.MediaTypeResolved,
                incoming.SpeedBpsResolved,
                incoming.SourceSummary,
                Min(
                    existing.FirstSeenUtc,
                    incoming.FirstSeenUtc),
                Max(
                    existing.LastSeenUtc,
                    incoming.LastSeenUtc),
                MaxNullable(
                    existing.LastConfirmedUtc,
                    incoming.LastConfirmedUtc),
                incoming.ResolverVersion,
                incoming.IsHidden,
                incoming.IsArchived,
                incoming.Notes);
        }

        private static PhysicalLink GetPhysicalLinkById(
            SQLiteConnection connection,
            Guid id)
        {
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
WHERE id = @id;";

                Add(command, "@id", id.ToString("D"));

                using (var reader = command.ExecuteReader())
                {
                    return reader.Read()
                        ? ReadLink(reader)
                        : null;
                }
            }
        }

        private static IReadOnlyList<PhysicalLink>
            GetPhysicalLinksForInterface(
                SQLiteConnection connection,
                Guid interfaceId)
        {
            var result =
                new List<PhysicalLink>();

            using (var command =
                connection.CreateCommand())
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
WHERE interface_a_id = @interfaceId
   OR interface_b_id = @interfaceId
ORDER BY id;";

                Add(
                    command,
                    "@interfaceId",
                    interfaceId.ToString("D"));

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(
                            ReadLink(
                                reader));
                    }
                }
            }

            return result;
        }

        private static IReadOnlyList<PhysicalLink>
            GetPhysicalLinksForDevicePair(
                SQLiteConnection connection,
                Guid deviceAId,
                Guid deviceBId)
        {
            var result =
                new List<PhysicalLink>();

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
WHERE
    (device_a_id = @deviceA AND device_b_id = @deviceB)
 OR (device_a_id = @deviceB AND device_b_id = @deviceA)
ORDER BY id;";

                Add(command, "@deviceA", deviceAId.ToString("D"));
                Add(command, "@deviceB", deviceBId.ToString("D"));

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

        private static bool SameDevicePair(
            PhysicalLink left,
            PhysicalLink right)
        {
            return
                left.DeviceAId == right.DeviceAId &&
                left.DeviceBId == right.DeviceBId;
        }

        private static bool SameEndpoints(
            PhysicalLink left,
            PhysicalLink right)
        {
            return
                SameDevicePair(left, right) &&
                left.InterfaceAId == right.InterfaceAId &&
                left.InterfaceBId == right.InterfaceBId;
        }

        private static bool EndpointsCompatible(
            PhysicalLink left,
            PhysicalLink right)
        {
            return
                SameDevicePair(left, right) &&
                InterfaceCompatible(
                    left.InterfaceAId,
                    right.InterfaceAId) &&
                InterfaceCompatible(
                    left.InterfaceBId,
                    right.InterfaceBId);
        }

        private static bool InterfaceCompatible(
            Guid? left,
            Guid? right)
        {
            return
                !left.HasValue ||
                !right.HasValue ||
                left.Value == right.Value;
        }

        private static Guid? MergeInterface(
            Guid? existing,
            Guid? incoming)
        {
            if (existing.HasValue &&
                incoming.HasValue &&
                existing.Value != incoming.Value)
            {
                throw new InvalidOperationException(
                    "Physical link refinement has incompatible interfaces.");
            }

            return incoming.HasValue
                ? incoming
                : existing;
        }

        private static DateTime Min(
            DateTime left,
            DateTime right)
        {
            return left <= right
                ? left
                : right;
        }

        private static DateTime Max(
            DateTime left,
            DateTime right)
        {
            return left >= right
                ? left
                : right;
        }

        private static DateTime? MaxNullable(
            DateTime? left,
            DateTime? right)
        {
            if (!left.HasValue)
            {
                return right;
            }

            if (!right.HasValue)
            {
                return left;
            }

            return left.Value >= right.Value
                ? left
                : right;
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

        private static IReadOnlyList<PhysicalLinkEvidence>
            ReadPhysicalLinkEvidence(
                SQLiteConnection connection,
                Guid? physicalLinkId)
        {
            var result =
                new List<PhysicalLinkEvidence>();

            using (var command = connection.CreateCommand())
            {
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
FROM physical_link_evidence_current" +
                    (physicalLinkId.HasValue
                        ? " WHERE physical_link_id = @physicalLinkId"
                        : string.Empty) +
                    @"
ORDER BY
    physical_link_id,
    evidence_kind,
    source_address,
    slot_discriminator;";

                if (physicalLinkId.HasValue)
                {
                    Add(
                        command,
                        "@physicalLinkId",
                        physicalLinkId.Value.ToString("D"));
                }

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(
                            new PhysicalLinkEvidence(
                                Guid.Parse(reader.GetString(0)),
                                Parse<PhysicalLinkEvidenceKind>(
                                    reader.GetString(1)),
                                Parse<PhysicalLinkEvidenceStrength>(
                                    reader.GetString(2)),
                                reader.GetString(3),
                                reader.GetString(4),
                                GuidNullable(reader, 5),
                                DateNullable(reader, 6),
                                StringNullable(reader, 7)));
                    }
                }
            }

            return result;
        }

        private static string EvidenceSlotKey(
            PhysicalLinkEvidence evidence)
        {
            return
                evidence.Kind +
                "\u001f" +
                evidence.SourceAddress +
                "\u001f" +
                evidence.SlotDiscriminator;
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
                reader.GetInt32(offset + 10) != 0,
                reader.GetInt32(offset + 11) != 0,
                DateNullable(reader, offset + 12),
                DateNullable(reader, offset + 13),
                DateNullable(reader, offset + 14),
                StringNullable(reader, offset + 8),
                StringNullable(reader, offset + 9),
                StringNullable(reader, offset + 15));
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
                DateNullable(reader, 16),
                StringNullable(reader, 17),
                StringNullable(reader, 18),
                reader.IsDBNull(19) ? (int?)null : reader.GetInt32(19));
        }

        private static PhysicalLink ReadLink(
            SQLiteDataReader reader)
        {
            return new PhysicalLink(
                Guid.Parse(reader.GetString(0)),
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
