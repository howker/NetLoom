using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Locations;
using NetLoom.Persistence.Sqlite.Topology;
using NetLoom.Topology.Map;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint16LiveMapProviderTests
    {
        private static readonly DateTime T1 =
            new DateTime(
                2026, 1, 1, 10, 0, 0,
                DateTimeKind.Utc);

        private static readonly DateTime T2 =
            T1.AddMinutes(5);

        [TestMethod]
        public void MaterializedSqliteGraphFeedsMapSnapshot()
        {
            var directory =
                Path.Combine(
                    Path.GetTempPath(),
                    "NetLoom.Tests",
                    Guid.NewGuid().ToString("N"));

            try
            {
                var factory =
                    new SqliteConnectionFactory(
                        Path.Combine(
                            directory,
                            "topology.db"));

                new DatabaseInitializer(
                    factory)
                    .Initialize();

                var repository =
                    new SqliteMaterializedTopologyRepository(
                        factory);

                var deviceA =
                    Device(Guid.NewGuid(), "switch-a");

                var deviceB =
                    Device(Guid.NewGuid(), "switch-b");

                repository.SaveDevice(deviceA);
                repository.SaveDevice(deviceB);

                var link =
                    repository.SavePhysicalLink(
                        new PhysicalLink(
                            Guid.NewGuid(),
                            deviceA.Id,
                            null,
                            deviceB.Id,
                            null,
                            PhysicalLinkStrength.Confirmed,
                            PhysicalLinkFreshness.Fresh,
                            null,
                            null,
                            "test",
                            T1,
                            T1,
                            T1,
                            "sprint16-test",
                            false,
                            false,
                            null));

                repository.ReplacePhysicalLinkEvidence(
                    link.Id,
                    new[]
                    {
                        new PhysicalLinkEvidence(
                            link.Id,
                            PhysicalLinkEvidenceKind.Lldp,
                            PhysicalLinkEvidenceStrength.Strong,
                            "192.0.2.10",
                            "lldp-local:index:1",
                            Guid.NewGuid(),
                            T1,
                            "LLDP adjacency")
                    });

                var provider =
                    new MaterializedMapSnapshotProvider(
                        repository,
                        new SqliteLocationRepository(
                            factory),
                        new MaterializedTopologyMapProjector(),
                        () => T2);

                var snapshot =
                    provider.GetSnapshot();

                Assert.AreEqual(T2, snapshot.GeneratedUtc);
                Assert.AreEqual(2, snapshot.Nodes.Count);
                Assert.AreEqual(1, snapshot.Links.Count);
                Assert.AreEqual(1, snapshot.Links[0].Evidence.Count);
                Assert.AreEqual(0, snapshot.Locations.Count);
            }
            finally
            {
                System.Data.SQLite.SQLiteConnection.ClearAllPools();

                if (Directory.Exists(directory))
                {
                    Directory.Delete(
                        directory,
                        true);
                }
            }
        }

        private static TopologyDevice Device(
            Guid id,
            string customName)
        {
            return new TopologyDevice(
                id,
                null,
                customName,
                DeviceCategory.Unknown,
                DeviceDiscoveryOrigin.Automatic,
                MonitoringCapability.Unknown,
                null,
                null,
                null,
                false,
                false,
                T1,
                T1,
                T1);
        }
    }
}
