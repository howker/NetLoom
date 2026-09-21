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

        public string TargetsFilePath { get; private set; }

        public int MaxConcurrentPolls { get; private set; }

        public int StartupJitterSeconds { get; private set; }

        public int Port { get; private set; }

        public SnmpVersion Version { get; private set; }

        public int TimeoutMilliseconds { get; private set; }

        public int RetryCount { get; private set; }

        public int MaxRepetitions { get; private set; }

        public int IntervalSeconds { get; private set; }

        public int DeliveryStatusLimit { get; private set; }

        public bool ControlStdin { get; private set; }

        public string DiscoveryCidr { get; private set; }

        public string DiscoveryStartAddress { get; private set; }

        public string DiscoveryEndAddress { get; private set; }

        public string DiscoverySubnetMask { get; private set; }

        public Guid? AccessProfileId { get; private set; }

        public IReadOnlyList<int> DiscoveryTcpPorts { get; private set; }

        public int DiscoveryIcmpTimeoutMilliseconds { get; private set; }

        public int DiscoveryTcpTimeoutMilliseconds { get; private set; }

        public int DiscoveryInterAddressDelayMilliseconds { get; private set; }

        public int DiscoveryMaxAddresses { get; private set; }

        public double? InterfaceErrorRatePerMinuteThreshold
        {
            get;
            private set;
        }

        public double? InterfaceDiscardRatePerMinuteThreshold
        {
            get;
            private set;
        }

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

            if (command == "smtp-readiness")
            {
                if (args.Length != 1)
                {
                    throw Invalid(
                        "SMTP_READINESS_TAKES_NO_ARGUMENTS");
                }

                return new EngineCommandLine
                {
                    Command = command
                };
            }

            if (command == "smtp-acceptance")
            {
                if (args.Length != 1)
                {
                    throw Invalid(
                        "SMTP_ACCEPTANCE_TAKES_NO_ARGUMENTS");
                }

                return new EngineCommandLine
                {
                    Command = command
                };
            }

            if (command == "delivery-status")
            {
                var deliveryValues =
                    ParseOptions(
                        args.Skip(1).ToArray(),
                        new[]
                        {
                            "database",
                            "limit"
                        });

                return new EngineCommandLine
                {
                    Command = command,
                    DatabasePath =
                        Get(
                            deliveryValues,
                            "database"),
                    DeliveryStatusLimit =
                        ParseInt(
                            Get(
                                deliveryValues,
                                "limit"),
                            50,
                            1,
                            1000,
                            "INVALID_DELIVERY_STATUS_LIMIT")
                };
            }

            if (command == "discover")
            {
                var discoveryValues =
                    ParseOptions(
                        args.Skip(1).ToArray(),
                        new[]
                        {
                            "cidr",
                            "start-address",
                            "end-address",
                            "subnet-mask",
                            "access-profile-id",
                            "port",
                            "version",
                            "timeout-ms",
                            "retries",
                            "max-repetitions",
                            "icmp-timeout-ms",
                            "tcp-timeout-ms",
                            "tcp-ports",
                            "inter-address-delay-ms",
                            "max-addresses",
                            "control-stdin"
                        });

                var discoveryCidr =
                    Get(
                        discoveryValues,
                        "cidr");

                var discoveryStartAddress =
                    Get(
                        discoveryValues,
                        "start-address");

                var discoveryEndAddress =
                    Get(
                        discoveryValues,
                        "end-address");

                var discoverySubnetMask =
                    Get(
                        discoveryValues,
                        "subnet-mask");

                var hasCidr =
                    !string.IsNullOrWhiteSpace(
                        discoveryCidr);

                var hasRangeValue =
                    !string.IsNullOrWhiteSpace(
                        discoveryStartAddress) ||
                    !string.IsNullOrWhiteSpace(
                        discoveryEndAddress) ||
                    !string.IsNullOrWhiteSpace(
                        discoverySubnetMask);

                if (hasCidr && hasRangeValue)
                {
                    throw Invalid(
                        "DISCOVERY_SCOPE_CONFLICT");
                }

                if (!hasCidr)
                {
                    discoveryStartAddress =
                        Required(
                            discoveryValues,
                            "start-address");
                    discoveryEndAddress =
                        Required(
                            discoveryValues,
                            "end-address");
                    discoverySubnetMask =
                        Required(
                            discoveryValues,
                            "subnet-mask");
                }

                return new EngineCommandLine
                {
                    Command = command,
                    DiscoveryCidr =
                        hasCidr
                            ? discoveryCidr.Trim()
                            : null,
                    DiscoveryStartAddress =
                        hasCidr
                            ? null
                            : discoveryStartAddress.Trim(),
                    DiscoveryEndAddress =
                        hasCidr
                            ? null
                            : discoveryEndAddress.Trim(),
                    DiscoverySubnetMask =
                        hasCidr
                            ? null
                            : discoverySubnetMask.Trim(),
                    AccessProfileId =
                        ParseRequiredGuid(
                            Required(
                                discoveryValues,
                                "access-profile-id"),
                            "INVALID_ACCESS_PROFILE_ID"),
                    Port = ParseInt(
                        Get(discoveryValues, "port"),
                        161,
                        1,
                        65535,
                        "INVALID_PORT"),
                    Version = ParseEnum<SnmpVersion>(
                        Get(discoveryValues, "version") ?? "V2C",
                        "INVALID_SNMP_VERSION"),
                    TimeoutMilliseconds = ParseInt(
                        Get(discoveryValues, "timeout-ms"),
                        750,
                        1,
                        int.MaxValue,
                        "INVALID_TIMEOUT"),
                    RetryCount = ParseInt(
                        Get(discoveryValues, "retries"),
                        0,
                        0,
                        int.MaxValue,
                        "INVALID_RETRY_COUNT"),
                    MaxRepetitions = ParseInt(
                        Get(discoveryValues, "max-repetitions"),
                        10,
                        1,
                        int.MaxValue,
                        "INVALID_MAX_REPETITIONS"),
                    DiscoveryIcmpTimeoutMilliseconds =
                        ParseInt(
                            Get(
                                discoveryValues,
                                "icmp-timeout-ms"),
                            500,
                            1,
                            int.MaxValue,
                            "INVALID_ICMP_TIMEOUT"),
                    DiscoveryTcpTimeoutMilliseconds =
                        ParseInt(
                            Get(
                                discoveryValues,
                                "tcp-timeout-ms"),
                            500,
                            1,
                            int.MaxValue,
                            "INVALID_TCP_TIMEOUT"),
                    DiscoveryTcpPorts = ParsePorts(
                        Get(discoveryValues, "tcp-ports") ??
                        "22,80,443"),
                    DiscoveryInterAddressDelayMilliseconds =
                        ParseInt(
                            Get(
                                discoveryValues,
                                "inter-address-delay-ms"),
                            50,
                            0,
                            int.MaxValue,
                            "INVALID_INTER_ADDRESS_DELAY"),
                    DiscoveryMaxAddresses = ParseInt(
                        Get(discoveryValues, "max-addresses"),
                        4096,
                        1,
                        65536,
                        "INVALID_MAX_ADDRESSES"),
                    ControlStdin = ParseBoolean(
                        Get(discoveryValues, "control-stdin"),
                        true,
                        "INVALID_CONTROL_STDIN")
                };
            }

            if (command != "poll-once" &&
                command != "schedule" &&
                command != "schedule-set")
            {
                throw Invalid("UNKNOWN_COMMAND");
            }

            var values =
                ParseOptions(
                    args.Skip(1).ToArray(),
                    new[]
                    {
                        "address",
                        "database",
                        "device-id",
                        "targets-file",
                        "max-concurrency",
                        "startup-jitter-seconds",
                        "port",
                        "version",
                        "timeout-ms",
                        "retries",
                        "max-repetitions",
                        "kinds",
                        "interval-seconds",
                        "control-stdin",
                        "interface-error-rate-per-minute",
                        "interface-discard-rate-per-minute"
                    });

            if (command == "poll-once" &&
                Get(values, "interval-seconds") != null)
            {
                throw Invalid(
                    "INTERVAL_ONLY_VALID_FOR_SCHEDULE");
            }

            if (command == "poll-once" &&
                Get(values, "control-stdin") != null)
            {
                throw Invalid(
                    "CONTROL_STDIN_ONLY_VALID_FOR_SCHEDULE");
            }

            var version =
                ParseEnum<SnmpVersion>(
                    Get(values, "version") ?? "V2C",
                    "INVALID_SNMP_VERSION");

            var intervalSeconds =
                ParseInt(
                    Get(values, "interval-seconds"),
                    60,
                    1,
                    int.MaxValue,
                    "INVALID_INTERVAL_SECONDS");

            var port =
                ParseInt(
                    Get(values, "port"),
                    161,
                    1,
                    65535,
                    "INVALID_PORT");

            var timeoutMilliseconds =
                ParseInt(
                    Get(values, "timeout-ms"),
                    2000,
                    1,
                    int.MaxValue,
                    "INVALID_TIMEOUT");

            var retryCount =
                ParseInt(
                    Get(values, "retries"),
                    1,
                    0,
                    int.MaxValue,
                    "INVALID_RETRY_COUNT");

            var maxRepetitions =
                ParseInt(
                    Get(values, "max-repetitions"),
                    25,
                    1,
                    int.MaxValue,
                    "INVALID_MAX_REPETITIONS");

            var controlStdin =
                ParseBoolean(
                    Get(values, "control-stdin"),
                    false,
                    "INVALID_CONTROL_STDIN");

            var errorThreshold =
                ParseOptionalPositiveDouble(
                    Get(
                        values,
                        "interface-error-rate-per-minute"),
                    "INVALID_INTERFACE_ERROR_RATE_PER_MINUTE");

            var discardThreshold =
                ParseOptionalPositiveDouble(
                    Get(
                        values,
                        "interface-discard-rate-per-minute"),
                    "INVALID_INTERFACE_DISCARD_RATE_PER_MINUTE");

            var kinds =
                ParseKinds(
                    Get(values, "kinds"));

            if (command == "schedule-set")
            {
                if (Get(values, "address") != null ||
                    Get(values, "device-id") != null)
                {
                    throw Invalid(
                        "SCHEDULE_SET_REQUIRES_TARGETS_FILE");
                }

                var maxConcurrency =
                    ParseInt(
                        Required(
                            values,
                            "max-concurrency"),
                        1,
                        1,
                        64,
                        "INVALID_MAX_CONCURRENCY");

                var startupJitterSeconds =
                    ParseInt(
                        Required(
                            values,
                            "startup-jitter-seconds"),
                        0,
                        0,
                        int.MaxValue,
                        "INVALID_STARTUP_JITTER_SECONDS");

                if (startupJitterSeconds >
                    intervalSeconds)
                {
                    throw Invalid(
                        "STARTUP_JITTER_EXCEEDS_INTERVAL");
                }

                return new EngineCommandLine
                {
                    Command = command,
                    DatabasePath = Get(values, "database"),
                    TargetsFilePath =
                        Required(
                            values,
                            "targets-file").Trim(),
                    MaxConcurrentPolls = maxConcurrency,
                    StartupJitterSeconds =
                        startupJitterSeconds,
                    Port = port,
                    Version = version,
                    TimeoutMilliseconds =
                        timeoutMilliseconds,
                    RetryCount = retryCount,
                    MaxRepetitions = maxRepetitions,
                    IntervalSeconds = intervalSeconds,
                    ControlStdin = controlStdin,
                    InterfaceErrorRatePerMinuteThreshold =
                        errorThreshold,
                    InterfaceDiscardRatePerMinuteThreshold =
                        discardThreshold,
                    Kinds = kinds
                };
            }

            if (Get(values, "targets-file") != null ||
                Get(values, "max-concurrency") != null ||
                Get(values, "startup-jitter-seconds") != null)
            {
                throw Invalid(
                    "TARGET_SET_OPTIONS_ONLY_VALID_FOR_SCHEDULE_SET");
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

            return new EngineCommandLine
            {
                Command = command,
                DatabasePath = Get(values, "database"),
                Address = address,
                DeviceId = ParseOptionalGuid(
                    Get(values, "device-id")),
                Port = port,
                Version = version,
                TimeoutMilliseconds =
                    timeoutMilliseconds,
                RetryCount = retryCount,
                MaxRepetitions = maxRepetitions,
                IntervalSeconds = intervalSeconds,
                ControlStdin = controlStdin,
                InterfaceErrorRatePerMinuteThreshold =
                    errorThreshold,
                InterfaceDiscardRatePerMinuteThreshold =
                    discardThreshold,
                Kinds = kinds
            };
        }

        private static Dictionary<string, string>
            ParseOptions(
                string[] args,
                IEnumerable<string> allowedOptions)
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
                    allowedOptions,
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

        private static Guid ParseRequiredGuid(
            string value,
            string errorCode)
        {
            Guid parsed;

            if (!Guid.TryParse(
                    value,
                    out parsed) ||
                parsed == Guid.Empty)
            {
                throw Invalid(errorCode);
            }

            return parsed;
        }

        private static IReadOnlyList<int> ParsePorts(
            string value)
        {
            var result =
                new List<int>();

            foreach (var token in
                (value ?? string.Empty).Split(
                    new[] { ',' },
                    StringSplitOptions.RemoveEmptyEntries))
            {
                int port;

                if (!int.TryParse(
                        token.Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out port) ||
                    port < 1 ||
                    port > 65535)
                {
                    throw Invalid("INVALID_DISCOVERY_TCP_PORTS");
                }

                if (!result.Contains(port))
                {
                    result.Add(port);
                }
            }

            if (result.Count == 0)
            {
                throw Invalid("EMPTY_DISCOVERY_TCP_PORTS");
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

        private static bool ParseBoolean(
            string value,
            bool defaultValue,
            string errorCode)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            bool parsed;

            if (!bool.TryParse(
                value,
                out parsed))
            {
                throw Invalid(errorCode);
            }

            return parsed;
        }

        private static double? ParseOptionalPositiveDouble(
            string value,
            string errorCode)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            double parsed;

            if (!double.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out parsed) ||
                double.IsNaN(parsed) ||
                double.IsInfinity(parsed) ||
                parsed <= 0.0)
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
