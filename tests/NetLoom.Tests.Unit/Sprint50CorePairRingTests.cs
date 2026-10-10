using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.Rings;
using NetLoom.Contracts.StpTree;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Topology;
using NetLoom.Topology.Alerts;
using NetLoom.Topology.Diagnostics;
using NetLoom.Topology.Map;
using NetLoom.Topology.Rings;
using NetLoom.Topology.Safety;

namespace NetLoom.Tests.Unit
{
    // Sprint 50, Г5: кольцо, замкнутое через пару связанных ядер (составной регион).
    [TestClass]
    public sealed class Sprint50CorePairRingTests
    {
        private const string Instance = "cist";

        private static readonly DateTime Now =
            new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

        // 1. Простое кольцо анализируется как раньше.
        [TestMethod]
        public void SimpleRingGivesOneSimpleRingAnalysis()
        {
            var net = new Net();
            net.Connect("d1", "d2");
            net.Connect("d2", "d3");
            net.Connect("d3", "d4");
            net.Connect("d4", "d1").Block();

            var region = DetectSingle(net);
            Assert.AreEqual(PhysicalRedundancyRegionKind.SimpleRing, region.Kind);

            var result = Analyze(net, region);

            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(PhysicalRedundancyRegionKind.SimpleRing, result[0].RingKind);
            Assert.AreEqual(RingProtectionStatus.Protected, result[0].Status);
            Assert.AreEqual(region.RegionKey, result[0].RegionKey);
        }

        // 2. Как на полевом стенде: два кабеля между ядрами, цепочка из трёх коммутаторов.
        [TestMethod]
        public void RingThroughCorePairWithParallelCoreCablesIsProtected()
        {
            var net = CorePairNet(3, 2);
            net.Cables[1].Block();
            net.Chain[1].Block();

            var region = DetectSingle(net);
            Assert.AreEqual(PhysicalRedundancyRegionKind.Composite, region.Kind);

            var result = Analyze(net, region);

            Assert.AreEqual(1, result.Length);
            var ring = result[0];
            Assert.AreEqual(PhysicalRedundancyRegionKind.CorePairRing, ring.RingKind);
            Assert.AreEqual(RingProtectionStatus.Protected, ring.Status);
            Assert.IsTrue(ring.RegionKey.StartsWith("pring-corepair-v1-", StringComparison.Ordinal));
            CollectionAssert.AreEquivalent(
                new[] { net.Id("A"), net.Id("B") },
                ring.CoreDeviceIds.ToArray());
            Assert.AreEqual(5, ring.DeviceIds.Count);
            Assert.AreEqual(2, ring.BlockingPhysicalLinkIds.Count);
            Assert.AreNotEqual(region.RegionKey, ring.RegionKey);
        }

        // 3. Два кольца на одной паре ядер с одним кабелем между ними.
        [TestMethod]
        public void TwoRingsOnOneCorePairGiveTwoAnalyses()
        {
            var net = new Net();
            var r1 = net.Connect("A", "r1");
            var r2 = net.Connect("r1", "r2");
            var r3 = net.Connect("r2", "B");
            var s1 = net.Connect("A", "s1");
            var s2 = net.Connect("s1", "s2");
            var s3 = net.Connect("s2", "B");
            var cable = net.Connect("A", "B");
            r2.Block();
            s2.Block();

            var region = DetectSingle(net);
            Assert.AreEqual(PhysicalRedundancyRegionKind.Composite, region.Kind);

            var result = Analyze(net, region);

            Assert.AreEqual(2, result.Length);
            Assert.IsTrue(result.All(item => item.RingKind == PhysicalRedundancyRegionKind.CorePairRing));
            Assert.IsTrue(result.All(item => item.Status == RingProtectionStatus.Protected));
            Assert.AreEqual(2, result.Select(item => item.RegionKey).Distinct().Count());

            var firstLinks = LinkIds(result.Single(item => item.DeviceIds.Contains(net.Id("r1"))));
            var secondLinks = LinkIds(result.Single(item => item.DeviceIds.Contains(net.Id("s1"))));

            CollectionAssert.AreEquivalent(
                new[] { r1.Id, r2.Id, r3.Id, cable.Id },
                firstLinks);
            CollectionAssert.AreEquivalent(
                new[] { s1.Id, s2.Id, s3.Id, cable.Id },
                secondLinks);
        }

