using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using NetLoom.Application.DiscoveryControl;

namespace NetLoom.Desktop.Discovery
{
    internal enum EngineDiscoveryMarkerKind
    {
        None = 0,
        ControlReady = 1,
        Started = 2,
        Progress = 3,
        Candidate = 4,
        Completed = 5,
        Stopped = 6
    }

    internal sealed class EngineDiscoveryMarker
    {
        public EngineDiscoveryMarker(
            EngineDiscoveryMarkerKind kind,
            Guid? accessProfileId = null,
            int processedAddresses = 0,
            int totalAddresses = 0,
            int foundCandidates = 0,
            IPAddress address = null,
            bool? candidateFound = null,
            DiscoveryCandidateSnapshot candidate = null)
        {
            Kind = kind;
            AccessProfileId = accessProfileId;
            ProcessedAddresses = processedAddresses;
            TotalAddresses = totalAddresses;
            FoundCandidates = foundCandidates;
            Address = address;
            CandidateFound = candidateFound;
            Candidate = candidate;
        }

        public EngineDiscoveryMarkerKind Kind { get; }

        public Guid? AccessProfileId { get; }

        public int ProcessedAddresses { get; }

        public int TotalAddresses { get; }

        public int FoundCandidates { get; }

        public IPAddress Address { get; }

        public bool? CandidateFound { get; }

        public DiscoveryCandidateSnapshot Candidate { get; }
    }

    internal static class EngineDiscoveryMarkerParser
    {
        private static readonly UTF8Encoding StrictUtf8 =
            new UTF8Encoding(
                false,
                true);

