using System;
using System.Collections.Generic;
using System.Globalization;
using NetLoom.Application.Discovery;
using NetLoom.Application.Snmp;

namespace NetLoom.Desktop.Discovery
{
    internal static class EngineProfileCheckMarkerParser
    {
        internal const string Prefix = "NETLOOM_PROFILE_CHECK ";

        public static bool TryParse(string line, out SnmpProfileCheckItem item, out bool completed)
        {
            item = null;
            completed = false;
            if (line == null || !line.StartsWith(Prefix, StringComparison.Ordinal)) return false;
            if (line == Prefix + "state=completed")
            {
                completed = true;
                return true;
            }
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var part in line.Substring(Prefix.Length).Split(' '))
            {
                var pair = part.Split('=');
                if (pair.Length != 2 || values.ContainsKey(pair[0])) return false;
                values.Add(pair[0], pair[1]);
            }
            if (values.Count != 5 || !values.ContainsKey("item") || !values.ContainsKey("status")
                || !values.ContainsKey("count") || !values.ContainsKey("ms") || !values.ContainsKey("failure")) return false;
            var kinds = new[] { "availability", "system", "if-mib", "lldp-mib", "bridge-mib", "q-bridge-mib" };
            var statuses = new[] { "ok", "partial", "absent", "failed", "not-checked" };
            var kind = Array.IndexOf(kinds, values["item"]);
            var status = Array.IndexOf(statuses, values["status"]);
            if (kind < 0 || status < 0) return false;
            int? count;
            long? milliseconds;
            if (!TryCount(values["count"], out count) || !TryMilliseconds(values["ms"], out milliseconds)) return false;
            SnmpTransportFailure? failure;
            switch (values["failure"])
            {
                case "none": failure = null; break;
                case "timeout": failure = SnmpTransportFailure.Timeout; break;
                case "socket": failure = SnmpTransportFailure.Socket; break;
                case "protocol": failure = SnmpTransportFailure.Protocol; break;
                case "unsupported": failure = SnmpTransportFailure.UnsupportedCredentials; break;
                case "authentication": failure = SnmpTransportFailure.Authentication; break;
                default: return false;
            }
            if (failure.HasValue && status != (int)SnmpProfileCheckStatus.Failed) return false;
            item = new SnmpProfileCheckItem((SnmpProfileCheckKind)kind, (SnmpProfileCheckStatus)status,
                count, milliseconds, failure);
            return true;
        }

        private static bool TryCount(string text, out int? result)
        {
            result = null;
            if (text == "none") return true;
            int value;
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value)) return false;
            result = value;
            return true;
        }

        private static bool TryMilliseconds(string text, out long? result)
        {
            result = null;
            if (text == "none") return true;
            long value;
            if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value)) return false;
            result = value;
            return true;
        }
    }
}
