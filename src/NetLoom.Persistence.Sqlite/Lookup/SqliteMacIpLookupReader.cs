using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using NetLoom.Application.Lookup;
using NetLoom.Domain.Observations.Arp;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Lookup
{
    public sealed class SqliteMacIpLookupReader :
        IMacIpLookupReader
    {
        private const int MaximumCandidates = 500;

        private readonly SqliteConnectionFactory
            _connectionFactory;

        public SqliteMacIpLookupReader(
            SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory =
                connectionFactory ??
                throw new ArgumentNullException(
                    nameof(connectionFactory));
        }

        public MacIpLookupResult FindByMac(
            string macAddress,
            int maxCandidates)
        {
            var normalized =
                AddressTextNormalizer
                    .NormalizeMacColon(
                        macAddress);

            if (normalized == null)
            {
                throw new ArgumentException(
                    "A valid MAC-48 address is required.",
                    nameof(macAddress));
            }

            ValidateLimit(maxCandidates);

            using (var connection =
                _connectionFactory.OpenConnection())
            {
                return new MacIpLookupResult(
                    MacIpLookupKind.Mac,
                    normalized,
                    LoadFdbCandidates(
                        connection,
                        normalized,
                        maxCandidates));
            }
        }

        public MacIpLookupResult FindByIp(
            string ipAddress,
            int maxCandidates)
        {
            var normalized =
                AddressTextNormalizer
                    .NormalizeIp(
                        ipAddress);

            if (normalized == null)
            {
                throw new ArgumentException(
                    "A valid IP address is required.",
                    nameof(ipAddress));
            }

            ValidateLimit(maxCandidates);

            using (var connection =
                _connectionFactory.OpenConnection())
            {
                var arp =
                    LoadLatestArpByMac(
                        connection,
                        normalized,
                        maxCandidates);

                var result =
                    new List<MacIpLookupCandidate>();

                foreach (var arpItem in arp)
                {
                    var fdb =
                        LoadFdbCandidates(
                            connection,
                            arpItem.MacAddress,
                            maxCandidates);

                    if (fdb.Count == 0)
                    {
                        result.Add(
                            new MacIpLookupCandidate(
                                normalized,
                                arpItem.MacAddress,
                                null,
                                null,
                                null,
                                null,
                                null,
                                null,
                                null,
                                arpItem.ObservationId,
                                arpItem.CapturedUtc,
                                arpItem.SourceAddress,
                                MacIpLookupCandidateStatus
                                    .FdbNotObserved));

                        continue;
                    }

                    foreach (var item in fdb)
                    {
                        result.Add(
                            AttachArp(
                                normalized,
                                item,
                                arpItem));
                    }
                }

                var ordered =
                    result
                        .OrderByDescending(
                            item =>
                                EvidenceTicks(item))
                        .ThenBy(
                            item =>
                                item.DeviceId.HasValue
                                    ? item.DeviceId.Value
                                        .ToString("D")
                                    : string.Empty,
                            StringComparer.Ordinal)
                        .ThenBy(
                            item =>
                                item.IfIndex ??
                                int.MaxValue)
                        .ThenBy(
                            item =>
                                item.MacAddress,
                            StringComparer.Ordinal)
                        .Take(maxCandidates)
                        .ToArray();

                return new MacIpLookupResult(
                    MacIpLookupKind.Ip,
                    normalized,
                    ordered);
            }
        }

        private static IReadOnlyList<
            MacIpLookupCandidate>
            LoadFdbCandidates(
                SQLiteConnection connection,
                string normalizedMac,
                int maxCandidates)
        {
            var forms =
                AddressTextNormalizer
                    .MacSearchForms(
                        normalizedMac);

            if (forms.Count == 0)
            {
                return new MacIpLookupCandidate[0];
            }

            var parameters =
                new List<string>();

            using (var command =
                connection.CreateCommand())
            {
                for (var index = 0;
                    index < forms.Count;
                    index++)
                {
                    var name =
                        "@mac" +
                        index.ToString(
                            CultureInfo.InvariantCulture);

                    parameters.Add(name);

                    command.Parameters.AddWithValue(
                        name,
                        forms[index]);
                }

                command.Parameters.AddWithValue(
                    "@limit",
                    maxCandidates);

                command.CommandText = @"
SELECT
    f.observation_id,
    o.captured_utc,
    o.source_address,
    b.device_id,
    f.mac_address,
    f.bridge_port_index,
    COUNT(DISTINCT bpm.if_index) AS mapping_count,
    MIN(bpm.if_index) AS resolved_if_index,
    MIN(i.id) AS interface_id
FROM fdb_observations f
JOIN observations o
    ON o.observation_id = f.observation_id
LEFT JOIN observation_device_bindings b
    ON b.observation_id = f.observation_id
LEFT JOIN bridge_port_mappings bpm
    ON bpm.observation_id = f.observation_id
   AND bpm.bridge_port_index =
       f.bridge_port_index
LEFT JOIN interfaces i
    ON b.device_id IS NOT NULL
   AND i.device_id = b.device_id
   AND i.if_index = bpm.if_index
WHERE f.mac_address IN (" +
                    string.Join(
                        ", ",
                        parameters) +
                    @")
GROUP BY
    f.observation_id,
    o.captured_utc,
    o.source_address,
    b.device_id,
    f.mac_address,
    f.bridge_port_index
ORDER BY
    o.captured_utc DESC,
    f.observation_id
LIMIT @limit;";

                var result =
                    new List<MacIpLookupCandidate>();

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var observationId =
                            Guid.Parse(
                                reader.GetString(0));

                        var capturedUtc =
                            ParseUtc(
                                reader.GetString(1));

                        var sourceAddress =
                            reader.GetString(2);

                        var deviceId =
                            GuidNullable(
                                reader,
                                3);

                        var storedMac =
                            reader.GetString(4);

                        var canonicalMac =
                            AddressTextNormalizer
                                .NormalizeMacColon(
                                    storedMac) ??
                            storedMac.Trim();

                        var bridgePortIndex =
                            IntNullable(
                                reader,
                                5);

                        var mappingCount =
                            reader.GetInt32(6);

                        var resolvedIfIndex =
                            mappingCount == 1
                                ? IntNullable(
                                    reader,
                                    7)
                                : null;

                        var interfaceId =
                            mappingCount == 1
                                ? GuidNullable(
                                    reader,
                                    8)
                                : null;

                        var status =
                            ResolveStatus(
                                deviceId,
                                bridgePortIndex,
                                mappingCount,
                                interfaceId);

                        result.Add(
                            new MacIpLookupCandidate(
                                null,
                                canonicalMac,
                                deviceId,
                                status ==
                                    MacIpLookupCandidateStatus
                                        .ResolvedInterface
                                    ? interfaceId
                                    : null,
                                status ==
                                    MacIpLookupCandidateStatus
                                        .ResolvedInterface ||
                                status ==
                                    MacIpLookupCandidateStatus
                                        .InterfaceNotMaterialized
                                    ? resolvedIfIndex
                                    : null,
                                bridgePortIndex,
                                observationId,
                                capturedUtc,
                                sourceAddress,
                                null,
                                null,
                                null,
                                status));
                    }
                }

                return result;
            }
        }

        private static IReadOnlyList<ArpLookupItem>
            LoadLatestArpByMac(
                SQLiteConnection connection,
                string normalizedIp,
                int maxCandidates)
        {
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    a.observation_id,
    o.captured_utc,
    o.source_address,
    a.physical_address,
    a.entry_type,
    a.entry_state,
    a.table_kind
FROM arp_observations a
JOIN observations o
    ON o.observation_id = a.observation_id
WHERE a.ip_address = @ip
  AND a.physical_address IS NOT NULL
ORDER BY
    o.captured_utc DESC,
    a.observation_id
LIMIT @limit;";

                command.Parameters.AddWithValue(
                    "@ip",
                    normalizedIp);

                command.Parameters.AddWithValue(
                    "@limit",
                    Math.Min(
                        MaximumCandidates * 4,
                        maxCandidates * 4));

                var byMac =
                    new Dictionary<
                        string,
                        ArpLookupItem>(
                        StringComparer.Ordinal);

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var physicalAddress =
                            reader.GetString(3);

                        var normalizedMac =
                            AddressTextNormalizer
                                .NormalizeMacColon(
                                    physicalAddress);

                        if (normalizedMac == null)
                        {
                            continue;
                        }

                        var entryType =
                            IntNullable(
                                reader,
                                4);

                        var entryState =
                            IntNullable(
                                reader,
                                5);

                        var tableKind =
                            (ArpTableKind)
                                reader.GetInt32(6);

                        if (!IsUsableArpEntry(
                            entryType,
                            entryState,
                            tableKind))
                        {
                            continue;
                        }

                        if (byMac.ContainsKey(
                            normalizedMac))
                        {
                            continue;
                        }

                        byMac.Add(
                            normalizedMac,
                            new ArpLookupItem(
                                Guid.Parse(
                                    reader.GetString(0)),
                                ParseUtc(
                                    reader.GetString(1)),
                                reader.GetString(2),
                                normalizedMac));
                    }
                }

                return byMac.Values.ToArray();
            }
        }

        private static bool IsUsableArpEntry(
            int? entryType,
            int? entryState,
            ArpTableKind tableKind)
        {
            if (entryType == 2)
            {
                return false;
            }

            if (tableKind ==
                    ArpTableKind.IpNetToPhysical &&
                entryType == 5)
            {
                return false;
            }

            if (tableKind ==
                    ArpTableKind.IpNetToPhysical &&
                (entryState == 7 ||
                 entryState == 5))
            {
                return false;
            }

            return true;
        }

        private static MacIpLookupCandidate
            AttachArp(
                string normalizedIp,
                MacIpLookupCandidate fdb,
                ArpLookupItem arp)
        {
            return new MacIpLookupCandidate(
                normalizedIp,
                fdb.MacAddress,
                fdb.DeviceId,
                fdb.InterfaceId,
                fdb.IfIndex,
                fdb.BridgePortIndex,
                fdb.FdbObservationId,
                fdb.FdbCapturedUtc,
                fdb.FdbSourceAddress,
                arp.ObservationId,
                arp.CapturedUtc,
                arp.SourceAddress,
                fdb.Status);
        }

        private static MacIpLookupCandidateStatus
            ResolveStatus(
                Guid? deviceId,
                int? bridgePortIndex,
                int mappingCount,
                Guid? interfaceId)
        {
            if (!deviceId.HasValue)
            {
                return
                    MacIpLookupCandidateStatus
                        .ObservationUnbound;
            }

            if (!bridgePortIndex.HasValue ||
                bridgePortIndex.Value < 1 ||
                mappingCount == 0)
            {
                return
                    MacIpLookupCandidateStatus
                        .BridgePortUnresolved;
            }

            if (mappingCount != 1)
            {
                return
                    MacIpLookupCandidateStatus
                        .BridgePortAmbiguous;
            }

            if (!interfaceId.HasValue)
            {
                return
                    MacIpLookupCandidateStatus
                        .InterfaceNotMaterialized;
            }

            return
                MacIpLookupCandidateStatus
                    .ResolvedInterface;
        }

        private static long EvidenceTicks(
            MacIpLookupCandidate candidate)
        {
            if (candidate.FdbCapturedUtc.HasValue)
            {
                return candidate
                    .FdbCapturedUtc
                    .Value
                    .Ticks;
            }

            return candidate
                .ArpCapturedUtc
                .HasValue
                    ? candidate
                        .ArpCapturedUtc
                        .Value
                        .Ticks
                    : 0L;
        }

        private static DateTime ParseUtc(
            string value)
        {
            return DateTime.Parse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
        }

        private static int? IntNullable(
            SQLiteDataReader reader,
            int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? (int?)null
                : reader.GetInt32(ordinal);
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

        private static void ValidateLimit(
            int maxCandidates)
        {
            if (maxCandidates < 1 ||
                maxCandidates > MaximumCandidates)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxCandidates));
            }
        }

        private sealed class ArpLookupItem
        {
            public ArpLookupItem(
                Guid observationId,
                DateTime capturedUtc,
                string sourceAddress,
                string macAddress)
            {
                ObservationId = observationId;
                CapturedUtc = capturedUtc;
                SourceAddress = sourceAddress;
                MacAddress = macAddress;
            }

            public Guid ObservationId { get; }

            public DateTime CapturedUtc { get; }

            public string SourceAddress { get; }

            public string MacAddress { get; }
        }
    }
}