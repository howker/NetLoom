using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NetLoom.Application.Observations;
using NetLoom.Application.Observations.Stp;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations.Stp;
using NetLoom.Protocols.Snmp;

namespace NetLoom.Protocols.Snmp.Stp
{
    public sealed class StpObservationParser :
        IStpObservationParser
    {
        public const string CommonInstanceId =
            "cist";

        private const string BasePortIfIndex =
            "1.3.6.1.2.1.17.1.4.1.2";

        private const string ProtocolSpecification =
            "1.3.6.1.2.1.17.2.1.0";

        private const string DesignatedRoot =
            "1.3.6.1.2.1.17.2.5.0";

        private const string RootCost =
            "1.3.6.1.2.1.17.2.6.0";

        private const string RootPort =
            "1.3.6.1.2.1.17.2.7.0";

        private const string PortEntry =
            "1.3.6.1.2.1.17.2.15.1";

        private const string PortPriority =
            PortEntry + ".2";

        private const string PortState =
            PortEntry + ".3";

        private const string PortEnable =
            PortEntry + ".4";

        private const string PortPathCost =
            PortEntry + ".5";

        private const string PortDesignatedRoot =
            PortEntry + ".6";

        private const string PortDesignatedCost =
            PortEntry + ".7";

        private const string PortDesignatedBridge =
            PortEntry + ".8";

        private const string PortDesignatedPort =
            PortEntry + ".9";

        private const string PortForwardTransitions =
            PortEntry + ".10";

        private const string PortPathCost32 =
            PortEntry + ".11";

        public StpObservation Parse(
            SnmpObservation snmpObservation)
        {
            if (snmpObservation == null)
            {
                throw new ArgumentNullException(
                    nameof(snmpObservation));
            }

            var mappings =
                BuildMappings(
                    snmpObservation.Variables);

            var builders =
                new Dictionary<int, PortBuilder>();

            int? protocol = null;
            string root = null;
            long? rootCost = null;
            int? rootBridgePort = null;

            foreach (var variable in
                snmpObservation.Variables)
            {
                if (OidEquals(
                    variable.Oid,
                    ProtocolSpecification))
                {
                    protocol =
                        ParseInt(variable.DisplayValue);
                    continue;
                }

                if (OidEquals(
                    variable.Oid,
                    DesignatedRoot))
                {
                    root =
                        SnmpBinaryValue.ReadStpBridgeId(
                            variable);
                    continue;
                }

                if (OidEquals(
                    variable.Oid,
                    RootCost))
                {
                    rootCost =
                        ParseLong(variable.DisplayValue);
                    continue;
                }

                if (OidEquals(
                    variable.Oid,
                    RootPort))
                {
                    rootBridgePort =
                        ParseNonNegativeInt(
                            variable.DisplayValue);
                    continue;
                }

                ParsePortVariable(
                    variable,
                    builders);
            }

            int? rootIfIndex = null;

            if (rootBridgePort.HasValue &&
                rootBridgePort.Value > 0)
            {
                rootIfIndex =
                    ResolveIfIndex(
                        mappings,
                        rootBridgePort.Value);
            }

            var ports =
                builders
                    .OrderBy(pair => pair.Key)
                    .Select(
                        pair =>
                            pair.Value.Build(
                                pair.Key,
                                ResolveIfIndex(
                                    mappings,
                                    pair.Key)))
                    .ToArray();

            return new StpObservation(
                snmpObservation.Observation,
                CommonInstanceId,
                protocol,
                root,
                rootCost,
                rootBridgePort,
                rootIfIndex,
                ports);
        }

        private static IDictionary<int, int[]>
            BuildMappings(
                IEnumerable<SnmpVariable> variables)
        {
            var candidates =
                new Dictionary<int, HashSet<int>>();

            foreach (var variable in variables)
            {
                int bridgePortIndex;

                if (!TryParseSingleIndex(
                    variable.Oid,
                    BasePortIfIndex,
                    out bridgePortIndex))
                {
                    continue;
                }

                var ifIndex =
                    ParseInt(variable.DisplayValue);

                if (!ifIndex.HasValue ||
                    ifIndex.Value < 1)
                {
                    continue;
                }

                HashSet<int> set;

                if (!candidates.TryGetValue(
                    bridgePortIndex,
                    out set))
                {
                    set = new HashSet<int>();
                    candidates.Add(
                        bridgePortIndex,
                        set);
                }

                set.Add(ifIndex.Value);
            }

            return candidates.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToArray());
        }

        private static int? ResolveIfIndex(
            IDictionary<int, int[]> mappings,
            int bridgePortIndex)
        {
            int[] candidates;

            if (!mappings.TryGetValue(
                    bridgePortIndex,
                    out candidates) ||
                candidates.Length != 1)
            {
                return null;
            }

            return candidates[0];
        }

