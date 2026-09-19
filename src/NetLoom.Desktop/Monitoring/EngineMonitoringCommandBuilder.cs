using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NetLoom.Application.MonitoringControl;

namespace NetLoom.Desktop.Monitoring
{
    internal static class EngineMonitoringCommandBuilder
    {
        public static IReadOnlyList<string> BuildScheduleTokens(
            MonitoringTarget target,
            MonitoringSessionPolicy policy,
            string databasePath)
        {
            var tokens =
                BuildCommonTokens(
                    "schedule",
                    target,
                    policy,
                    databasePath);

            tokens.Add(
                "--interval-seconds");
            tokens.Add(
                ((int)policy.Interval.TotalSeconds)
                    .ToString(
                        CultureInfo.InvariantCulture));
            tokens.Add(
                "--control-stdin");
            tokens.Add(
                "true");

            return tokens;
        }

        public static IReadOnlyList<string> BuildPollOnceTokens(
            MonitoringTarget target,
            MonitoringSessionPolicy policy,
            string databasePath)
        {
            return BuildCommonTokens(
                "poll-once",
                target,
                policy,
                databasePath);
        }

        public static string FormatArguments(
            IEnumerable<string> tokens)
        {
            if (tokens == null)
            {
                throw new ArgumentNullException(
                    nameof(tokens));
            }

            return string.Join(
                " ",
                tokens.Select(
                    QuoteWindowsArgument));
        }

        private static List<string> BuildCommonTokens(
            string command,
            MonitoringTarget target,
            MonitoringSessionPolicy policy,
            string databasePath)
        {
            if (target == null)
            {
                throw new ArgumentNullException(
                    nameof(target));
            }

            if (policy == null)
            {
                throw new ArgumentNullException(
                    nameof(policy));
            }

            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException(
                    "DATABASE_PATH_REQUIRED",
                    nameof(databasePath));
            }

            var tokens =
                new List<string>
                {
                    command,
                    "--address",
                    target.TargetAddress.ToString(),
                    "--database",
                    databasePath,
                    "--device-id",
                    target.DeviceId.ToString("D"),
                    "--port",
                    policy.Port.ToString(
                        CultureInfo.InvariantCulture),
                    "--version",
                    policy.Version.ToString(),
                    "--timeout-ms",
                    policy.TimeoutMilliseconds.ToString(
                        CultureInfo.InvariantCulture),
                    "--retries",
                    policy.RetryCount.ToString(
                        CultureInfo.InvariantCulture),
                    "--max-repetitions",
                    policy.MaxRepetitions.ToString(
                        CultureInfo.InvariantCulture),
                    "--kinds",
                    string.Join(
                        ",",
                        policy.Kinds.Select(
                            kind => kind.ToString()))
                };

            AddOptionalThreshold(
                tokens,
                "--interface-error-rate-per-minute",
                policy.InterfaceErrorRatePerMinuteThreshold);

            AddOptionalThreshold(
                tokens,
                "--interface-discard-rate-per-minute",
                policy.InterfaceDiscardRatePerMinuteThreshold);

            return tokens;
        }

        private static void AddOptionalThreshold(
            ICollection<string> tokens,
            string option,
            double? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            tokens.Add(
                option);
            tokens.Add(
                value.Value.ToString(
                    "R",
                    CultureInfo.InvariantCulture));
        }

        private static string QuoteWindowsArgument(
            string value)
        {
            if (value == null)
            {
                return "\"\"";
            }

            if (value.Length > 0 &&
                value.IndexOfAny(
                    new[]
                    {
                        ' ',
                        '\t',
                        '\n',
                        '\v',
                        '"'
                    }) < 0)
            {
                return value;
            }

            var builder =
                new StringBuilder();

            builder.Append('"');

            var backslashes = 0;

            foreach (var character in value)
            {
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (character == '"')
                {
                    builder.Append(
                        '\\',
                        backslashes * 2 + 1);
                    builder.Append('"');
                    backslashes = 0;
                    continue;
                }

                if (backslashes > 0)
                {
                    builder.Append(
                        '\\',
                        backslashes);
                    backslashes = 0;
                }

                builder.Append(
                    character);
            }

            if (backslashes > 0)
            {
                builder.Append(
                    '\\',
                    backslashes * 2);
            }

            builder.Append('"');

            return builder.ToString();
        }
    }
}