        // 4. Все участки пересылают: кольцо без защиты; без данных STP у участника — неопределённо.
        [TestMethod]
        public void AllForwardingRingIsUnprotectedUnlessStpDataIsMissing()
        {
            var net = CorePairNet(3, 2);
            net.Cables[1].Block();

            var region = DetectSingle(net);
            var result = Analyze(net, region);

            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(RingProtectionStatus.Unprotected, result[0].Status);
            Assert.IsTrue(result[0].IsComplete);

            // Инварианты оценщика предупреждений: Unprotected подтверждён анализом циклов пересылки.
            var snapshots = net.Snapshots("A");
            var forwarding = new PhysicalGraphSafetyAnalyzer()
                .AnalyzeForwardingCycles(net.PhysicalLinks, snapshots, Instance);
            var alerts = new TopologyAlertEvaluator().Evaluate(Now, forwarding, result);
            Assert.IsTrue(alerts.Alerts.Any(item => item.Kind == TopologyAlertKind.ForwardingCycle));

            var incomplete = new RingProtectionAnalyzer()
                .AnalyzeRegion(region, net.PhysicalLinks, net.Snapshots("A", "r2"), Instance);

            Assert.AreEqual(1, incomplete.Count);
            Assert.AreEqual(RingProtectionStatus.Unresolved, incomplete[0].Status);
        }

        // 5. Разрыв участка цепочки (Disabled на обоих концах) — кольцо деградировало.
        [TestMethod]
        public void BrokenChainLinkMakesRingDegraded()
        {
            var net = CorePairNet(3, 2);
            net.Cables[1].Block();
            net.Chain[1].Disable();

            var region = DetectSingle(net);
            var result = Analyze(net, region);

            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(RingProtectionStatus.Degraded, result[0].Status);
            CollectionAssert.AreEqual(
                new[] { net.Chain[1].Id },
                result[0].DisabledPhysicalLinkIds.ToArray());
        }

        // 6. Нет данных STP ни у кого: неопределённо, а не «без защиты».
        [TestMethod]
        public void RingWithoutAnyStpDataIsUnresolved()
        {
            var net = CorePairNet(3, 2);
            var region = DetectSingle(net);

            var result = new RingProtectionAnalyzer()
                .AnalyzeRegion(region, net.PhysicalLinks, new StpTreeSnapshot[0], Instance);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(PhysicalRedundancyRegionKind.CorePairRing, result[0].RingKind);
            Assert.AreEqual(RingProtectionStatus.Unresolved, result[0].Status);
        }

        // 7. Цепочки между якорями без прямой связи X–Y кольцом не считаются.
        [TestMethod]
        public void ChainsBetweenAnchorsWithoutDirectLinkGiveNoRing()
        {
            var net = new Net();
            net.Connect("X", "a1");
            net.Connect("a1", "a2");
            net.Connect("a2", "Y");
            net.Connect("X", "b1");
            net.Connect("b1", "b2");
            net.Connect("b2", "Y");
            net.Connect("X", "c1");
            net.Connect("c1", "Y");

            var region = DetectSingle(net);
            Assert.AreEqual(PhysicalRedundancyRegionKind.Composite, region.Kind);

            Assert.AreEqual(
                0,
                new PhysicalRedundancyRegionDetector()
                    .DetectCorePairRings(region, net.PhysicalLinks)
                    .Count);

            var result = Analyze(net, region);

            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(RingProtectionStatus.NotApplicable, result[0].Status);
            Assert.AreEqual(region.RegionKey, result[0].RegionKey);
        }

