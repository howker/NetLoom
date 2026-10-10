using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Application.Topology;
using NetLoom.Application.Monitoring.Interfaces;
using NetLoom.Application.Observations.Stp;
using NetLoom.Domain.Locations;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Topology;
using NetLoom.Topology.Diagnostics;
using NetLoom.Topology.Map;
using NetLoom.Topology.Safety;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint50DeviceFailureImpactTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
        private static Guid Id(int value) => new Guid("00000000-0000-0000-0000-" + value.ToString("D12"));

        private static PhysicalLink Link(int id, int a, int b, int port = 0)
        {
            return new PhysicalLink(Id(id), Id(a), port == 0 ? (Guid?)null : Id(port),
                Id(b), port == 0 ? (Guid?)null : Id(port + 1), PhysicalLinkStrength.Confirmed,
                PhysicalLinkFreshness.Fresh, null, null, "test", Now, Now, Now,
                "sprint50", false, false, null);
        }

        [TestMethod]
        public void ChainAndStarExposeSortedPartsAndPairCounts()
        {
            var analyzer = new PhysicalGraphSafetyAnalyzer();
            var chain = analyzer.AnalyzeDeviceFailures(new[] { Link(101, 1, 2), Link(102, 2, 3) });
            CollectionAssert.AreEqual(new[] { Id(1), Id(2), Id(3) }, chain.Select(x => x.DeviceId).ToArray());
            var middle = chain.Single(x => x.DeviceId == Id(2));
            Assert.IsTrue(middle.IsArticulationPoint);
            Assert.AreEqual(1L, middle.SeparatedDevicePairCount);
            CollectionAssert.AreEqual(new[] { Id(1), Id(3) },
                middle.PartDeviceIds.SelectMany(x => x).ToArray());
            Assert.IsFalse(chain.Single(x => x.DeviceId == Id(1)).IsArticulationPoint);

            var star = analyzer.AnalyzeDeviceFailures(new[]
            {
                Link(103, 2, 1), Link(104, 2, 3), Link(105, 2, 4)
            }).Single(x => x.DeviceId == Id(2));
            Assert.AreEqual(3, star.PartDeviceIds.Count);
            Assert.AreEqual(3L, star.SeparatedDevicePairCount);
        }

        [TestMethod]
        public void RingAndParallelEdgeDoNotCreateArticulationPoint()
        {
            var analyzer = new PhysicalGraphSafetyAnalyzer();
            Assert.IsFalse(analyzer.AnalyzeDeviceFailures(new[]
            {
                Link(101, 1, 2), Link(102, 2, 3), Link(103, 3, 4), Link(104, 4, 1)
            }).Any(x => x.IsArticulationPoint));
            Assert.IsFalse(analyzer.AnalyzeDeviceFailures(new[]
            {
                Link(105, 1, 2, 201), Link(106, 1, 2, 203)
            }).Any(x => x.IsArticulationPoint));
        }

        [TestMethod]
        public void PartsSortBySizeThenSmallestIdAndDiagnosticAcceptsCounts()
        {
            var impact = new PhysicalGraphSafetyAnalyzer().AnalyzeDeviceFailures(new[]
            {
                Link(101, 2, 1), Link(102, 2, 3), Link(103, 3, 4),
                Link(104, 2, 5), Link(105, 5, 6), Link(106, 5, 7)
            }).Single(x => x.DeviceId == Id(2));
            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, impact.PartDeviceIds.Select(x => x.Count).ToArray());
            CollectionAssert.AreEqual(new[] { Id(5), Id(6), Id(7) }, impact.PartDeviceIds[0].ToArray());
            Assert.AreEqual(11L, impact.SeparatedDevicePairCount);

            var diagnostic = new DeviceDiagnostic(Id(2), null, null, null, null, null,
                new InterfaceDiagnostic[0], null, null, null, impact.IsArticulationPoint,
                impact.PartDeviceIds.Select(x => x.Count).ToArray());
            Assert.IsTrue(diagnostic.IsArticulationPoint);
            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, diagnostic.FailurePartDeviceCounts.ToArray());
        }

        [TestMethod]
        public void ProjectorCarriesArticulationCountsIntoDeviceDiagnostics()
        {
            var devices = Enumerable.Range(1, 3).Select(number =>
                new TopologyDevice(Id(number), null, "Device " + number,
                    DeviceCategory.Unknown, DeviceDiscoveryOrigin.Automatic,
                    MonitoringCapability.Unknown, null, null, null, false, false,
                    Now.AddHours(-1), Now, Now, "Device " + number, null)).ToArray();
            var readSet = new MaterializedTopologyReadSet(devices,
                new DeviceInterface[0], new[] { Link(101, 1, 2), Link(102, 2, 3) },
                new PhysicalLinkEvidence[0], new Location[0],
                new BoundStpObservation[0], new InterfaceDegradationState[0]);
            var map = new MaterializedTopologyMapProjector().Project(
                readSet.Devices, readSet.Interfaces, readSet.PhysicalLinks,
                readSet.PhysicalLinkEvidence, readSet.Locations, Now);
            var snapshot = new MaterializedTopologyDiagnosticSnapshotProjector()
                .Project(readSet, map, "cist");
            var middle = snapshot.Devices.Single(device => device.DeviceId == Id(2));
            Assert.IsTrue(middle.IsArticulationPoint);
            CollectionAssert.AreEqual(new[] { 1, 1 }, middle.FailurePartDeviceCounts.ToArray());
            Assert.IsFalse(snapshot.Devices.Single(device => device.DeviceId == Id(1)).IsArticulationPoint);
        }
    }
}
