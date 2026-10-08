using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Application.DiscoveryInbox;
using NetLoom.Application.Locations;
using NetLoom.Application.Topology;
using NetLoom.Domain.Access;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Discovery;
using NetLoom.Persistence.Sqlite.Locations;
using NetLoom.Persistence.Sqlite.Topology;
using NetLoom.Topology.Map;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint48DiscoveryInboxActionsTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 7, 9, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void AcceptConfirmsDeviceAndMissingOnlyChangesResolution()
        {
            WithDatabase(factory =>
            {
                var topology = new SqliteMaterializedTopologyRepository(factory);
                var runs = new SqliteDiscoveryRunRepository(factory);
                var actions = Actions(factory);
                var added = Device("10.0.0.1");
                var missing = Device("10.0.0.2");
                topology.SaveDevice(added);
                topology.SaveDevice(missing);
                var runId = Run(runs);
                runs.SaveResult(Row(runId, added.ManagementAddress, added.Id, DiscoveryResultGroup.New));
                runs.SaveResult(Row(runId, missing.ManagementAddress, missing.Id, DiscoveryResultGroup.Missing));
                runs.SaveResult(Row(runId, "10.0.0.3", null, DiscoveryResultGroup.Missing));
                var result = actions.Accept(runId, new[] { "10.0.0.1", "10.0.0.2", "10.0.0.3" }, T0.AddMinutes(1));
                Assert.AreEqual(3, result.Applied);
                Assert.AreEqual(0, result.Skipped);
                Assert.IsFalse(topology.GetDevice(added.Id).IsUnconfirmed);
                Assert.IsTrue(topology.GetDevice(missing.Id).IsUnconfirmed);
                AssertDevice(missing, topology.GetDevice(missing.Id));
                Assert.IsTrue(runs.GetResults(runId).All(row =>
                    row.Resolution == DiscoveryResultResolution.Accepted && row.ResolvedUtc == T0.AddMinutes(1)));
            });
        }

        [TestMethod]
        public void IgnoreExcludesNextRunAndUndoRestoresMapAndRemovesExcludedRow()
        {
            WithDatabase(factory =>
            {
                var topology = new SqliteMaterializedTopologyRepository(factory);
                var runs = new SqliteDiscoveryRunRepository(factory);
                var device = Device("10.0.0.1");
                topology.SaveDevice(device);
                var runId = Run(runs);
                runs.SaveResult(Row(runId, device.ManagementAddress, device.Id, DiscoveryResultGroup.New));
                var ignoredUtc = T0.AddMinutes(1);
                Assert.AreEqual(1, Actions(factory).Ignore(runId, new[] { device.ManagementAddress }, ignoredUtc).Applied);
                Assert.AreEqual(ignoredUtc, topology.GetDevice(device.Id).IgnoredUtc);
                Assert.AreEqual(DiscoveryResultResolution.Ignored, runs.GetResults(runId).Single().Resolution);
                Assert.AreEqual(ignoredUtc, runs.GetResults(runId).Single().ResolvedUtc);
                Assert.AreEqual(0, MapNodeCount(topology));
                Assert.AreEqual(ignoredUtc, new SqliteMaterializedTopologyReadSetReader(factory)
                    .Read("CIST").Devices.Single().IgnoredUtc);

                var journal = Journal(factory, new EmptyDiscoveryExclusionSource());
                var start = journal.BeginRun(Request(), "Synthetic profile", T0.AddMinutes(2));
                CollectionAssert.AreEqual(new[] { device.ManagementAddress }, start.ExcludedAddresses.ToArray());
                var excluded = runs.GetResults(start.RunId).Single();
                Assert.AreEqual(DiscoveryResultGroup.Excluded, excluded.Group);
                Assert.AreEqual(DiscoveryResultReason.OperatorIgnored, excluded.Reason);
                Assert.AreEqual(ignoredUtc.ToString("o"), excluded.ReasonDetail);
                Assert.AreEqual(device.Id, excluded.DeviceId);
                Assert.AreEqual(1, Actions(factory).UndoIgnore(start.RunId, excluded.Address, T0.AddMinutes(3)).Applied);
                Assert.IsNull(new SqliteMaterializedTopologyRepository(factory).GetDevice(device.Id).IgnoredUtc);
                Assert.AreEqual(1, MapNodeCount(topology));
                Assert.AreEqual(0, runs.GetResults(start.RunId).Count);
            });
        }

        [TestMethod]
        public void UndoOriginalIgnoreResetsResolutionAndUpsertPreservesIgnore()
        {
            WithDatabase(factory =>
            {
                var topology = new SqliteMaterializedTopologyRepository(factory);
                var runs = new SqliteDiscoveryRunRepository(factory);
                var ignoredUtc = T0.AddMinutes(1);
                var device = Device("10.0.0.1", ignoredUtc);
                topology.SaveDevice(device);
                topology.SaveDevice(Device(device.ManagementAddress, null, device.Id));
                Assert.AreEqual(ignoredUtc, topology.GetDevice(device.Id).IgnoredUtc);
                var runId = Run(runs);
                runs.SaveResult(Row(runId, device.ManagementAddress, device.Id, DiscoveryResultGroup.Changed));
                runs.SetResolution(runId, device.ManagementAddress, DiscoveryResultResolution.Ignored, ignoredUtc);
                Actions(factory).UndoIgnore(runId, device.ManagementAddress, T0.AddMinutes(2));
                var row = runs.GetResults(runId).Single();
                Assert.AreEqual(DiscoveryResultResolution.Pending, row.Resolution);
                Assert.IsNull(row.ResolvedUtc);
                Assert.AreEqual(1, row.Changes.Count);
                Assert.IsNull(topology.GetDevice(device.Id).IgnoredUtc);
            });
        }

        [TestMethod]
        public void ProfileExclusionTakesPrecedenceOverOperatorIgnore()
        {
            WithDatabase(factory =>
            {
                var topology = new SqliteMaterializedTopologyRepository(factory);
                var device = Device("10.0.0.1", T0);
                topology.SaveDevice(device);
                var journal = Journal(factory, new FixedExclusions());
                var start = journal.BeginRun(Request(), "Synthetic profile", T0.AddMinutes(1));
                var row = journal.GetResults(start.RunId).Single();
                Assert.AreEqual(DiscoveryResultReason.ProfileExclusion, row.Reason);
                Assert.IsNull(row.DeviceId);
                CollectionAssert.AreEqual(new[] { device.ManagementAddress }, start.ExcludedAddresses.ToArray());
                Assert.AreEqual(0, Actions(factory).UndoIgnore(start.RunId, row.Address, T0.AddMinutes(2)).Applied);
                Assert.AreEqual(T0, topology.GetDevice(device.Id).IgnoredUtc);
            });
        }

        [TestMethod]
        public void MarkUnmanagedPreservesOtherFieldsAndConfirmsDevice()
        {
            WithDatabase(factory =>
            {
                var topology = new SqliteMaterializedTopologyRepository(factory);
                var runs = new SqliteDiscoveryRunRepository(factory);
                var device = Device("10.0.0.1", T0);
                topology.SaveDevice(device);
                var locations = new LocationTopologyService(new SqliteLocationRepository(factory), topology);
                var locationId = locations.CreateLocation(null, "Серверная", "ЙЁ");
                locations.AssignDevice(device.Id, locationId);
                device = topology.GetDevice(device.Id);
                var runId = Run(runs);
                runs.SaveResult(Row(runId, device.ManagementAddress, device.Id, DiscoveryResultGroup.New));
                var result = Actions(factory).MarkUnmanaged(runId, new[] { device.ManagementAddress }, T0.AddMinutes(1));
                Assert.AreEqual(1, result.Applied);
                var updated = topology.GetDevice(device.Id);
                Assert.AreEqual(MonitoringCapability.None, updated.MonitoringCapability);
                Assert.IsFalse(updated.IsUnconfirmed);
                AssertDevice(device, updated, confirmation: false, capability: MonitoringCapability.None);
                Assert.AreEqual(DiscoveryResultResolution.Unmanaged, runs.GetResults(runId).Single().Resolution);
                Assert.AreEqual(T0.AddMinutes(1), runs.GetResults(runId).Single().ResolvedUtc);
            });
        }

        [TestMethod]
        public void AssignPlacementSetsLocationAndConfirmsDevice()
        {
            WithDatabase(factory =>
            {
                var topology = new SqliteMaterializedTopologyRepository(factory);
                var runs = new SqliteDiscoveryRunRepository(factory);
                var device = Device("10.0.0.1");
                topology.SaveDevice(device);
                var locations = new LocationTopologyService(new SqliteLocationRepository(factory), topology);
                var locationId = locations.CreateLocation(null, "Серверная", null);
                var runId = Run(runs);
                runs.SaveResult(Row(runId, device.ManagementAddress, device.Id, DiscoveryResultGroup.New));
                Assert.AreEqual(1, Actions(factory).AssignPlacement(runId,
                    new[] { device.ManagementAddress }, T0.AddMinutes(1), locationId).Applied);
                Assert.AreEqual(locationId, topology.GetDevice(device.Id).LocationId);
                Assert.IsFalse(topology.GetDevice(device.Id).IsUnconfirmed);
                Assert.AreEqual(DiscoveryResultResolution.Placed, runs.GetResults(runId).Single().Resolution);
                Assert.AreEqual(T0.AddMinutes(1), runs.GetResults(runId).Single().ResolvedUtc);
            });
        }

        [TestMethod]
        public void InapplicableUnknownAndAlreadyResolvedRowsAreCountedAsSkipped()
        {
            WithDatabase(factory =>
            {
                var topology = new SqliteMaterializedTopologyRepository(factory);
                var runs = new SqliteDiscoveryRunRepository(factory);
                var device = Device("10.0.0.1");
                topology.SaveDevice(device);
                var runId = Run(runs);
                runs.SaveResult(Row(runId, "10.0.0.1", device.Id, DiscoveryResultGroup.New));
                runs.SaveResult(Row(runId, "10.0.0.2", null, DiscoveryResultGroup.Ambiguous));
                runs.SaveResult(Row(runId, "10.0.0.3", device.Id, DiscoveryResultGroup.Excluded));
                runs.SaveResult(Row(runId, "10.0.0.4", device.Id, DiscoveryResultGroup.Changed));
                runs.SetResolution(runId, "10.0.0.4", DiscoveryResultResolution.Accepted, T0);
                var result = Actions(factory).Accept(runId,
                    new[] { "10.0.0.1", "10.0.0.2", "10.0.0.3", "10.0.0.4", "10.0.0.5" }, T0.AddMinutes(1));
                Assert.AreEqual(1, result.Applied);
                Assert.AreEqual(4, result.Skipped);
                Assert.AreEqual(DiscoveryResultResolution.Pending,
                    runs.GetResults(runId).Single(row => row.Address == "10.0.0.2").Resolution);
                result = Actions(factory).Ignore(runId, new[] { "10.0.0.1" }, T0.AddMinutes(2));
                Assert.AreEqual(0, result.Applied);
                Assert.AreEqual(1, result.Skipped);
                Assert.IsNull(topology.GetDevice(device.Id).IgnoredUtc);
            });
        }

        [TestMethod]
        public void IgnoredDevicesAndTheirLinksAreFilteredLikeHiddenDevices()
        {
            var ignored = Device("10.0.0.1", T0);
            var visible = Device("10.0.0.2");
            var link = new PhysicalLink(Guid.NewGuid(), ignored.Id, null, visible.Id, null,
                PhysicalLinkStrength.Manual, PhysicalLinkFreshness.Fresh, null, null, null,
                T0, T0, T0, "test", false, false, null);
            var map = new MaterializedTopologyMapProjector().Project(new[] { ignored, visible },
                new DeviceInterface[0], new[] { link }, new NetLoom.Domain.Locations.Location[0], T0);
            Assert.AreEqual(1, map.Nodes.Count);
            Assert.AreEqual(visible.Id, map.Nodes.Single().DeviceId);
            Assert.AreEqual(0, map.Links.Count);
        }

        private static int MapNodeCount(SqliteMaterializedTopologyRepository topology) =>
            new MaterializedTopologyMapProjector().Project(topology.GetDevices(), topology.GetInterfaces(),
                topology.GetPhysicalLinks(), new NetLoom.Domain.Locations.Location[0], T0).Nodes.Count;

        private static DiscoveryInboxActions Actions(SqliteConnectionFactory factory)
        {
            var topology = new SqliteMaterializedTopologyRepository(factory);
            return new DiscoveryInboxActions(new SqliteDiscoveryRunRepository(factory), topology, topology, topology,
                new LocationTopologyService(new SqliteLocationRepository(factory), topology));
        }

        private static DiscoveryRunJournal Journal(SqliteConnectionFactory factory, IDiscoveryExclusionSource exclusions)
        {
            var topology = new SqliteMaterializedTopologyRepository(factory);
            return new DiscoveryRunJournal(new SqliteDiscoveryRunRepository(factory),
                new MaterializedTopologyDiscoveryReader(topology), new DiscoveryCandidateTopologyMaterializer(topology), exclusions);
        }

        private static DiscoveryControlRequest Request() =>
            new DiscoveryControlRequest("10.0.0.1", "10.0.0.2", "255.255.255.0", Guid.NewGuid(), SnmpVersion.V2C);

        private static Guid Run(IDiscoveryRunRepository runs)
        {
            var id = Guid.NewGuid();
            runs.SaveRun(new DiscoveryRunRecord(id, T0, T0.AddMinutes(1), DiscoveryControlState.Completed,
                null, null, "10.0.0.1 – 10.0.0.10", 10, 10, 1, 1, 0, 0, null));
            return id;
        }

        private static TopologyDevice Device(string address, DateTime? ignoredUtc = null, Guid? id = null) =>
            new TopologyDevice(id ?? Guid.NewGuid(), null, "Операторское имя ЙЁ", DeviceCategory.Switch,
                DeviceDiscoveryOrigin.Automatic, MonitoringCapability.Unknown, "Vendor", "Model", "Notes",
                false, false, T0, T0, T0, "switch-1", "chassis", address, "Description", "1.3.6.1.4.1.99999", true, ignoredUtc);

        private static DiscoveryRunResult Row(Guid runId, string address, Guid? deviceId, DiscoveryResultGroup group) =>
            new DiscoveryRunResult(runId, address, group, deviceId, T0, true, new[] { 22 }, true, null,
                "switch-1", "Description", "1.3.6.1.4.1.99999", 8, DiscoveryResultCompleteness.Ready,
                DiscoveryPartialReason.None, DiscoveryResultReason.None, null,
                new[] { new DiscoveryFieldChange("sysName", "old", "new") }, DiscoveryResultResolution.Pending, null);

        private static void AssertDevice(TopologyDevice expected, TopologyDevice actual,
            bool? confirmation = null, MonitoringCapability? capability = null)
        {
            Assert.AreEqual(expected.Id, actual.Id);
            Assert.AreEqual(expected.LocationId, actual.LocationId);
            Assert.AreEqual(expected.CustomName, actual.CustomName);
            Assert.AreEqual(expected.Category, actual.Category);
            Assert.AreEqual(expected.DiscoveryOrigin, actual.DiscoveryOrigin);
            Assert.AreEqual(capability ?? expected.MonitoringCapability, actual.MonitoringCapability);
            Assert.AreEqual(expected.VendorOverride, actual.VendorOverride);
            Assert.AreEqual(expected.ModelOverride, actual.ModelOverride);
            Assert.AreEqual(expected.Notes, actual.Notes);
            Assert.AreEqual(expected.IsHidden, actual.IsHidden);
            Assert.AreEqual(expected.IsArchived, actual.IsArchived);
            Assert.AreEqual(expected.FirstSeenUtc, actual.FirstSeenUtc);
            Assert.AreEqual(expected.LastSeenUtc, actual.LastSeenUtc);
            Assert.AreEqual(expected.LastResolvedUtc, actual.LastResolvedUtc);
            Assert.AreEqual(expected.DiscoveredName, actual.DiscoveredName);
            Assert.AreEqual(expected.LldpChassisId, actual.LldpChassisId);
            Assert.AreEqual(expected.ManagementAddress, actual.ManagementAddress);
            Assert.AreEqual(expected.SystemDescription, actual.SystemDescription);
            Assert.AreEqual(expected.SystemObjectId, actual.SystemObjectId);
            Assert.AreEqual(confirmation ?? expected.IsUnconfirmed, actual.IsUnconfirmed);
            Assert.AreEqual(expected.IgnoredUtc, actual.IgnoredUtc);
        }

        private sealed class FixedExclusions : IDiscoveryExclusionSource
        {
            public System.Collections.Generic.IReadOnlyList<DiscoveryExclusionRule> GetRules(Guid accessProfileId) =>
                new[] { new DiscoveryExclusionRule("IpAddress", "10.0.0.1") };
        }

        private static void WithDatabase(Action<SqliteConnectionFactory> action)
        {
            var path = Path.Combine(Path.GetTempPath(), "netloom-s48-inbox-actions-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var factory = new SqliteConnectionFactory(path);
                new DatabaseInitializer(factory).Initialize();
                action(factory);
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                foreach (var file in new[] { path, path + "-wal", path + "-shm" })
                    if (File.Exists(file)) File.Delete(file);
            }
        }
    }
}