        // 8. Пучок параллельных связей — один участок: Forwarding, если хоть одна связь пересылает.
        [TestMethod]
        public void BundleSectionIsForwardingWhenAnyCableForwards()
        {
            var mixed = CorePairNet(3, 2);
            mixed.Cables[1].Block();
            mixed.Chain[1].Block();

            var protectedRing = Analyze(mixed, DetectSingle(mixed)).Single();
            Assert.AreEqual(RingProtectionStatus.Protected, protectedRing.Status);

            var mixedWithoutChainBlock = CorePairNet(3, 2);
            mixedWithoutChainBlock.Cables[1].Block();

            var unprotected = Analyze(
                mixedWithoutChainBlock,
                DetectSingle(mixedWithoutChainBlock)).Single();
            Assert.AreEqual(RingProtectionStatus.Unprotected, unprotected.Status);

            var bothBlocked = CorePairNet(3, 2);
            bothBlocked.Cables[0].Block();
            bothBlocked.Cables[1].Block();

            var blockedBundle = Analyze(bothBlocked, DetectSingle(bothBlocked)).Single();
            Assert.AreEqual(RingProtectionStatus.Protected, blockedBundle.Status);
        }

        // Для региона не Composite поиск колец пуст.
        [TestMethod]
        public void SimpleRegionHasNoCorePairRings()
        {
            var net = new Net();
            net.Connect("d1", "d2");
            net.Connect("d2", "d3");
            net.Connect("d3", "d1");

            var region = DetectSingle(net);

            Assert.AreEqual(
                0,
                new PhysicalRedundancyRegionDetector()
                    .DetectCorePairRings(region, net.PhysicalLinks)
                    .Count);
        }

        // 9. Диагностика кольца: корень STP, заблокированные порты, последнее изменение топологии.
        [TestMethod]
        public void RingDiagnosticReportsRootBlockedPortsAndLastChange()
        {
            var net = CorePairNet(3, 2);
            net.Cables[1].Block();
            net.Chain[1].Block();

            var changes = new Dictionary<string, DateTime>
            {
                { "A", Now.AddMinutes(-30) },
                { "r1", Now.AddHours(-2) },
                { "B", Now.AddMinutes(-5) }
            };

            var snapshots = net.Snapshots("A", null, changes);
            var stpByDevice = snapshots.ToDictionary(item => item.DeviceId);

            var rings = MaterializedTopologyDiagnosticSnapshotProjector.BuildRings(
                net.PhysicalLinks,
                stpByDevice,
                Instance);

            Assert.AreEqual(1, rings.Count);
            var ring = rings[0];

            Assert.AreEqual(PhysicalRedundancyRegionKind.CorePairRing, ring.Kind);
            Assert.AreEqual(RingProtectionStatus.Protected, ring.Status);
            Assert.AreEqual(net.Id("A"), ring.RootDeviceId);
            Assert.AreEqual("8000.00aa", ring.DesignatedRoot);
            Assert.AreEqual(0, ring.DevicesWithoutStpIds.Count);
            Assert.AreEqual(Now.AddMinutes(-5), ring.LastTopologyChangeUtc);
            CollectionAssert.AreEquivalent(
                new[] { net.Id("A"), net.Id("B") },
                ring.CoreDeviceIds.ToArray());
            Assert.AreEqual(6, ring.PhysicalLinkIds.Count);

            // Заблокированы порт кабеля ядер и порт на связи цепочки.
            CollectionAssert.AreEquivalent(
                new[] { net.Cables[1].Id, net.Chain[1].Id },
                ring.BlockedPorts.Select(item => item.PhysicalLinkId).ToArray());
            Assert.IsTrue(ring.BlockedPorts.All(
                item => item.InterfaceId.HasValue &&
                        item.DeviceId == net.Cables[1].DeviceA &&
                        item.InterfaceId.Value == net.Cables[1].InterfaceA ||
                        item.DeviceId == net.Chain[1].DeviceA &&
                        item.InterfaceId.Value == net.Chain[1].InterfaceA));
        }