        private static void ParsePortVariable(
            SnmpVariable variable,
            IDictionary<int, PortBuilder> builders)
        {
            int bridgePortIndex;

            if (TryParseSingleIndex(
                variable.Oid,
                PortPriority,
                out bridgePortIndex))
            {
                GetBuilder(
                    builders,
                    bridgePortIndex).Priority =
                    ParseInt(variable.DisplayValue);
                return;
            }

            if (TryParseSingleIndex(
                variable.Oid,
                PortState,
                out bridgePortIndex))
            {
                GetBuilder(
                    builders,
                    bridgePortIndex).State =
                    ParseInt(variable.DisplayValue);
                return;
            }

            if (TryParseSingleIndex(
                variable.Oid,
                PortEnable,
                out bridgePortIndex))
            {
                GetBuilder(
                    builders,
                    bridgePortIndex).Enabled =
                    ParseInt(variable.DisplayValue);
                return;
            }

            if (TryParseSingleIndex(
                variable.Oid,
                PortPathCost,
                out bridgePortIndex))
            {
                GetBuilder(
                    builders,
                    bridgePortIndex).LegacyPathCost =
                    ParseLong(variable.DisplayValue);
                return;
            }

            if (TryParseSingleIndex(
                variable.Oid,
                PortPathCost32,
                out bridgePortIndex))
            {
                GetBuilder(
                    builders,
                    bridgePortIndex).PathCost32 =
                    ParseLong(variable.DisplayValue);
                return;
            }

            if (TryParseSingleIndex(
                variable.Oid,
                PortDesignatedRoot,
                out bridgePortIndex))
            {
                GetBuilder(
                    builders,
                    bridgePortIndex).DesignatedRoot =
                    SnmpBinaryValue.ReadStpBridgeId(
                        variable);
                return;
            }

            if (TryParseSingleIndex(
                variable.Oid,
                PortDesignatedCost,
                out bridgePortIndex))
            {
                GetBuilder(
                    builders,
                    bridgePortIndex).DesignatedCost =
                    ParseLong(variable.DisplayValue);
                return;
            }

            if (TryParseSingleIndex(
                variable.Oid,
                PortDesignatedBridge,
                out bridgePortIndex))
            {
                GetBuilder(
                    builders,
                    bridgePortIndex).DesignatedBridge =
                    SnmpBinaryValue.ReadStpBridgeId(
                        variable);
                return;
            }

            if (TryParseSingleIndex(
                variable.Oid,
                PortDesignatedPort,
                out bridgePortIndex))
            {
                GetBuilder(
                    builders,
                    bridgePortIndex).DesignatedPort =
                    SnmpBinaryValue.ReadStpPortId(
                        variable);
                return;
            }

            if (TryParseSingleIndex(
                variable.Oid,
                PortForwardTransitions,
                out bridgePortIndex))
            {
                GetBuilder(
                    builders,
                    bridgePortIndex).ForwardTransitions =
                    ParseLong(variable.DisplayValue);
            }
        }

        private static PortBuilder GetBuilder(
            IDictionary<int, PortBuilder> builders,
            int bridgePortIndex)
        {
            PortBuilder builder;

            if (!builders.TryGetValue(
                bridgePortIndex,
                out builder))
            {
                builder = new PortBuilder();
                builders.Add(
                    bridgePortIndex,
                    builder);
            }

            return builder;
        }

        private static bool OidEquals(
            string actual,
            string expected)
        {
            return string.Equals(
                NormalizeOid(actual),
                expected,
                StringComparison.Ordinal);
        }

        private static bool TryParseSingleIndex(
            string oid,
            string rootOid,
            out int index)
        {
            index = 0;

            var normalized =
                NormalizeOid(oid);

            if (normalized == null)
            {
                return false;
            }

            var prefix =
                rootOid + ".";

            if (!normalized.StartsWith(
                prefix,
                StringComparison.Ordinal))
            {
                return false;
            }

            var suffix =
                normalized.Substring(
                    prefix.Length);

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

        private static int? ParseNonNegativeInt(
            string value)
        {
            var parsed =
                ParseLong(value);

            if (!parsed.HasValue ||
                parsed.Value < 0 ||
                parsed.Value > int.MaxValue)
            {
                return null;
            }

            return (int)parsed.Value;
        }

        private static int? ParseInt(
            string value)
        {
            var parsed =
                ParseLong(value);

            if (!parsed.HasValue ||
                parsed.Value < int.MinValue ||
                parsed.Value > int.MaxValue)
            {
                return null;
            }

            return (int)parsed.Value;
        }

        private static long? ParseLong(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var text = value.Trim();
            long parsed;

            if (long.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parsed))
            {
                return parsed;
            }

            var open =
                text.LastIndexOf('(');

            var close =
                text.LastIndexOf(')');

            if (open >= 0 &&
                close > open + 1 &&
                long.TryParse(
                    text.Substring(
                        open + 1,
                        close - open - 1),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out parsed))
            {
                return parsed;
            }

            return null;
        }

        private static string NormalizeOid(
            string oid)
        {
            return string.IsNullOrWhiteSpace(oid)
                ? null
                : oid.Trim().TrimStart('.');
        }

        private static string Normalize(
            string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private sealed class PortBuilder
        {
            public int? Priority { get; set; }

            public int? State { get; set; }

            public int? Enabled { get; set; }

            public long? LegacyPathCost { get; set; }

            public long? PathCost32 { get; set; }

            public string DesignatedRoot { get; set; }

            public long? DesignatedCost { get; set; }

            public string DesignatedBridge { get; set; }

            public string DesignatedPort { get; set; }

            public long? ForwardTransitions { get; set; }

            public StpPortState Build(
                int bridgePortIndex,
                int? ifIndex)
            {
                return new StpPortState(
                    bridgePortIndex,
                    ifIndex,
                    Priority,
                    State,
                    Enabled,
                    PathCost32 ?? LegacyPathCost,
                    DesignatedRoot,
                    DesignatedCost,
                    DesignatedBridge,
                    DesignatedPort,
                    ForwardTransitions);
            }
        }
    }
}
