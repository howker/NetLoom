using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NetLoom.Application.Observations;
using NetLoom.Application.Observations.Fdb;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations.Fdb;

namespace NetLoom.Protocols.Snmp.Fdb
{
    public sealed class FdbObservationParser
        : IFdbObservationParser
    {
        private const string BasePortIfIndex =
            "1.3.6.1.2.1.17.1.4.1.2";

        private const string FdbAddress =
            "1.3.6.1.2.1.17.4.3.1.1";

        private const string FdbPort =
            "1.3.6.1.2.1.17.4.3.1.2";

        private const string FdbStatus =
            "1.3.6.1.2.1.17.4.3.1.3";

        public FdbObservation Parse(
            SnmpObservation snmpObservation)
        {
            if (snmpObservation == null)
            {
                throw new ArgumentNullException(
                    nameof(snmpObservation));
            }

            var mappings =
                new List<BridgePortMapping>();

            var entries =
                new Dictionary<string, Builder>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (var variable in
                snmpObservation.Variables)
            {
                ParseBridgePortMapping(
                    variable,
                    mappings);

                ParseFdbVariable(
                    variable,
                    entries);
            }

            var normalizedEntries =
                entries
                    .OrderBy(pair => pair.Key)
                    .Select(
                        pair => pair.Value.Build(pair.Key))
                    .ToArray();

            return new FdbObservation(
                snmpObservation.Observation,
                mappings,
                normalizedEntries);
        }

        private static void ParseBridgePortMapping(
            SnmpVariable variable,
            ICollection<BridgePortMapping> mappings)
        {
            int bridgePortIndex;

            if (!TryParseSingleIndex(
                variable.Oid,
                BasePortIfIndex,
                out bridgePortIndex))
            {
                return;
            }

            int ifIndex;

            if (!int.TryParse(
                    variable.DisplayValue,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out ifIndex) ||
                ifIndex < 1)
            {
                return;
            }

            mappings.Add(
                new BridgePortMapping(
                    bridgePortIndex,
                    ifIndex));
        }

        private static void ParseFdbVariable(
            SnmpVariable variable,
            IDictionary<string, Builder> entries)
        {
            string macAddress;

            if (TryParseMacIndex(
                variable.Oid,
                FdbAddress,
                out macAddress))
            {
                GetBuilder(entries, macAddress);
                return;
            }

            if (TryParseMacIndex(
                variable.Oid,
                FdbPort,
                out macAddress))
            {
                int port;

                if (int.TryParse(
                        variable.DisplayValue,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out port) &&
                    port >= 0 &&
                    port <= 65535)
                {
                    GetBuilder(
                        entries,
                        macAddress).BridgePortIndex = port;
                }

                return;
            }

            if (TryParseMacIndex(
                variable.Oid,
                FdbStatus,
                out macAddress))
            {
                int status;

                if (int.TryParse(
                        variable.DisplayValue,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out status))
                {
                    GetBuilder(
                        entries,
                        macAddress).Status = status;
                }
            }
        }

        private static Builder GetBuilder(
            IDictionary<string, Builder> entries,
            string macAddress)
        {
            Builder builder;

            if (!entries.TryGetValue(
                macAddress,
                out builder))
            {
                builder = new Builder();
                entries.Add(macAddress, builder);
            }

            return builder;
        }

        private static bool TryParseSingleIndex(
            string oid,
            string rootOid,
            out int index)
        {
            index = 0;

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

            if (suffix.IndexOf('.') >= 0)
            {
                return false;
            }

            return int.TryParse(
                       suffix,
                       NumberStyles.None,
                       CultureInfo.InvariantCulture,
                       out index) &&
                   index > 0;
        }

        private static bool TryParseMacIndex(
            string oid,
            string rootOid,
            out string macAddress)
        {
            macAddress = null;

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

            var parts =
                normalized
                    .Substring(prefix.Length)
                    .Split('.');

            if (parts.Length != 6)
            {
                return false;
            }

            var octets = new byte[6];

            for (var i = 0; i < parts.Length; i++)
            {
                int value;

                if (!int.TryParse(
                        parts[i],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out value) ||
                    value < 0 ||
                    value > 255)
                {
                    return false;
                }

                octets[i] = (byte)value;
            }

            macAddress =
                string.Join(
                    ":",
                    octets.Select(
                        value => value.ToString(
                            "X2",
                            CultureInfo.InvariantCulture)));

            return true;
        }

        private sealed class Builder
        {
            public int? BridgePortIndex { get; set; }

            public int? Status { get; set; }

            public FdbEntry Build(string macAddress)
            {
                return new FdbEntry(
                    macAddress,
                    BridgePortIndex,
                    Status);
            }
        }
    }
}