        [TestMethod]
        public void RingDiagnosticListsDevicesWithoutStpAndNoAmbiguousRoot()
        {
            var net = CorePairNet(3, 2);
            net.Cables[1].Block();
            net.Chain[1].Block();

            var stpByDevice = net.Snapshots("A", "r3").ToDictionary(item => item.DeviceId);

            var ring = MaterializedTopologyDiagnosticSnapshotProjector.BuildRings(
                net.PhysicalLinks,
                stpByDevice,
                Instance).Single();

            CollectionAssert.AreEqual(
                new[] { net.Id("r3") },
                ring.DevicesWithoutStpIds.ToArray());
            Assert.AreEqual(RingProtectionStatus.Unresolved, ring.Status);
        }

        [TestMethod]
        public void RingDiagnosticTreatsEmptyStpObservationAsNoStpData()
        {
            var net = CorePairNet(3, 2);
            net.Cables[1].Block();
            net.Chain[1].Block();
            var stpByDevice = net.Snapshots("A").ToDictionary(item => item.DeviceId);
            var missingId = net.Id("r2");
            stpByDevice[missingId] = new StpTreeSnapshot(
                missingId, Guid.NewGuid(), Now, Instance, null, null,
                null, null, null, new StpTreePort[0]);

            var ring = MaterializedTopologyDiagnosticSnapshotProjector.BuildRings(
                net.PhysicalLinks, stpByDevice, Instance).Single();

            CollectionAssert.AreEqual(new[] { missingId }, ring.DevicesWithoutStpIds.ToArray());
            Assert.AreEqual(RingProtectionStatus.Unresolved, ring.Status);
            Assert.AreEqual(net.Id("A"), ring.RootDeviceId);
        }

        // 10. Предупреждение о разорванном кольце через пару ядер — с ключом региона кольца.
        [TestMethod]
        public void BrokenCorePairRingRaisesDegradedAlertWithRingKey()
        {
            var net = CorePairNet(3, 2);
            net.Cables[1].Block();
            net.Chain[1].Disable();

            var region = DetectSingle(net);
            var rings = Analyze(net, region);
            var forwarding = new PhysicalGraphSafetyAnalyzer()
                .AnalyzeForwardingCycles(net.PhysicalLinks, net.Snapshots("A"), Instance);

            var alerts = new TopologyAlertEvaluator().Evaluate(Now, forwarding, rings);

            var alert = alerts.Alerts.Single();
            Assert.AreEqual(TopologyAlertKind.RingProtectionDegraded, alert.Kind);
            CollectionAssert.AreEqual(
                new[] { rings[0].RegionKey },
                alert.RelatedRegionKeys.ToArray());
        }

