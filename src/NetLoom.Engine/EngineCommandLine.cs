using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using NetLoom.Application.Monitoring;
using NetLoom.Domain.Access;

namespace NetLoom.Engine
{
    internal sealed class EngineCommandLine
    {
        private EngineCommandLine()
        {
        }

        public string Command { get; private set; }

        public string DatabasePath { get; private set; }

        public IPAddress Address { get; private set; }

        public Guid? DeviceId { get; private set; }

        public int Port { get; private set; }

        public SnmpVersion Version { get; private set; }

        public int TimeoutMilliseconds { get; private set; }

        public int RetryCount { get; private set; }

        public int MaxRepetitions { get; private set; }

        public int IntervalSeconds { get; private set; }

        public IReadOnlyList<MonitoringPollKind> Kinds { get; private set; }

        public static EngineCommandLine Parse(
            string[] args)
        {
            if (args == null ||
                args.Length == 0)
            {
                throw Invalid("MISSING_COMMAND");
            }

            var command =
                args[0].Trim().ToLowerInvariant();

            if (command == "runtime-smoke")
            {
                if (args.Length != 1)
                {
                    throw Invalid(
                        "RUNTIME_SMOKE_TAKES_NO_ARGUMENTS");
                }

                return new EngineCommandLine
                {
                    Command = command
                };
            }

            if (command != "poll-once" &&
                command != "schedule")
            {
                throw Invalid("UNKNOWN_COMMAND");
            }

            var values =
                ParseOptions(
                    args.Skip(1).ToArray());

            if (command == "poll-once" &&
                Get(values, "interval-seconds") != null)
            {
                throw Invalid(
                    "INTERVAL_ONLY_VALID_FOR_SCHEDULE");
            }

            var addressText =
                Required(values, "address");

            IPAddress address;

            if (!IPAddress.TryParse(
                addressText,
                out address))
            {
                throw Invalid("INVALID_ADDRESS");
            }

            var version =
                ParseEnum<SnmpVersion>(
                    Get(values, "version") ?? "V2C",
                    "INVALID_SNMP_VERSION");

            return new EngineCommandLine
            {
                Command = command,
                DatabasePath = Get(values, "database"),
                Address = address,
                DeviceId = ParseOptionalGuid(
                    Get(values, "device-id")),
                Port = ParseInt(
                    Get(values, "port"),
                    161,
                    1,
                    65535,
                    "INVALID_PORT"),
                Version = version,
                TimeoutMilliseconds = ParseInt(
                    Get(values, "timeout-ms"),
                    2000,
                    1,
                    int.MaxValue,
                    "INVALID_TIMEOUT"),
                RetryCount = ParseInt(
                    Get(values, "retries"),
                    1,
                    0,
                    int.MaxValue,
                    "INVALID_RETRY_COUNT"),
                MaxRepetitions = ParseInt(
                    Get(values, "max-repetitions"),
                    25,
                    1,
                    int.MaxValue,
                    "INVALID_MAX_REPETITIONS"),
                IntervalSeconds = ParseInt(
                    Get(values, "interval-seconds"),
                    60,
                    1,
                    int.MaxValue,
                    "INVALID_INTERVAL_SECONDS"),
                Kinds = ParseKinds(
                    Get(values, "kinds"))
            };
        }

        private static Dictionary<string, string>
            ParseOptions(
                string[] args)
        {
            var result =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

            for (var index = 0;
                 index < args.Length;
                 index += 2)
            {
                if (index + 1 >= args.Length ||
                    !args[index].StartsWith(
                        "--",
                        StringComparison.Ordinal))
                {
                    throw Invalid("INVALID_OPTION_PAIR");
                }

                var key =
                    args[index].Substring(2);

                if (string.IsNullOrWhiteSpace(key) ||
                    result.ContainsKey(key))
                {
                    throw Invalid("INVALID_OR_DUPLICATE_OPTION");
                }

                result.Add(
                    key,
                    args[index + 1]);
            }

            var allowed =
                new HashSet<string>(
                    new[]
                    {
                        "address",
                        "database",
                        "device-id",
                        "port",
                        "version",
                        "timeout-ms",
                        "retries",
                        "max-repetitions",
                        "kinds",
                        "interval-seconds"
                    },
                    StringComparer.OrdinalIgnoreCase);

            foreach (var key in result.Keys)
            {
                if (!allowed.Contains(key))
                {
                    throw Invalid("UNKNOWN_OPTION_" + key);
                }
            }

            return result;
        }

        private static IReadOnlyList<MonitoringPollKind>
            ParseKinds(
                string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new[]
                {
                    MonitoringPollKind.Lldp,
                    MonitoringPollKind.Cdp,
                    MonitoringPollKind.Fdb,
                    MonitoringPollKind.Arp,
                    MonitoringPollKind.Health,
                    MonitoringPollKind.Interface,
                    MonitoringPollKind.Stp
                };
            }

            var result =
                new List<MonitoringPollKind>();

            foreach (var token in
                value.Split(
                    new[] { ',' },
                    StringSplitOptions.RemoveEmptyEntries))
            {
                MonitoringPollKind kind;

                if (!Enum.TryParse(
                    token.Trim(),
                    true,
                    out kind))
                {
                    throw Invalid("INVALID_POLL_KIND");
                }

                if (!result.Contains(kind))
                {
                    result.Add(kind);
                }
            }

            if (result.Count == 0)
            {
                throw Invalid("EMPTY_POLL_KIND_SET");
            }

            return result;
        }

        private static Guid? ParseOptionalGuid(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            Guid parsed;

            if (!Guid.TryParse(
                value,
                out parsed) ||
                parsed == Guid.Empty)
            {
                throw Invalid("INVALID_DEVICE_ID");
            }

            return parsed;
        }

        private static int ParseInt(
            string value,
            int defaultValue,
            int minimum,
            int maximum,
            string errorCode)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            int parsed;

            if (!int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out parsed) ||
                parsed < minimum ||
                parsed > maximum)
            {
                throw Invalid(errorCode);
            }

            return parsed;
        }

        private static T ParseEnum<T>(
            string value,
            string errorCode)
            where T : struct
        {
            T parsed;

            if (!Enum.TryParse(
                value,
                true,
                out parsed))
            {
                throw Invalid(errorCode);
            }

            return parsed;
        }

        private static string Required(
            IDictionary<string, string> values,
            string key)
        {
            var value = Get(values, key);

            if (string.IsNullOrWhiteSpace(value))
            {
                throw Invalid(
                    "MISSING_" +
                    key.ToUpperInvariant());
            }

            return value;
        }

        private static string Get(
            IDictionary<string, string> values,
            string key)
        {
            string value;

            return values.TryGetValue(key, out value)
                ? value
                : null;
        }

        private static ArgumentException Invalid(
            string code)
        {
            return new ArgumentException(code);
        }
    }
}
