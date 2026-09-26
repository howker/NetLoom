using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NetLoom.Application.Observations;
using NetLoom.Application.Observations.Lldp;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations.Lldp;
using NetLoom.Protocols.Snmp;

namespace NetLoom.Protocols.Snmp.Lldp
{
    public sealed class LldpObservationParser
        : ILldpObservationParser
    {
        private const string LocChassisIdSubtype =
            "1.0.8802.1.1.2.1.3.1.0";

        private const string LocChassisId =
            "1.0.8802.1.1.2.1.3.2.0";

        private const string LocSysName =
            "1.0.8802.1.1.2.1.3.3.0";

        private const string LocPortIdSubtype =
            "1.0.8802.1.1.2.1.3.7.1.2";

        private const string LocPortId =
            "1.0.8802.1.1.2.1.3.7.1.3";

        private const string LocPortDesc =
            "1.0.8802.1.1.2.1.3.7.1.4";

        private const string RemChassisIdSubtype =
            "1.0.8802.1.1.2.1.4.1.1.4";

        private const string RemChassisId =
            "1.0.8802.1.1.2.1.4.1.1.5";

        private const string RemPortIdSubtype =
            "1.0.8802.1.1.2.1.4.1.1.6";

        private const string RemPortId =
            "1.0.8802.1.1.2.1.4.1.1.7";

        private const string RemPortDesc =
            "1.0.8802.1.1.2.1.4.1.1.8";

        private const string RemSysName =
            "1.0.8802.1.1.2.1.4.1.1.9";

        private const string RemSysDesc =
            "1.0.8802.1.1.2.1.4.1.1.10";

        private const string RemSysCapSupported =
            "1.0.8802.1.1.2.1.4.1.1.11";

        private const string RemSysCapEnabled =
            "1.0.8802.1.1.2.1.4.1.1.12";

        public LldpObservation Parse(
            SnmpObservation snmpObservation)
        {
            if (snmpObservation == null)
            {
                throw new ArgumentNullException(
                    nameof(snmpObservation));
            }

            var localSystem =
                ParseLocalSystem(
                    snmpObservation.Variables);

            var localPorts =
                ParseLocalPorts(
                    snmpObservation.Variables);

            var builders =
                new Dictionary<RemoteKey, RemoteBuilder>();

            foreach (var variable in
                snmpObservation.Variables)
            {
                ApplyRemote(
                    variable,
                    RemChassisIdSubtype,
                    builders,
                    (builder, value) =>
                        builder.ChassisIdSubtype =
                            ParseInt(value.DisplayValue));

                ApplyRemote(
                    variable,
                    RemChassisId,
                    builders,
                    (builder, value) =>
                        builder.ChassisIdVariable =
                            value);

                ApplyRemote(
                    variable,
                    RemPortIdSubtype,
                    builders,
                    (builder, value) =>
                        builder.PortIdSubtype =
                            ParseInt(value.DisplayValue));

                ApplyRemote(
                    variable,
                    RemPortId,
                    builders,
                    (builder, value) =>
                        builder.PortIdVariable =
                            value);

                ApplyRemote(
                    variable,
                    RemPortDesc,
                    builders,
                    (builder, value) =>
                        builder.PortDescription =
                            value.DisplayValue);

                ApplyRemote(
                    variable,
                    RemSysName,
                    builders,
                    (builder, value) =>
                        builder.SystemName =
                            value.DisplayValue);

                ApplyRemote(
                    variable,
                    RemSysDesc,
                    builders,
                    (builder, value) =>
                        builder.SystemDescription =
                            value.DisplayValue);

                ApplyRemote(
                    variable,
                    RemSysCapSupported,
                    builders,
                    (builder, value) =>
                        builder.SystemCapabilitiesSupported =
                            value.DisplayValue);

                ApplyRemote(
                    variable,
                    RemSysCapEnabled,
                    builders,
                    (builder, value) =>
                        builder.SystemCapabilitiesEnabled =
                            value.DisplayValue);
            }

            var neighbors = builders
                .OrderBy(pair => pair.Key.TimeMark)
                .ThenBy(pair => pair.Key.LocalPortNumber)
                .ThenBy(pair => pair.Key.RemoteIndex)
                .Select(
                    pair =>
                    {
                        LldpLocalPort localPort;

                        localPorts.TryGetValue(
                            pair.Key.LocalPortNumber,
                            out localPort);

                        return pair.Value.Build(
                            pair.Key,
                            localPort);
                    })
                .ToArray();

            return new LldpObservation(
                snmpObservation.Observation,
                neighbors,
                localSystem);
        }

        private static LldpLocalSystem ParseLocalSystem(
            IReadOnlyList<SnmpVariable> variables)
        {
            int? chassisIdSubtype = null;
            SnmpVariable chassisIdVariable = null;
            string systemName = null;

            foreach (var variable in variables)
            {
                var oid =
                    string.IsNullOrWhiteSpace(variable.Oid)
                        ? string.Empty
                        : variable.Oid.TrimStart('.');

                if (string.Equals(
                    oid,
                    LocChassisIdSubtype,
                    StringComparison.Ordinal))
                {
                    chassisIdSubtype =
                        ParseInt(variable.DisplayValue);
                }
                else if (string.Equals(
                    oid,
                    LocChassisId,
                    StringComparison.Ordinal))
                {
                    chassisIdVariable = variable;
                }
                else if (string.Equals(
                    oid,
                    LocSysName,
                    StringComparison.Ordinal))
                {
                    systemName = variable.DisplayValue;
                }
            }

            var chassisId =
                SnmpBinaryValue.ReadLldpChassisId(
                    chassisIdVariable,
                    chassisIdSubtype);

            if (!chassisIdSubtype.HasValue &&
                string.IsNullOrWhiteSpace(chassisId) &&
                string.IsNullOrWhiteSpace(systemName))
            {
                return null;
            }

            return new LldpLocalSystem(
                chassisIdSubtype,
                chassisId,
                systemName);
        }

        private static Dictionary<int, LldpLocalPort>
            ParseLocalPorts(
                IReadOnlyList<SnmpVariable> variables)
        {
            var builders =
                new Dictionary<int, LocalBuilder>();

            foreach (var variable in variables)
            {
                ApplyLocal(
                    variable,
                    LocPortIdSubtype,
                    builders,
                    (builder, value) =>
                        builder.PortIdSubtype =
                            ParseInt(value.DisplayValue));

                ApplyLocal(
                    variable,
                    LocPortId,
                    builders,
                    (builder, value) =>
                        builder.PortIdVariable =
                            value);

                ApplyLocal(
                    variable,
                    LocPortDesc,
                    builders,
                    (builder, value) =>
                        builder.PortDescription =
                            value.DisplayValue);
            }

            return builders.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Build(pair.Key));
        }

