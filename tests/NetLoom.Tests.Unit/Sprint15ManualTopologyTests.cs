using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Domain.Locations;
using NetLoom.Domain.Observations;
using NetLoom.Topology.Map;
using NetLoom.Domain.Topology;
using NetLoom.Topology.Lifecycle;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint15ManualTopologyTests
    {
        private static readonly DateTime Now =
            new DateTime(
                2026,
                1,
                1,
                12,
                0,
                0,
                DateTimeKind.Utc);

        [TestMethod]
        public void ManualDeviceHasStableGuidAndNoMonitoring()
        {
            var id = Guid.NewGuid();

            var device =
                new ManualTopologyFactory().CreateDevice(
                    id,
                    null,
                    "Media converter",
                    DeviceCategory.MediaConverter,
                    null);

            Assert.AreEqual(id, device.Id);

            Assert.AreEqual(
                DeviceDiscoveryOrigin.Manual,
                device.DiscoveryOrigin);

            Assert.AreEqual(
                MonitoringCapability.None,
                device.MonitoringCapability);
        }

        [TestMethod]
        public void ManualInterfaceHasNoIfIndex()
        {
            var networkInterface =
                new ManualTopologyFactory().CreateInterface(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "FX",
                    "Fiber");

            Assert.IsTrue(networkInterface.IsManual);
            Assert.IsNull(networkInterface.IfIndex);
        }

        [TestMethod]
        public void ManualLinkIsCommonPhysicalLink()
        {
            var link =
                new ManualTopologyFactory().CreateLink(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    null,
                    Guid.NewGuid(),
                    null,
                    "Fiber",
                    null,
                    Now);

            Assert.AreEqual(
                PhysicalLinkStrength.Manual,
                link.Strength);

            Assert.AreEqual(
                PhysicalLinkFreshness.Fresh,
                link.Freshness);
        }

        [TestMethod]
        public void ManualLifecycleNeverAgesOrDeletes()
        {
            var policy =
                new TopologyLifecyclePolicy(
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(15));

            var state =
                policy.CreateManual(
                    "manual-device",
                    Now).State;

            var afterFailure =
                policy.RecordPollFailure(
                    state,
                    Now.AddDays(365));

            Assert.AreEqual(
                TopologyFreshness.Fresh,
                afterFailure.State.Freshness);

            Assert.IsFalse(afterFailure.ShouldDelete);
        }

        [TestMethod]
        public void ManualObservationUsesExistingObservationModel()
        {
            var observation =
                new ManualTopologyFactory()
                    .CreateObservation(
                        Guid.NewGuid(),
                        Now);

            Assert.AreEqual(
                ObservationKind.Manual,
                observation.Kind);

            Assert.AreEqual(
                "User",
                observation.SourceAddress);
        }

        [TestMethod]
        public void ManualAndAutomaticDevicesShareOneMapGraph()
        {
            var manualFactory =
                new ManualTopologyFactory();

            var automatic =
                new TopologyDevice(
                    Guid.NewGuid(),
                    null,
                    "Core",
                    DeviceCategory.Unknown,
                    DeviceDiscoveryOrigin.Automatic,
                    MonitoringCapability.Unknown,
                    null,
                    null,
                    null,
                    false,
                    false,
                    Now,
                    Now,
                    Now);

            var manual =
                manualFactory.CreateDevice(
                    Guid.NewGuid(),
                    null,
                    "Converter",
                    DeviceCategory.MediaConverter,
                    null);

            var manualPort =
                manualFactory.CreateInterface(
                    Guid.NewGuid(),
                    manual.Id,
                    "FX",
                    "Fiber");

            var link =
                manualFactory.CreateLink(
                    Guid.NewGuid(),
                    automatic.Id,
                    null,
                    manual.Id,
                    manualPort.Id,
                    "Fiber",
                    null,
                    Now);

            var snapshot =
                new MaterializedTopologyMapProjector()
                    .Project(
                        new[] { automatic, manual },
                        new[] { manualPort },
                        new[] { link },
                        new Location[0],
                        Now);

            Assert.AreEqual(
                2,
                snapshot.Nodes.Count);

            Assert.AreEqual(
                1,
                snapshot.Links.Count);

            MapNode manualNode = null;

            foreach (var node in snapshot.Nodes)
            {
                if (node.Origin ==
                    MapNodeOrigin.Manual)
                {
                    manualNode = node;
                }
            }

            Assert.IsNotNull(manualNode);

            Assert.AreEqual(
                MapMonitoringCapability.None,
                manualNode.MonitoringCapability);

            Assert.AreEqual(
                MapNodeCategory.MediaConverter,
                manualNode.Category);

            Assert.AreEqual(
                MapEvidenceKind.Manual,
                snapshot.Links[0].Evidence[0].Kind);

            Assert.AreEqual(
                MapEvidenceStrength.Strong,
                snapshot.Links[0].Evidence[0].Strength);
        }

        [TestMethod]
        public void MaterializedMapDoesNotExposeDeviceId()
        {
            var device =
                new ManualTopologyFactory()
                    .CreateDevice(
                        Guid.NewGuid(),
                        null,
                        "Converter",
                        DeviceCategory.MediaConverter,
                        null);

            var snapshot =
                new MaterializedTopologyMapProjector()
                    .Project(
                        new[] { device },
                        new DeviceInterface[0],
                        new PhysicalLink[0],
                        new Location[0],
                        Now);

            Assert.IsFalse(
                snapshot.Nodes[0].Key.Contains(
                    device.Id.ToString("D")));
        }

        [TestMethod]
        public void LocationOverlayPreservesManualMetadata()
        {
            var locationId =
                Guid.NewGuid();

            var source =
                new MapSnapshot(
                    Now,
                    new[]
                    {
                        new MapNode(
                            "map-manual",
                            "Converter",
                            null,
                            10,
                            20,
                            null,
                            MapNodeOrigin.Manual,
                            MapMonitoringCapability.None,
                            MapNodeCategory.MediaConverter)
                    },
                    new MapLink[0]);

            var result =
                new MapLocationOverlay()
                    .Apply(
                        source,
                        new[]
                        {
                            new Location(
                                locationId,
                                null,
                                "Rack",
                                null)
                        },
                        new[]
                        {
                            new MapLocationAssignment(
                                "map-manual",
                                locationId)
                        });

            Assert.AreEqual(
                MapNodeOrigin.Manual,
                result.Nodes[0].Origin);

            Assert.AreEqual(
                MapMonitoringCapability.None,
                result.Nodes[0].MonitoringCapability);

            Assert.AreEqual(
                MapNodeCategory.MediaConverter,
                result.Nodes[0].Category);
        }
    }
}
