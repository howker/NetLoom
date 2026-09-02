using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NetLoom.Application.Observations;
using NetLoom.Application.Observations.Cdp;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations.Cdp;

namespace NetLoom.Protocols.Snmp.Cdp
{
    public sealed class CdpObservationParser
        : ICdpObservationParser
    {
        private const string AddressType =
            "1.3.6.1.4.1.9.9.23.1.2.1.1.3";

        private const string Address =
            "1.3.6.1.4.1.9.9.23.1.2.1.1.4";

        private const string Version =
            "1.3.6.1.4.1.9.9.23.1.2.1.1.5";

        private const string DeviceId =
            "1.3.6.1.4.1.9.9.23.1.2.1.1.6";

        private const string DevicePort =
            "1.3.6.1.4.1.9.9.23.1.2.1.1.7";

        private const string Platform =
            "1.3.6.1.4.1.9.9.23.1.2.1.1.8";

        private const string Capabilities =
            "1.3.6.1.4.1.9.9.23.1.2.1.1.9";

        private const string NativeVlan =
            "1.3.6.1.4.1.9.9.23.1.2.1.1.11";

        private const string Duplex =
            "1.3.6.1.4.1.9.9.23.1.2.1.1.12";

        private const string SysName =
            "1.3.6.1.4.1.9.9.23.1.2.1.1.17";

        private const string SysObjectId =
            "1.3.6.1.4.1.9.9.23.1.2.1.1.18";

        private const string PrimaryMgmtAddrType =
            "1.3.6.1.4.1.9.9.23.1.2.1.1.19";

        private const string PrimaryMgmtAddr =
            "1.3.6.1.4.1.9.9.23.1.2.1.1.20";

        private const string PhysLocation =
            "1.3.6.1.4.1.9.9.23.1.2.1.1.23";

        private const string LastChange =
            "1.3.6.1.4.1.9.9.23.1.2.1.1.24";

        public CdpObservation Parse(
            SnmpObservation snmpObservation)
        {
            if (snmpObservation == null)
            {
                throw new ArgumentNullException(
                    nameof(snmpObservation));
            }

            var builders =
                new Dictionary<CdpKey, Builder>();

            foreach (var variable in
                snmpObservation.Variables)
            {
                Apply(
                    variable,
                    AddressType,
                    builders,
                    (builder, value) =>
                        builder.AddressType = ParseInt(value));

                Apply(
                    variable,
                    Address,
                    builders,
                    (builder, value) =>
                        builder.Address = value);

                Apply(
                    variable,
                    Version,
                    builders,
                    (builder, value) =>
                        builder.Version = value);

                Apply(
                    variable,
                    DeviceId,
                    builders,
                    (builder, value) =>
                        builder.DeviceId = value);

                Apply(
                    variable,
                    DevicePort,
                    builders,
                    (builder, value) =>
                        builder.DevicePort = value);

                Apply(
                    variable,
                    Platform,
                    builders,
                    (builder, value) =>
                        builder.Platform = value);

                Apply(
                    variable,
                    Capabilities,
                    builders,
                    (builder, value) =>
                        builder.Capabilities = value);

                Apply(
                    variable,
                    NativeVlan,
                    builders,
                    (builder, value) =>
                        builder.NativeVlan = ParseInt(value));

                Apply(
                    variable,
                    Duplex,
                    builders,
                    (builder, value) =>
                        builder.Duplex = ParseInt(value));

                Apply(
                    variable,
                    SysName,
                    builders,
                    (builder, value) =>
                        builder.SystemName = value);

                Apply(
                    variable,
                    SysObjectId,
                    builders,
                    (builder, value) =>
                        builder.SystemObjectId = value);

                Apply(
                    variable,
                    PrimaryMgmtAddrType,
                    builders,
                    (builder, value) =>
                        builder.PrimaryManagementAddressType =
                            ParseInt(value));

                Apply(
                    variable,
                    PrimaryMgmtAddr,
                    builders,
                    (builder, value) =>
                        builder.PrimaryManagementAddress = value);

                Apply(
                    variable,
                    PhysLocation,
                    builders,
                    (builder, value) =>
                        builder.PhysicalLocation = value);

                Apply(
                    variable,
                    LastChange,
                    builders,
                    (builder, value) =>
                        builder.LastChange = ParseLong(value));
            }

            var neighbors = builders
                .OrderBy(pair => pair.Key.CacheIfIndex)
                .ThenBy(pair => pair.Key.DeviceIndex)
                .Select(
                    pair => pair.Value.Build(pair.Key))
                .ToArray();

            return new CdpObservation(
                snmpObservation.Observation,
                neighbors);
        }

        private static void Apply(
            SnmpVariable variable,
            string rootOid,
            IDictionary<CdpKey, Builder> builders,
            Action<Builder, string> apply)
        {
            CdpKey key;

            if (!TryParseIndex(
                variable.Oid,
                rootOid,
                out key))
            {
                return;
            }

            Builder builder;

            if (!builders.TryGetValue(key, out builder))
            {
                builder = new Builder();
                builders.Add(key, builder);
            }

            apply(builder, variable.DisplayValue);
        }

        private static bool TryParseIndex(
            string oid,
            string rootOid,
            out CdpKey key)
        {
            key = default(CdpKey);

            if (string.IsNullOrWhiteSpace(oid))
            {
                return false;
            }

            var normalized = oid.TrimStart('.');
            var prefix = rootOid + ".";

            if (!normalized.StartsWith(
                prefix,
                StringComparison.Ordinal))
            {
                return false;
            }

            var suffix =
                normalized.Substring(prefix.Length);

            var parts = suffix.Split('.');

            if (parts.Length != 2)
            {
                return false;
            }

            int cacheIfIndex;
            int deviceIndex;

            if (!int.TryParse(
                    parts[0],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out cacheIfIndex) ||
                !int.TryParse(
                    parts[1],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out deviceIndex) ||
                cacheIfIndex < 0 ||
                deviceIndex < 0)
            {
                return false;
            }

            key = new CdpKey(
                cacheIfIndex,
                deviceIndex);

            return true;
        }

        private static int? ParseInt(string value)
        {
            int parsed;

            return int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parsed)
                ? parsed
                : (int?)null;
        }