        private static void ApplyLocal(
            SnmpVariable variable,
            string rootOid,
            IDictionary<int, LocalBuilder> builders,
            Action<LocalBuilder, SnmpVariable> apply)
        {
            int portNumber;

            if (!TryParseLocalIndex(
                variable.Oid,
                rootOid,
                out portNumber))
            {
                return;
            }

            LocalBuilder builder;

            if (!builders.TryGetValue(
                portNumber,
                out builder))
            {
                builder = new LocalBuilder();
                builders.Add(portNumber, builder);
            }

            apply(builder, variable);
        }

        private static void ApplyRemote(
            SnmpVariable variable,
            string rootOid,
            IDictionary<RemoteKey, RemoteBuilder> builders,
            Action<RemoteBuilder, SnmpVariable> apply)
        {
            RemoteKey key;

            if (!TryParseRemoteIndex(
                variable.Oid,
                rootOid,
                out key))
            {
                return;
            }

            RemoteBuilder builder;

            if (!builders.TryGetValue(key, out builder))
            {
                builder = new RemoteBuilder();
                builders.Add(key, builder);
            }

            apply(builder, variable);
        }

        private static bool TryParseLocalIndex(
            string oid,
            string rootOid,
            out int portNumber)
        {
            portNumber = 0;

            var suffix = GetSuffix(
                oid,
                rootOid);

            return suffix != null &&
                int.TryParse(
                    suffix,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out portNumber) &&
                portNumber > 0;
        }