        // Полевой стенд: «Кольцо ПС-1» замкнуто на два ядра и распознаётся как кольцо через пару ядер.
        [TestMethod]
        [TestCategory("FieldCheck")]
        public void FieldStandRingPs1IsCorePairRing()
        {
            var root = FindRepositoryRoot();

            var source = Path.Combine(root, "artifacts", "realistic-stand", "field-s46.db");

            if (!File.Exists(source))
            {
                Assert.Inconclusive("Field stand is not built: run TestCategory=StandBuilder first.");
            }

            var database = Path.Combine(
                Path.GetTempPath(),
                "netloom-s50-corepair-" + Guid.NewGuid().ToString("N") + ".db");

            File.Copy(source, database);

            try
            {
                var factory = new SqliteConnectionFactory(database);
                new DatabaseInitializer(factory).Initialize();

                var readSet = new SqliteMaterializedTopologyReadSetReader(factory).Read(Instance);

                var map = new MaterializedTopologyMapProjector().Project(
                    readSet.Devices,
                    readSet.Interfaces,
                    readSet.PhysicalLinks,
                    readSet.PhysicalLinkEvidence,
                    readSet.Locations,
                    DateTime.UtcNow);

                var diagnostics = new MaterializedTopologyDiagnosticSnapshotProjector()
                    .Project(readSet, map, Instance);

                var names = readSet.Devices.ToDictionary(
                    item => item.Id,
                    item => item.CustomName ?? item.DiscoveredName);

                var ifIndexById = readSet.Interfaces
                    .Where(item => item.IfIndex.HasValue)
                    .ToDictionary(item => item.Id, item => item.IfIndex.Value);

                var expectedNames = new[] { "core-sw-01", "core-sw-02" }
                    .Concat(Enumerable.Range(1, 8).Select(index => "ps1-sw-0" + index))
                    .ToArray();

                var ring = diagnostics.Rings.Single(
                    item => item.Kind == PhysicalRedundancyRegionKind.CorePairRing &&
                            item.DeviceIds.Count == 10 &&
                            expectedNames.All(
                                name => item.DeviceIds.Any(id => names[id] == name)));

                Assert.AreEqual(RingProtectionStatus.Protected, ring.Status);

                CollectionAssert.AreEquivalent(
                    new[] { "core-sw-01", "core-sw-02" },
                    ring.CoreDeviceIds.Select(id => names[id]).ToArray());

                Assert.IsTrue(
                    ring.BlockedPorts.Any(
                        item => names[item.DeviceId] == "ps1-sw-05" &&
                                item.InterfaceId.HasValue &&
                                ifIndexById[item.InterfaceId.Value] == 8),
                    "ps1-sw-05 port 8 must be blocked.");

                Assert.IsTrue(
                    ring.BlockedPorts.Any(
                        item => names[item.DeviceId] == "core-sw-02" &&
                                item.InterfaceId.HasValue &&
                                ifIndexById[item.InterfaceId.Value] == 26),
                    "core-sw-02 port 26 must be blocked.");

                Assert.IsTrue(ring.RootDeviceId.HasValue);
                Assert.AreEqual("core-sw-01", names[ring.RootDeviceId.Value]);
                Assert.IsTrue(ring.LastTopologyChangeUtc.HasValue);
            }
            finally
            {
                try
                {
                    System.Data.SQLite.SQLiteConnection.ClearAllPools();
                    File.Delete(database);
                }
                catch (IOException)
                {
                }
            }
        }

        // ---------- помощники ----------

        private static PhysicalRedundancyRegion DetectSingle(Net net)
        {
            return new PhysicalRedundancyRegionDetector()
                .Detect(net.PhysicalLinks)
                .Single();
        }

        private static RingProtectionAnalysis[] Analyze(
            Net net,
            PhysicalRedundancyRegion region)
        {
            return new RingProtectionAnalyzer()
                .AnalyzeRegion(region, net.PhysicalLinks, net.Snapshots("A"), Instance)
                .ToArray();
        }

        private static Guid[] LinkIds(RingProtectionAnalysis analysis)
        {
            return analysis.ForwardingPhysicalLinkIds
                .Concat(analysis.BlockingPhysicalLinkIds)
                .Concat(analysis.DisabledPhysicalLinkIds)
                .Concat(analysis.UnresolvedPhysicalLinkIds)
                .ToArray();
        }

        // Ядра A и B, цепочка из chainDevices коммутаторов A–r1…rN–B и cableCount кабелей A–B.
        private static Net CorePairNet(int chainDevices, int cableCount)
        {
            var net = new Net();
            var previous = "A";

            for (var index = 1; index <= chainDevices; index++)
            {
                var name = "r" + index;
                net.Chain.Add(net.Connect(previous, name));
                previous = name;
            }

            net.Chain.Add(net.Connect(previous, "B"));

            for (var index = 0; index < cableCount; index++)
            {
                net.Cables.Add(net.Connect("A", "B"));
            }

            return net;
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

            while (directory != null &&
                   !File.Exists(Path.Combine(directory.FullName, "NetLoom.sln")))
            {
                directory = directory.Parent;
            }

            if (directory == null)
            {
                throw new InvalidOperationException("NetLoom.sln was not found above the test output directory.");
            }

            return directory.FullName;
        }

