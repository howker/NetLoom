using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Export;
using NetLoom.Application.MapLayout;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.TopologyMap;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class
        Sprint44TopologyExportDiagramTests
    {
        [TestMethod]
        public void
            UsesPersistedDeviceAndLocationGeometry()
        {
            var deviceId =
                new Guid(
                    "11111111-1111-1111-1111-111111111111");

            var locationId =
                new Guid(
                    "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

            var snapshot =
                CreateSnapshot(
                    new[]
                    {
                        new MapNode(
                            "device-1",
                            "Core",
                            "10.0.0.1",
                            9000.0,
                            9000.0,
                            locationId,
                            deviceId: deviceId)
                    },
                    new MapLink[0],
                    new[]
                    {
                        new MapLocation(
                            locationId,
                            null,
                            "Rack A",
                            null)
                    },
                    new[]
                    {
                        new MapDeviceLayout(
                            deviceId,
                            100.0,
                            200.0,
                            false)
                    },
                    new[]
                    {
                        new MapLocationLayout(
                            locationId,
                            40.0,
                            120.0,
                            500.0,
                            300.0,
                            false,
                            false)
                    });

            var diagram =
                new TopologyExportDiagramBuilder()
                    .Build(
                        snapshot);

            Assert.AreEqual(
                1,
                diagram.Nodes.Count);

            Assert.AreEqual(
                1,
                diagram.Locations.Count);

            Assert.AreEqual(
                60.0 * diagram.Scale,
                diagram.Nodes[0].X -
                diagram.Locations[0].X,
                0.001);

            Assert.AreEqual(
                80.0 * diagram.Scale,
                diagram.Nodes[0].Y -
                diagram.Locations[0].Y,
                0.001);
        }

        [TestMethod]
        public void
            SynthesizesMissingLocationAroundAssignedNode()
        {
            var locationId =
                new Guid(
                    "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

            var snapshot =
                CreateSnapshot(
                    new[]
                    {
                        new MapNode(
                            "device-1",
                            "Core",
                            null,
                            100.0,
                            200.0,
                            locationId)
                    },
                    new MapLink[0],
                    new[]
                    {
                        new MapLocation(
                            locationId,
                            null,
                            "Rack A",
                            null)
                    },
                    new MapDeviceLayout[0],
                    new MapLocationLayout[0]);

            var diagram =
                new TopologyExportDiagramBuilder()
                    .Build(
                        snapshot);

            var node =
                diagram.Nodes[0];

            var location =
                diagram.Locations[0];

            Assert.IsTrue(
                location.X <= node.X);

            Assert.IsTrue(
                location.Y <= node.Y);

            Assert.IsTrue(
                location.X +
                location.Width >=
                node.X +
                node.Width);

            Assert.IsTrue(
                location.Y +
                location.Height >=
                node.Y +
                node.Height);
        }

        [TestMethod]
        public void
            CapsHugeDiagramByDimensionAndPixelBudget()
        {
            var snapshot =
                CreateSnapshot(
                    new[]
                    {
                        new MapNode(
                            "left",
                            "Left",
                            null,
                            0.0,
                            0.0),
                        new MapNode(
                            "right",
                            "Right",
                            null,
                            6000.0,
                            6000.0)
                    },
                    new MapLink[0],
                    new MapLocation[0],
                    new MapDeviceLayout[0],
                    new MapLocationLayout[0]);

            var diagram =
                new TopologyExportDiagramBuilder()
                    .Build(
                        snapshot);

            Assert.IsTrue(
                diagram.PixelWidth <=
                TopologyExportDiagramPolicy.MaxPixelDimension);

            Assert.IsTrue(
                diagram.PixelHeight <=
                TopologyExportDiagramPolicy.MaxPixelDimension);

            Assert.IsTrue(
                (long)diagram.PixelWidth *
                diagram.PixelHeight <=
                TopologyExportDiagramPolicy.MaxPixelCount);

            Assert.IsTrue(
                diagram.Scale < 1.0,
                "The pixel budget, not the live 1,000,000 x 1,000,000 canvas, must bound a large export.");
        }

        [TestMethod]
        public void
            KeepsAllCanonicalMapLinksIndependentOfViewportState()
        {
            var snapshot =
                CreateSnapshot(
                    new[]
                    {
                        new MapNode(
                            "a",
                            "A",
                            null,
                            0.0,
                            0.0),
                        new MapNode(
                            "b",
                            "B",
                            null,
                            400.0,
                            0.0)
                    },
                    new[]
                    {
                        new MapLink(
                            "a-b",
                            "a",
                            "b",
                            "Gi1/0/1",
                            "Gi1/0/2",
                            MapConfidence.High,
                            MapFreshness.Fresh,
                            new MapEvidenceItem[0])
                    },
                    new MapLocation[0],
                    new MapDeviceLayout[0],
                    new MapLocationLayout[0]);

            var diagram =
                new TopologyExportDiagramBuilder()
                    .Build(
                        snapshot);

            Assert.AreEqual(
                2,
                diagram.Nodes.Count);

            Assert.AreEqual(
                1,
                diagram.Links.Count);

            Assert.AreEqual(
                "Gi1/0/1 \u2194 Gi1/0/2",
                diagram.Links[0].Label);
        }

        private static TopologyExportSnapshot
            CreateSnapshot(
                MapNode[] nodes,
                MapLink[] links,
                MapLocation[] locations,
                MapDeviceLayout[] deviceLayouts,
                MapLocationLayout[] locationLayouts)
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

            var topology =
                new TopologyRefreshSnapshot(
                    new MapSnapshot(
                        generatedUtc,
                        nodes,
                        links,
                        locations),
                    new TopologyAlertSnapshot(
                        generatedUtc,
                        "cist",
                        new TopologyAlert[0]));

            return new TopologyExportSnapshot(
                topology,
                new TopologyExportLayoutSnapshot(
                    MapLayoutScope.PhysicalTopologyMapId,
                    deviceLayouts,
                    locationLayouts));
        }
    }
}
