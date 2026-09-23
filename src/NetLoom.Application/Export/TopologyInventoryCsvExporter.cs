using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NetLoom.Contracts.Diagnostics;

namespace NetLoom.Application.Export
{
    public sealed class TopologyInventoryCsvExporter
    {
        private static readonly string[]
            Header =
            {
                "DeviceId",
                "DeviceName",
                "ManagementAddress",
                "Location",
                "DeviceLastSeenUtc",
                "DeviceLastResolvedUtc",
                "InterfaceId",
                "IfIndex",
                "InterfaceName",
                "IfName",
                "IfAlias",
                "IfType",
                "IfDescription",
                "MacAddress",
                "AdminStatus",
                "OperStatus",
                "SpeedBps",
                "InterfaceLastSeenUtc",
                "StpState",
                "DegradationStatus",
                "DegradationCapturedUtc",
                "DegradationReasons"
            };

        public byte[] Export(
            TopologyExportSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(
                    nameof(snapshot));
            }

            var diagnostic =
                snapshot.Topology.DiagnosticSnapshot;

            var builder =
                new StringBuilder();

            AppendRow(
                builder,
                Header);

            foreach (var device in
                diagnostic.Devices
                    .OrderBy(item => item.DeviceId))
            {
                var interfaces =
                    device.Interfaces
                        .OrderBy(
                            item =>
                                item.IfIndex.HasValue
                                    ? 0
                                    : 1)
                        .ThenBy(
                            item =>
                                item.IfIndex ??
                                int.MaxValue)
                        .ThenBy(item => item.InterfaceId)
                        .ToArray();

                if (interfaces.Length == 0)
                {
                    AppendDeviceRow(
                        builder,
                        device,
                        null);

                    continue;
                }

                foreach (var networkInterface in
                    interfaces)
                {
                    AppendDeviceRow(
                        builder,
                        device,
                        networkInterface);
                }
            }

            var content =
                Encoding.UTF8.GetBytes(
                    builder.ToString());

            var preamble =
                new UTF8Encoding(true)
                    .GetPreamble();

            var result =
                new byte[
                    preamble.Length +
                    content.Length];

            Buffer.BlockCopy(
                preamble,
                0,
                result,
                0,
                preamble.Length);

            Buffer.BlockCopy(
                content,
                0,
                result,
                preamble.Length,
                content.Length);

            return result;
        }

        private static void AppendDeviceRow(
            StringBuilder builder,
            DeviceDiagnostic device,
            InterfaceDiagnostic networkInterface)
        {
            AppendRow(
                builder,
                new[]
                {
                    device.DeviceId.ToString("D"),
                    device.DisplayName,
                    device.ManagementAddress,
                    device.LocationName,
                    FormatUtc(device.LastSeenUtc),
                    FormatUtc(device.LastResolvedUtc),
                    networkInterface == null
                        ? null
                        : networkInterface.InterfaceId.ToString("D"),
                    networkInterface == null ||
                    !networkInterface.IfIndex.HasValue
                        ? null
                        : networkInterface.IfIndex.Value.ToString(
                            CultureInfo.InvariantCulture),
                    networkInterface == null
                        ? null
                        : networkInterface.DisplayName,
                    networkInterface == null
                        ? null
                        : networkInterface.IfName,
                    networkInterface == null
                        ? null
                        : networkInterface.IfAlias,
                    networkInterface == null ||
                    !networkInterface.IfType.HasValue
                        ? null
                        : networkInterface.IfType.Value.ToString(
                            CultureInfo.InvariantCulture),
                    networkInterface == null
                        ? null
                        : networkInterface.IfDescription,
                    networkInterface == null
                        ? null
                        : networkInterface.MacAddress,
                    networkInterface == null
                        ? null
                        : networkInterface.AdminStatus,
                    networkInterface == null
                        ? null
                        : networkInterface.OperStatus,
                    networkInterface == null ||
                    !networkInterface.SpeedBps.HasValue
                        ? null
                        : networkInterface.SpeedBps.Value.ToString(
                            CultureInfo.InvariantCulture),
                    networkInterface == null
                        ? null
                        : FormatUtc(
                            networkInterface.LastSeenUtc),
                    networkInterface == null
                        ? null
                        : networkInterface.StpState.ToString(),
                    networkInterface == null
                        ? null
                        : networkInterface.DegradationStatus.ToString(),
                    networkInterface == null
                        ? null
                        : FormatUtc(
                            networkInterface.DegradationCapturedUtc),
                    networkInterface == null
                        ? null
                        : string.Join(
                            "|",
                            networkInterface
                                .DegradationReasons
                                .Select(item => item.ToString()))
                });
        }

        private static void AppendRow(
            StringBuilder builder,
            IEnumerable<string> values)
        {
            builder.Append(
                string.Join(
                    ";",
                    values.Select(Escape)));

            builder.Append("\r\n");
        }

        private static string Escape(
            string value)
        {
            var normalized =
                value ?? string.Empty;

            if (normalized.IndexOf(';') < 0 &&
                normalized.IndexOf('"') < 0 &&
                normalized.IndexOf('\r') < 0 &&
                normalized.IndexOf('\n') < 0)
            {
                return normalized;
            }

            return
                "\"" +
                normalized.Replace(
                    "\"",
                    "\"\"") +
                "\"";
        }

        private static string FormatUtc(
            DateTime? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return value.Value.ToString(
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                CultureInfo.InvariantCulture);
        }
    }
}
