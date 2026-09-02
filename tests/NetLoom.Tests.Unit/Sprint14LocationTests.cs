using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Domain.Locations;
using NetLoom.Topology.Map;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint14LocationTests
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
        public void LocationOverlayPreservesNodeAndLinkKeys()
        {
            var locationId =
                Guid.NewGuid();

            var source =
                Snapshot();

            var nodeKey =
                source.Nodes[0].Key;

            var linkKey =
                source.Links[0].Key;

            var result =
                new MapLocationOverlay().Apply(
                    source,
                    new[]
                    {
                        new Location(
                            locationId,
                            null,
                            "Серверная",
                            null)
                    },
                    new[]
                    {
                        new MapLocationAssignment(
                            nodeKey,
                            locationId)
                    });

            Assert.AreEqual(
                nodeKey,
                result.Nodes[0].Key);

            Assert.AreEqual(
                linkKey,
                result.Links[0].Key);

            Assert.AreEqual(
                locationId,
                result.Nodes[0].LocationId);

            Assert.AreEqual(
                1,
                result.Locations.Count);
        }

        [TestMethod]
        public void RenamingLocationDoesNotChangeTopologyIdentity()
        {
            var locationId =
                Guid.NewGuid();

            var source =
                Snapshot();

            var overlay =
                new MapLocationOverlay();

            var first =
                overlay.Apply(
                    source,
                    new[]
                    {
                        new Location(
                            locationId,
                            null,
                            "Шкаф 1",
                            null)
                    },
                    new[]
                    {
                        new MapLocationAssignment(
                            source.Nodes[0].Key,
                            locationId)
                    });

            var renamed =
                overlay.Apply(
                    source,
                    new[]
                    {
                        new Location(
                            locationId,
                            null,
                            "Шкаф ядра",
                            null)
                    },
                    new[]
                    {
                        new MapLocationAssignment(
                            source.Nodes[0].Key,
                            locationId)
                    });

            Assert.AreEqual(
                first.Nodes[0].Key,
                renamed.Nodes[0].Key);

            Assert.AreEqual(
                first.Links[0].Key,
                renamed.Links[0].Key);

            Assert.AreEqual(
                "Шкаф ядра",
                renamed.Locations[0].Name);
        }

        [TestMethod]
        public void UnassignedNodeRemainsWithoutLocation()
        {
            var source =
                Snapshot();

            var result =
                new MapLocationOverlay().Apply(
                    source,
                    new Location[0],
                    new MapLocationAssignment[0]);

            Assert.IsNull(
                result.Nodes[0].LocationId);

            Assert.IsNull(
                result.Nodes[1].LocationId);

            Assert.AreEqual(
                1,
                result.Links.Count);
        }

        [TestMethod]
        public void UnknownLocationAssignmentIsRejected()
        {
            var source =
                Snapshot();

            AssertInvalidOperation(
                () =>
                    new MapLocationOverlay().Apply(
                        source,
                        new Location[0],
                        new[]
                        {
                            new MapLocationAssignment(
                                source.Nodes[0].Key,
                                Guid.NewGuid())
                        }));
        }

        private static MapSnapshot Snapshot()
        {
            var a =
                new MapNode(
                    "map-node-a",
                    "оммутатор A",
                    "192.0.2.10",
                    60,
                    60);

            var b =
                new MapNode(
                    "map-node-b",
                    "оммутатор B",
                    "192.0.2.20",
                    300,
                    60);

            var link =
                new MapLink(
                    "map-link-a-b",
                    a.Key,
                    b.Key,
                    "Gi1",
                    "Gi2",
                    MapConfidence.High,
                    MapFreshness.Fresh,
                    new MapEvidenceItem[0]);

            return new MapSnapshot(
                Now,
                new[] { a, b },
                new[] { link });
        }

        private static void AssertInvalidOperation(
            Action action)
        {
            try
            {
                action();

                Assert.Fail(
                    "InvalidOperationException was expected.");
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
