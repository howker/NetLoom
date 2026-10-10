using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Export;
using NetLoom.Application.MapLayout;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.TopologyMap;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint49ParallelLinkExportTests
    {
        [TestMethod]
        [DataRow(400.0)]
        [DataRow(20000.0)]
        public void ParallelLinksHaveDifferentSegmentsAtDiagramScale(double targetX)
        {
            var diagram = new TopologyExportDiagramBuilder().Build(Snapshot(targetX,
                Link("z", "b", "a"), Link("a", "a", "b")));
            Assert.AreEqual(2, diagram.Links.Count);
            var first = diagram.Links.Single(link => link.Source.Key == "a");
            var second = diagram.Links.Single(link => link.Source.Key == "z");
            Assert.AreEqual(first.SourceY, first.TargetY, 0.001);
            Assert.AreEqual(second.SourceY, second.TargetY, 0.001);
            Assert.AreEqual(first.SourceX, second.TargetX, 0.001);
            Assert.AreEqual(first.TargetX, second.SourceX, 0.001);
            Assert.AreEqual(TopologyExportDiagramPolicy.ParallelLinkSpacing * diagram.Scale,
                second.SourceY - first.SourceY, 0.001);
            var centerY = diagram.Nodes[0].Y + diagram.Nodes[0].Height / 2.0;
            Assert.AreEqual(centerY, (first.SourceY + second.SourceY) / 2.0, 0.001);
            if (targetX > TopologyExportDiagramPolicy.MaxPixelDimension)
                Assert.IsTrue(diagram.Scale < 1.0);
        }

        [TestMethod]
        public void SingleLinkStillJoinsNodeCenters()
        {
            var diagram = new TopologyExportDiagramBuilder().Build(Snapshot(400, Link("a", "a", "b")));
            var first = diagram.Nodes.Single(node => node.Source.Key == "a");
            var second = diagram.Nodes.Single(node => node.Source.Key == "b");
            var link = diagram.Links.Single();
            Assert.AreEqual(first.X + first.Width / 2.0, link.SourceX, 0.001);
            Assert.AreEqual(first.Y + first.Height / 2.0, link.SourceY, 0.001);
            Assert.AreEqual(second.X + second.Width / 2.0, link.TargetX, 0.001);
            Assert.AreEqual(second.Y + second.Height / 2.0, link.TargetY, 0.001);
        }

        private static TopologyExportSnapshot Snapshot(double targetX, params MapLink[] links)
        {
            var now = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
            return new TopologyExportSnapshot(
                new TopologyRefreshSnapshot(
                    new MapSnapshot(now, new[]
                    {
                        new MapNode("a", "A", null, 0, 0),
                        new MapNode("b", "B", null, targetX, 0)
                    }, links),
                    new TopologyAlertSnapshot(now, "cist", new TopologyAlert[0])),
                new TopologyExportLayoutSnapshot(MapLayoutScope.PhysicalTopologyMapId,
                    new MapDeviceLayout[0], new MapLocationLayout[0]));
        }

        private static MapLink Link(string key, string source, string target)
        {
            return new MapLink(key, source, target, "Gi1/0/1", "Gi1/0/2",
                MapConfidence.High, MapFreshness.Fresh, new MapEvidenceItem[0]);
        }
    }
}
