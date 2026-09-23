using System;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Export;
using NetLoom.Application.MapLayout;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.StpTree;
using NetLoom.Contracts.TopologyMap;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class
        Sprint44InventoryCsvExportTests
    {
        [TestMethod]
        public void
            WritesUtf8BomAndExcelCompatibleSemicolonCsvFromDiagnostics()
        {
            var bytes =
                new TopologyInventoryCsvExporter()
                    .Export(
                        CreateExportSnapshot());

            Assert.IsTrue(
                bytes.Length > 3);

            Assert.AreEqual(
                (byte)0xEF,
                bytes[0]);

            Assert.AreEqual(
                (byte)0xBB,
                bytes[1]);

            Assert.AreEqual(
                (byte)0xBF,
                bytes[2]);

            var text =
                Encoding.UTF8.GetString(
                    bytes,
                    3,
                    bytes.Length - 3);

            Assert.IsTrue(
                text.StartsWith(
                    "DeviceId;DeviceName;ManagementAddress;Location;",
                    StringComparison.Ordinal));

            Assert.IsTrue(
                text.EndsWith(
                    "\r\n",
                    StringComparison.Ordinal));

            StringAssert.Contains(
                text,
                "2026-09-23T12:00:00.000Z");

            StringAssert.Contains(
                text,
                "ErrorRateThresholdExceeded|DiscardRateThresholdExceeded");
        }

        [TestMethod]
        public void
            EscapesCsvFieldsAndKeepsDevicesWithoutInterfaces()
        {
            var bytes =
                new TopologyInventoryCsvExporter()
                    .Export(
                        CreateExportSnapshot());

            var text =
                Encoding.UTF8.GetString(
                    bytes,
                    3,
                    bytes.Length - 3);

            var lines =
                text.Split(
                    new[] { "\r\n" },
                    StringSplitOptions.RemoveEmptyEntries);

            Assert.AreEqual(
                3,
                lines.Length,
                "Header plus one interface row and one device-only row are expected.");

            StringAssert.Contains(
                lines[1],
                "\"Core; \"\"A\"\"\"");

            var deviceOnlyColumns =
                lines[2].Split(';');

            Assert.AreEqual(
                22,
                deviceOnlyColumns.Length);

            Assert.AreEqual(
                "22222222-2222-2222-2222-222222222222",
                deviceOnlyColumns[0]);

            Assert.AreEqual(
                "Access",
                deviceOnlyColumns[1]);

            Assert.AreEqual(
                string.Empty,
                deviceOnlyColumns[6],
                "A device without interfaces must still be exported with empty interface columns.");
        }

        [TestMethod]
        public void
            OrdersRowsByStableDeviceAndInterfaceIdentity()
        {
            var bytes =
                new TopologyInventoryCsvExporter()
                    .Export(
                        CreateExportSnapshot());

            var text =
                Encoding.UTF8.GetString(
                    bytes,
                    3,
                    bytes.Length - 3);

            var lines =
                text.Split(
                    new[] { "\r\n" },
                    StringSplitOptions.RemoveEmptyEntries);

            Assert.IsTrue(
                lines[1].StartsWith(
                    "11111111-1111-1111-1111-111111111111;",
                    StringComparison.Ordinal));

            Assert.IsTrue(
                lines[2].StartsWith(
                    "22222222-2222-2222-2222-222222222222;",
                    StringComparison.Ordinal));
        }

        private static TopologyExportSnapshot
            CreateExportSnapshot()
        {
            var generatedUtc =
                new DateTime(
                    2026,
                    9,
                    23,
                    12,
                    0,
                    0,
                    DateTimeKind.Utc);

            var firstDeviceId =
                new Guid(
                    "11111111-1111-1111-1111-111111111111");

            var secondDeviceId =
                new Guid(
                    "22222222-2222-2222-2222-222222222222");

            var firstInterface =
                new InterfaceDiagnostic(
                    new Guid(
                        "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    firstDeviceId,
                    7,
                    "Gi1/0/7",
                    "00:11:22:33:44:55",
                    "Up",
                    "Up",
                    1000000000L,
                    generatedUtc,
                    StpTreePortState.Forwarding,
                    DiagnosticDegradationStatus.Degraded,
                    generatedUtc,
                    new[]
                    {
                        DiagnosticDegradationReason
                            .ErrorRateThresholdExceeded,
                        DiagnosticDegradationReason
                            .DiscardRateThresholdExceeded
                    },
                    "Gi1/0/7",
                    "uplink",
                    6,
                    "Gigabit Ethernet");

            var diagnostics =
                new NetworkDiagnosticSnapshot(
                    generatedUtc,
                    new[]
                    {
                        new DeviceDiagnostic(
                            secondDeviceId,
                            "Access",
                            null,
                            "Room 2",
                            generatedUtc,
                            null,
                            new InterfaceDiagnostic[0],
                            "192.0.2.20"),
                        new DeviceDiagnostic(
                            firstDeviceId,
                            "Core; \"A\"",
                            "primary",
                            "Rack 1",
                            generatedUtc,
                            generatedUtc,
                            new[]
                            {
                                firstInterface
                            },
                            "192.0.2.10")
                    },
                    new PhysicalLinkDiagnostic[0]);

            var topology =
                new TopologyRefreshSnapshot(
                    new MapSnapshot(
                        generatedUtc,
                        new MapNode[0],
                        new MapLink[0]),
                    new TopologyAlertSnapshot(
                        generatedUtc,
                        "cist",
                        new TopologyAlert[0]),
                    diagnostics);

            return new TopologyExportSnapshot(
                topology,
                new TopologyExportLayoutSnapshot(
                    MapLayoutScope.PhysicalTopologyMapId,
                    new MapDeviceLayout[0],
                    new MapLocationLayout[0]));
        }
    }
}