        private sealed class NetLink
        {
            public Guid Id;
            public Guid DeviceA;
            public Guid DeviceB;
            public Guid InterfaceA;
            public Guid InterfaceB;
            public StpTreePortState StateA = StpTreePortState.Forwarding;
            public StpTreePortState StateB = StpTreePortState.Forwarding;
            public PhysicalLink Link;

            // Порт на стороне A в состоянии Blocking.
            public NetLink Block()
            {
                StateA = StpTreePortState.Blocking;
                return this;
            }

            public NetLink Disable()
            {
                StateA = StpTreePortState.Disabled;
                StateB = StpTreePortState.Disabled;
                return this;
            }
        }

        private sealed class Net
        {
            private readonly Dictionary<string, Guid> _ids = new Dictionary<string, Guid>();
            private readonly List<NetLink> _links = new List<NetLink>();

            public List<NetLink> Chain { get; } = new List<NetLink>();

            public List<NetLink> Cables { get; } = new List<NetLink>();

            public PhysicalLink[] PhysicalLinks
            {
                get { return _links.Select(item => item.Link).ToArray(); }
            }

            public Guid Id(string name)
            {
                Guid id;

                if (!_ids.TryGetValue(name, out id))
                {
                    id = Guid.NewGuid();
                    _ids.Add(name, id);
                }

                return id;
            }

            public NetLink Connect(string a, string b)
            {
                var link = new NetLink
                {
                    Id = Guid.NewGuid(),
                    DeviceA = Id(a),
                    DeviceB = Id(b),
                    InterfaceA = Guid.NewGuid(),
                    InterfaceB = Guid.NewGuid()
                };

                link.Link = new PhysicalLink(
                    link.Id,
                    link.DeviceA,
                    link.InterfaceA,
                    link.DeviceB,
                    link.InterfaceB,
                    PhysicalLinkStrength.Confirmed,
                    PhysicalLinkFreshness.Fresh,
                    null,
                    null,
                    "sprint50-test",
                    Now,
                    Now,
                    Now,
                    "sprint50",
                    false,
                    false,
                    null);

                _links.Add(link);

                return link;
            }

            // Снимки STP всех устройств, кроме перечисленных; root — имя корня STP.
            public StpTreeSnapshot[] Snapshots(
                string root,
                string without = null,
                IDictionary<string, DateTime> changes = null)
            {
                var result = new List<StpTreeSnapshot>();

                foreach (var pair in _ids)
                {
                    if (pair.Key == without)
                    {
                        continue;
                    }

                    var ports = new List<StpTreePort>();

                    foreach (var link in _links)
                    {
                        if (link.DeviceA == pair.Value)
                        {
                            ports.Add(Port(ports.Count + 1, link.InterfaceA, link.StateA));
                        }
                        else if (link.DeviceB == pair.Value)
                        {
                            ports.Add(Port(ports.Count + 1, link.InterfaceB, link.StateB));
                        }
                    }

                    var isRoot = pair.Key == root;
                    DateTime change;
                    DateTime? lastChange = null;

                    if (changes != null && changes.TryGetValue(pair.Key, out change))
                    {
                        lastChange = change;
                    }

                    result.Add(
                        new StpTreeSnapshot(
                            pair.Value,
                            Guid.NewGuid(),
                            Now,
                            Instance,
                            "8000.00aa",
                            isRoot ? 0L : 10L,
                            isRoot ? (int?)null : 1,
                            null,
                            null,
                            ports,
                            lastChange));
                }

                return result.ToArray();
            }

            private static StpTreePort Port(
                int bridgePortIndex,
                Guid interfaceId,
                StpTreePortState state)
            {
                return new StpTreePort(
                    bridgePortIndex,
                    100 + bridgePortIndex,
                    interfaceId,
                    "port-" + bridgePortIndex,
                    state,
                    false,
                    10);
            }
        }
    }
}
