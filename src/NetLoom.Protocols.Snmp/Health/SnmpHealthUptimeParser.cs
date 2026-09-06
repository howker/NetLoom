using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NetLoom.Protocols.Snmp.Health
{
    public static class SnmpHealthUptimeParser
    {
        private static readonly Regex ParenthesizedTicks =
            new Regex(
                @"\((\d+)\)",
                RegexOptions.CultureInvariant);

        public static TimeSpan? Parse(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed =
                value.Trim();

            long ticks;

            if (long.TryParse(
                trimmed,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out ticks) &&
                ticks >= 0)
            {
                return FromHundredths(
                    ticks);
            }

            var match =
                ParenthesizedTicks.Match(
                    trimmed);

            if (match.Success &&
                long.TryParse(
                    match.Groups[1].Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out ticks) &&
                ticks >= 0)
            {
                return FromHundredths(
                    ticks);
            }

            TimeSpan parsed;

            if (TimeSpan.TryParse(
                trimmed,
                CultureInfo.InvariantCulture,
                out parsed) &&
                parsed >= TimeSpan.Zero)
            {
                return parsed;
            }

            var closingParenthesis =
                trimmed.IndexOf(
                    ')');

            if (closingParenthesis >= 0 &&
                closingParenthesis + 1 <
                    trimmed.Length)
            {
                var tail =
                    trimmed.Substring(
                        closingParenthesis + 1).
                        Trim();

                if (TimeSpan.TryParse(
                    tail,
                    CultureInfo.InvariantCulture,
                    out parsed) &&
                    parsed >= TimeSpan.Zero)
                {
                    return parsed;
                }
            }

            return null;
        }

        private static TimeSpan FromHundredths(
            long hundredths)
        {
            return TimeSpan.FromMilliseconds(
                hundredths * 10.0);
        }
    }
}