        private static long? ParseLong(string value)
        {
            long parsed;

            return long.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parsed)
                ? parsed
                : (long?)null;
        }

        private struct CdpKey
            : IEquatable<CdpKey>
        {
            public CdpKey(
                int cacheIfIndex,
                int deviceIndex)
            {
                CacheIfIndex = cacheIfIndex;
                DeviceIndex = deviceIndex;
            }

            public int CacheIfIndex { get; }

            public int DeviceIndex { get; }

            public bool Equals(CdpKey other)
            {
                return CacheIfIndex ==
                    other.CacheIfIndex &&
                    DeviceIndex ==
                    other.DeviceIndex;
            }

            public override bool Equals(object obj)
            {
                return obj is CdpKey &&
                    Equals((CdpKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return
                        (CacheIfIndex * 397) ^
                        DeviceIndex;
                }
            }
        }

        private sealed class Builder
        {
            public int? AddressType { get; set; }
            public string Address { get; set; }
            public string Version { get; set; }
            public string DeviceId { get; set; }
            public string DevicePort { get; set; }
            public string Platform { get; set; }
            public string Capabilities { get; set; }
            public int? NativeVlan { get; set; }
            public int? Duplex { get; set; }
            public string SystemName { get; set; }
            public string SystemObjectId { get; set; }

            public int? PrimaryManagementAddressType
            {
                get;
                set;
            }

            public string PrimaryManagementAddress
            {
                get;
                set;
            }

            public string PhysicalLocation { get; set; }
            public long? LastChange { get; set; }

            public CdpRemoteNeighbor Build(CdpKey key)
            {
                return new CdpRemoteNeighbor(
                    key.CacheIfIndex,
                    key.DeviceIndex,
                    AddressType,
                    Address,
                    Version,
                    DeviceId,
                    DevicePort,
                    Platform,
                    Capabilities,
                    NativeVlan,
                    Duplex,
                    SystemName,
                    SystemObjectId,
                    PrimaryManagementAddressType,
                    PrimaryManagementAddress,
                    PhysicalLocation,
                    LastChange);
            }
        }
    }
}
