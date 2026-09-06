using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NetLoom.Protocols.Snmp.Interfaces
{
    public static class SnmpInterfaceStatusParser
    {
        private static readonly Regex ParenthesizedValue =
            new Regex(
                @"\((\d+)\)",
                RegexOptions.CultureInvariant);

        public static int? ParseStatus(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            int parsed;

            if (int.TryParse(
                value.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parsed) &&
                parsed > 0)
            {
                return parsed;
            }

            var match =
                ParenthesizedValue.Match(
                    value);

            if (match.Success &&
                int.TryParse(
                    match.Groups[1].Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out parsed) &&
                parsed > 0)
            {
                return parsed;
            }

            return null;
        }

        public static int? ParseIfIndex(
            string oid,
            string columnOid)
        {
            if (string.IsNullOrWhiteSpace(oid) ||
                string.IsNullOrWhiteSpace(columnOid))
            {
                return null;
            }

            var prefix =
                columnOid + ".";

            if (!oid.StartsWith(
                prefix,
                StringComparison.Ordinal))
            {
                return null;
            }

            int parsed;

            return int.TryParse(
                    oid.Substring(prefix.Length),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out parsed) &&
                parsed > 0
                ? (int?)parsed
                : null;
        }
    }
}
