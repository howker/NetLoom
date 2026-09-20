using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Desktop.Monitoring;

namespace NetLoom.Desktop.Discovery
{
    internal static class EngineDiscoveryCommandBuilder
    {
        public static IReadOnlyList<string> BuildTokens(
            DiscoveryControlRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(
                    nameof(request));
            }

            var scopeTokens =
                request.UsesAddressRange
                    ? new[]
                    {
                        "--start-address",
                        request.StartAddress,
                        "--end-address",
                        request.EndAddress,
                        "--subnet-mask",
                        request.SubnetMask
                    }
                    : new[]
                    {
                        "--cidr",
                        request.Cidr
                    };

            return new[]
            {
                "discover"
            }
            .Concat(scopeTokens)
            .Concat(
                new[]
                {
                    "--access-profile-id",
                    request.AccessProfileId.ToString("D"),
                    "--port",
                request.Port.ToString(
                    CultureInfo.InvariantCulture),
                "--version",
                request.Version.ToString(),
                "--timeout-ms",
                request.TimeoutMilliseconds.ToString(
                    CultureInfo.InvariantCulture),
                "--retries",
                request.RetryCount.ToString(
                    CultureInfo.InvariantCulture),
                "--max-repetitions",
                request.MaxRepetitions.ToString(
                    CultureInfo.InvariantCulture),
                "--icmp-timeout-ms",
                request.IcmpTimeoutMilliseconds.ToString(
                    CultureInfo.InvariantCulture),
                "--tcp-timeout-ms",
                request.TcpTimeoutMilliseconds.ToString(
                    CultureInfo.InvariantCulture),
                "--tcp-ports",
                string.Join(
                    ",",
                    request.TcpPorts.Select(
                        port =>
                            port.ToString(
                                CultureInfo.InvariantCulture))),
                "--inter-address-delay-ms",
                request.InterAddressDelayMilliseconds.ToString(
                    CultureInfo.InvariantCulture),
                "--max-addresses",
                request.MaxAddresses.ToString(
                    CultureInfo.InvariantCulture),
                "--control-stdin",
                "true"
                })
            .ToArray();
        }

        public static string FormatArguments(
            IEnumerable<string> tokens)
        {
            return EngineMonitoringCommandBuilder
                .FormatArguments(
                    tokens);
        }
    }
}