        private static bool TryParseRemoteIndex(
            string oid,
            string rootOid,
            out RemoteKey key)
        {
            key = default(RemoteKey);

            var suffix = GetSuffix(
                oid,
                rootOid);

            if (suffix == null)
            {
                return false;
            }

            var parts = suffix.Split('.');

            if (parts.Length != 3)
            {
                return false;
            }

            long timeMark;
            int localPortNumber;
            int remoteIndex;

            if (!long.TryParse(
                    parts[0],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out timeMark) ||
                !int.TryParse(
                    parts[1],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out localPortNumber) ||
                !int.TryParse(
                    parts[2],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out remoteIndex) ||
                timeMark < 0 ||
                localPortNumber < 1 ||
                remoteIndex < 1)
            {
                return false;
            }

            key = new RemoteKey(
                timeMark,
                localPortNumber,
                remoteIndex);

            return true;
        }

        private static string GetSuffix(
            string oid,
            string rootOid)
        {
            if (string.IsNullOrWhiteSpace(oid))
            {
                return null;
            }

            var normalized =
                oid.TrimStart('.');

            var prefix =
                rootOid + ".";

            if (!normalized.StartsWith(
                prefix,
                StringComparison.Ordinal))
            {
                return null;
            }

            return normalized.Substring(
                prefix.Length);
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

        private struct RemoteKey
            : IEquatable<RemoteKey>
        {
            public RemoteKey(
                long timeMark,
                int localPortNumber,
                int remoteIndex)
            {
                TimeMark = timeMark;
                LocalPortNumber = localPortNumber;
                RemoteIndex = remoteIndex;
            }

            public long TimeMark { get; }

            public int LocalPortNumber { get; }

            public int RemoteIndex { get; }

            public bool Equals(RemoteKey other)
            {
                return TimeMark == other.TimeMark &&
                    LocalPortNumber ==
                    other.LocalPortNumber &&
                    RemoteIndex == other.RemoteIndex;
            }

            public override bool Equals(object obj)
            {
                return obj is RemoteKey &&
                    Equals((RemoteKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = TimeMark.GetHashCode();
                    hash = (hash * 397) ^
                        LocalPortNumber;
                    hash = (hash * 397) ^
                        RemoteIndex;
                    return hash;
                }
            }
        }

        private sealed class LocalBuilder
        {
            public int? PortIdSubtype { get; set; }

            public SnmpVariable PortIdVariable { get; set; }

            public string PortDescription { get; set; }

            public LldpLocalPort Build(
                int localPortNumber)
            {
                return new LldpLocalPort(
                    localPortNumber,
                    PortIdSubtype,
                    SnmpBinaryValue.ReadLldpPortId(
                        PortIdVariable,
                        PortIdSubtype),
                    PortDescription);
            }
        }

        private sealed class RemoteBuilder
        {
            public int? ChassisIdSubtype { get; set; }

            public SnmpVariable ChassisIdVariable { get; set; }

            public int? PortIdSubtype { get; set; }

            public SnmpVariable PortIdVariable { get; set; }

            public string PortDescription { get; set; }

            public string SystemName { get; set; }

            public string SystemDescription { get; set; }

            public string SystemCapabilitiesSupported
            {
                get;
                set;
            }

            public string SystemCapabilitiesEnabled
            {
                get;
                set;
            }

            public LldpRemoteNeighbor Build(
                RemoteKey key,
                LldpLocalPort localPort)
            {
                return new LldpRemoteNeighbor(
                    key.TimeMark,
                    key.LocalPortNumber,
                    key.RemoteIndex,
                    ChassisIdSubtype,
                    SnmpBinaryValue.ReadLldpChassisId(
                        ChassisIdVariable,
                        ChassisIdSubtype),
                    PortIdSubtype,
                    SnmpBinaryValue.ReadLldpPortId(
                        PortIdVariable,
                        PortIdSubtype),
                    PortDescription,
                    SystemName,
                    SystemDescription,
                    SystemCapabilitiesSupported,
                    SystemCapabilitiesEnabled,
                    localPort);
            }
        }
    }
}
