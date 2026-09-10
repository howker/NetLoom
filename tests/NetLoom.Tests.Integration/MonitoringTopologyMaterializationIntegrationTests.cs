using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Locations;
using NetLoom.Persistence.Sqlite.Topology;
using NetLoom.Topology.Map;
using NetLoom.Topology.Materialization;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class
        MonitoringTopologyMaterializationIntegrationTests
    {
        private static readonly DateTime T1 =
            new DateTime(
                2026,
                9,
                10,
                8,
                0,
                0,
                DateTimeKind.Utc);

        private static readonly DateTime T2 =
            T1.AddMinutes(5);

        [TestMethod]
        public void
            ExplicitPolledDeviceAndIfIndexFeedMaterializedMapWithoutDuplicates()
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
                            "monitoring-topology.db"));

                new DatabaseInitializer(
                    factory)
                    .Initialize();

                var repository =
                    new SqliteMaterializedTopologyRepository(
                        factory);

                var materializer =
                    new MonitoringTopologyMaterializer(
                        repository);

                var deviceId =
                    new Guid(
                        "11111111-1111-1111-1111-111111111111");

                materializer.MaterializeDevice(
                    deviceId,
                    T1);

                materializer.MaterializeInterface(
                    deviceId,
                    17,
                    T1);

                materializer.MaterializeDevice(
                    deviceId,
                    T2);

                materializer.MaterializeInterface(
                    deviceId,
                    17,
                    T2);

                Assert.AreEqual(
                    1,
                    repository.GetDevices().Count);

                Assert.AreEqual(
                    1,
                    repository.GetInterfaces().Count);

                Assert.AreEqual(
                    0,
                    repository.GetPhysicalLinks().Count);

                var networkInterface =
                    repository
                        .GetInterfaces()
                        .Single();

                Assert.AreEqual(
                    deviceId,
                    networkInterface.DeviceId);

                Assert.AreEqual(
                    17,
                    networkInterface.IfIndex);

                Assert.AreEqual(
                    T1,
                    networkInterface.FirstSeenUtc);

                Assert.AreEqual(
                    T2,
                    networkInterface.LastSeenUtc);

                var provider =
                    new MaterializedMapSnapshotProvider(
                        repository,
                        new SqliteLocationRepository(
                            factory),
                        new MaterializedTopologyMapProjector(),
                        () => T2);

                var snapshot =
                    provider.GetSnapshot();

                Assert.AreEqual(
                    1,
                    snapshot.Nodes.Count);

                Assert.AreEqual(
                    deviceId,
                    snapshot.Nodes[0].DeviceId);

                Assert.AreEqual(
                    0,
                    snapshot.Links.Count);
            }
            finally
            {
                System.Data.SQLite.SQLiteConnection
                    .ClearAllPools();

                if (Directory.Exists(directory))
                {
                    Directory.Delete(
                        directory,
                        true);
                }
            }
        }
    }
}