        public static bool TryParse(
            string line,
            out EngineDiscoveryMarker marker)
        {
            marker = null;

            if (string.Equals(
                line,
                "NETLOOM_DISCOVERY_CONTROL state=ready",
                StringComparison.Ordinal))
            {
                marker =
                    new EngineDiscoveryMarker(
                        EngineDiscoveryMarkerKind.ControlReady);
                return true;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            var parts =
                line.Split(
                    new[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                return false;
            }

            var values =
                ParseValues(
                    parts);

            if (string.Equals(
                parts[0],
                "NETLOOM_DISCOVERY_CANDIDATE",
                StringComparison.Ordinal))
            {
                return TryParseCandidate(
                    values,
                    out marker);
            }

            if (!string.Equals(
                parts[0],
                "NETLOOM_DISCOVERY",
                StringComparison.Ordinal))
            {
                return false;
            }

            string state;

            if (!values.TryGetValue(
                "state",
                out state))
            {
                return false;
            }

            if (string.Equals(
                state,
                "started",
                StringComparison.Ordinal))
            {
                Guid accessProfileId;
                int total;
                int delayMilliseconds;

                if (!TryGuid(
                        values,
                        "accessProfileId",
                        out accessProfileId) ||
                    !TryNonNegativeInt(
                        values,
                        "total",
                        out total) ||
                    !TryNonNegativeInt(
                        values,
                        "delayMs",
                        out delayMilliseconds))
                {
                    return false;
                }

                marker =
                    new EngineDiscoveryMarker(
                        EngineDiscoveryMarkerKind.Started,
                        accessProfileId,
                        0,
                        total,
                        0);
                return true;
            }

            if (string.Equals(
                state,
                "progress",
                StringComparison.Ordinal))
            {
                int processed;
                int total;
                int found;
                IPAddress address;
                bool candidateFound;

                if (!TryPositiveInt(
                        values,
                        "processed",
                        out processed) ||
                    !TryNonNegativeInt(
                        values,
                        "total",
                        out total) ||
                    processed > total ||
                    !TryNonNegativeInt(
                        values,
                        "found",
                        out found) ||
                    found > processed ||
                    !TryAddress(
                        values,
                        "address",
                        out address) ||
                    !TryBoolean(
                        values,
                        "candidate",
                        out candidateFound) ||
                    (candidateFound && found == 0))
                {
                    return false;
                }

                marker =
                    new EngineDiscoveryMarker(
                        EngineDiscoveryMarkerKind.Progress,
                        null,
                        processed,
                        total,
                        found,
                        address,
                        candidateFound);
                return true;
            }

            if (string.Equals(
                    state,
                    "completed",
                    StringComparison.Ordinal) ||
                string.Equals(
                    state,
                    "stopped",
                    StringComparison.Ordinal))
            {
                int processed;
                int total;
                int found;

                if (!TryNonNegativeInt(
                        values,
                        "processed",
                        out processed) ||
                    !TryNonNegativeInt(
                        values,
                        "total",
                        out total) ||
                    processed > total ||
                    !TryNonNegativeInt(
                        values,
                        "found",
                        out found) ||
                    found > processed)
                {
                    return false;
                }

                marker =
                    new EngineDiscoveryMarker(
                        string.Equals(
                            state,
                            "completed",
                            StringComparison.Ordinal)
                            ? EngineDiscoveryMarkerKind.Completed
                            : EngineDiscoveryMarkerKind.Stopped,
                        null,
                        processed,
                        total,
                        found);
                return true;
            }

            return false;
        }

        private static bool TryParseCandidate(
            IReadOnlyDictionary<string, string> values,
            out EngineDiscoveryMarker marker)
        {
            marker = null;

            IPAddress address;
            Guid? accessProfileId;
            bool icmp;
            bool snmp;
            IReadOnlyList<int> tcpPorts;
            string sysName;
            string sysDescription;
            string sysObjectId;
            string sysLocation;
            int interfaceCount;

            if (!TryAddress(
                    values,
                    "address",
                    out address) ||
                !TryOptionalGuid(
                    values,
                    "accessProfileId",
                    out accessProfileId) ||
                !TryBoolean(
                    values,
                    "icmp",
                    out icmp) ||
                !TryBoolean(
                    values,
                    "snmp",
                    out snmp) ||
                !TryPorts(
                    values,
                    "tcpPorts",
                    out tcpPorts) ||
                !TryEncodedText(
                    values,
                    "sysName64",
                    out sysName) ||
                !TryEncodedText(
                    values,
                    "sysDescription64",
                    out sysDescription) ||
                !TryEncodedText(
                    values,
                    "sysObjectId64",
                    out sysObjectId) ||
                !TryEncodedText(
                    values,
                    "sysLocation64",
                    out sysLocation) ||
                !TryNonNegativeInt(
                    values,
                    "interfaces",
                    out interfaceCount) ||
                (snmp && !accessProfileId.HasValue) ||
                (!snmp && accessProfileId.HasValue))
            {
                return false;
            }

            DiscoveryCandidateSnapshot candidate;

            try
            {
                candidate =
                    new DiscoveryCandidateSnapshot(
                        address,
                        accessProfileId,
                        icmp,
                        snmp,
                        tcpPorts,
                        sysName,
                        sysDescription,
                        sysObjectId,
                        sysLocation,
                        interfaceCount);
            }
            catch
            {
                return false;
            }

            marker =
                new EngineDiscoveryMarker(
                    EngineDiscoveryMarkerKind.Candidate,
                    accessProfileId,
                    candidate: candidate);

            return true;
        }

        private static Dictionary<string, string> ParseValues(
            IReadOnlyList<string> parts)
        {
            var values =
                new Dictionary<string, string>(
                    StringComparer.Ordinal);

            for (var index = 1;
                 index < parts.Count;
                 index++)
            {
                var part =
                    parts[index];

                var separator =
                    part.IndexOf('=');

                if (separator <= 0 ||
                    separator == part.Length - 1)
                {
                    continue;
                }

                var key =
                    part.Substring(
                        0,
                        separator);

                if (!values.ContainsKey(key))
                {
                    values.Add(
                        key,
                        part.Substring(
                            separator + 1));
                }
            }

            return values;
        }

        private static bool TryGuid(
            IReadOnlyDictionary<string, string> values,
            string key,
            out Guid value)
        {
            value = Guid.Empty;
            string text;

            return values.TryGetValue(key, out text) &&
                Guid.TryParse(text, out value) &&
                value != Guid.Empty;
        }

        private static bool TryOptionalGuid(
            IReadOnlyDictionary<string, string> values,
            string key,
            out Guid? value)
        {
            value = null;
            string text;

            if (!values.TryGetValue(key, out text))
            {
                return false;
            }

            if (string.Equals(
                text,
                "none",
                StringComparison.Ordinal))
            {
                return true;
            }

            Guid parsed;

            if (!Guid.TryParse(text, out parsed) ||
                parsed == Guid.Empty)
            {
                return false;
            }

            value = parsed;
            return true;
        }

        private static bool TryPositiveInt(
            IReadOnlyDictionary<string, string> values,
            string key,
            out int value)
        {
            return TryInt(
                values,
                key,
                1,
                int.MaxValue,
                out value);
        }

        private static bool TryNonNegativeInt(
            IReadOnlyDictionary<string, string> values,
            string key,
            out int value)
        {
            return TryInt(
                values,
                key,
                0,
                int.MaxValue,
                out value);
        }

        private static bool TryInt(
            IReadOnlyDictionary<string, string> values,
            string key,
            int minimum,
            int maximum,
            out int value)
        {
            value = 0;
            string text;

            return values.TryGetValue(key, out text) &&
                int.TryParse(
                    text,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out value) &&
                value >= minimum &&
                value <= maximum;
        }

        private static bool TryBoolean(
            IReadOnlyDictionary<string, string> values,
            string key,
            out bool value)
        {
            value = false;
            string text;

            if (!values.TryGetValue(key, out text))
            {
                return false;
            }

            if (string.Equals(
                text,
                "true",
                StringComparison.Ordinal))
            {
                value = true;
                return true;
            }

            if (string.Equals(
                text,
                "false",
                StringComparison.Ordinal))
            {
                value = false;
                return true;
            }

            return false;
        }

        private static bool TryAddress(
            IReadOnlyDictionary<string, string> values,
            string key,
            out IPAddress value)
        {
            value = null;
            string text;

            return values.TryGetValue(key, out text) &&
                IPAddress.TryParse(
                    text,
                    out value);
        }

        private static bool TryPorts(
            IReadOnlyDictionary<string, string> values,
            string key,
            out IReadOnlyList<int> ports)
        {
            ports = null;
            string text;

            if (!values.TryGetValue(key, out text))
            {
                return false;
            }

            if (string.Equals(
                text,
                "none",
                StringComparison.Ordinal))
            {
                ports = new int[0];
                return true;
            }

            var result =
                new List<int>();

            foreach (var token in
                text.Split(
                    new[] { ',' },
                    StringSplitOptions.RemoveEmptyEntries))
            {
                int port;

                if (!int.TryParse(
                        token,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out port) ||
                    port < 1 ||
                    port > 65535 ||
                    result.Contains(port))
                {
                    return false;
                }

                result.Add(port);
            }

            if (result.Count == 0)
            {
                return false;
            }

            ports = result;
            return true;
        }

        private static bool TryEncodedText(
            IReadOnlyDictionary<string, string> values,
            string key,
            out string value)
        {
            value = null;
            string text;

            if (!values.TryGetValue(key, out text))
            {
                return false;
            }

            if (string.Equals(
                text,
                "-",
                StringComparison.Ordinal))
            {
                return true;
            }

            try
            {
                value =
                    StrictUtf8.GetString(
                        Convert.FromBase64String(
                            text));
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }
    }
}
