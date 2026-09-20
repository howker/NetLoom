using System;
using System.IO;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Application.Topology;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Topology;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint42DiscoveryCandidateMaterializerTests
    {
        [TestMethod]
        public void NewCandidateCreatesAutomaticDeviceAndRepeatReusesStoredDeviceId()
        {
            WithRepository(
                repository =>
                {
                    var materializer =
                        new DiscoveryCandidateTopologyMaterializer(
                            repository);

                    var firstObserved =
                        new DateTime(
                            2026,
                            9,
                            20,
                            10,
                            0,
                            0,
                            DateTimeKind.Utc);

                    var firstId =
                        materializer.Materialize(
                            Candidate(
                                "192.0.2.44",
                                "Switch A",
                                true),
                            firstObserved);

                    Assert.AreNotEqual(
                        Guid.Empty,
                        firstId);

                    var secondObserved =
                        firstObserved.AddMinutes(5);

                    var secondId =
                        materializer.Materialize(
                            Candidate(
                                "192.0.2.44",
                                "Switch A renamed",
                                true),
                            secondObserved);

                    Assert.AreEqual(
                        firstId,
                        secondId,
                        "A repeated scan of the same stored management address must reuse the persisted DeviceId instead of treating the IP address as DeviceId.");

                    var devices =
                        repository.GetDevices();

                    Assert.AreEqual(
                        1,
                        devices.Count);

                    var device =
                        repository.GetDevice(
                            firstId);

                    Assert.IsNotNull(device);
                    Assert.AreEqual(
                        DeviceDiscoveryOrigin.Automatic,
                        device.DiscoveryOrigin);
                    Assert.AreEqual(
                        "192.0.2.44",
                        device.ManagementAddress);
                    Assert.AreEqual(
                        "Switch A renamed",
                        device.DiscoveredName);
                    Assert.AreEqual(
                        secondObserved,
                        device.LastSeenUtc);
                    Assert.AreEqual(
                        0,
                        repository.GetInterfaces().Count,
                        "Discovery candidate materialization must not invent interfaces from a count-only candidate snapshot.");
                    Assert.AreEqual(
                        0,
                        repository.GetPhysicalLinks().Count,
                        "Discovery must not create PhysicalLink directly.");
                });
        }

        [TestMethod]
        public void ManualDeviceAtDiscoveredAddressIsPreservedWithoutAutomaticOverwrite()
        {
            WithRepository(
                repository =>
                {
                    var manualId =
                        Guid.NewGuid();

                    repository.SaveDevice(
                        new TopologyDevice(
                            manualId,
                            null,
                            "Manual cabinet switch",
                            DeviceCategory.UnmanagedSwitch,
                            DeviceDiscoveryOrigin.Manual,
                            MonitoringCapability.None,
                            null,
                            null,
                            "Keep manual metadata",
                            false,
                            false,
                            null,
                            null,
                            null,
                            null,
                            null,
                            "192.0.2.55"));

                    var materializer =
                        new DiscoveryCandidateTopologyMaterializer(
                            repository);

                    var returnedId =
                        materializer.Materialize(
                            Candidate(
                                "192.0.2.55",
                                "Automatic name",
                                true),
                            new DateTime(
                                2026,
                                9,
                                20,
                                10,
                                5,
                                0,
                                DateTimeKind.Utc));

                    Assert.AreEqual(
                        manualId,
                        returnedId);

                    var preserved =
                        repository.GetDevice(
                            manualId);

                    Assert.AreEqual(
                        DeviceDiscoveryOrigin.Manual,
                        preserved.DiscoveryOrigin);
                    Assert.AreEqual(
                        "Manual cabinet switch",
                        preserved.CustomName);
                    Assert.AreEqual(
                        "Keep manual metadata",
                        preserved.Notes);
                    Assert.IsNull(
                        preserved.DiscoveredName,
                        "Discovery must not overwrite a manual device with automatic identity data.");
                    Assert.AreEqual(
                        1,
                        repository.GetDevices().Count,
                        "Discovery must not duplicate a manual device already represented by that management address.");
                });
        }

        private static DiscoveryCandidateSnapshot Candidate(
            string address,
            string sysName,
            bool snmpResponded)
        {
            return new DiscoveryCandidateSnapshot(
                IPAddress.Parse(address),
                Guid.NewGuid(),
                true,
                snmpResponded,
                new[] { 22 },
                sysName,
                "Synthetic description",
                "1.3.6.1.4.1.99999",
                null,
                snmpResponded ? 8 : 0);
        }

        private static void WithRepository(
            Action<SqliteMaterializedTopologyRepository> action)
        {
            var path =
                Path.Combine(
                    Path.GetTempPath(),
                    "netloom-s42-discovery-materializer-" +
                    Guid.NewGuid().ToString("N") +
                    ".db");

            try
            {
                var connectionFactory =
                    new SqliteConnectionFactory(
                        path);

                new DatabaseInitializer(
                    connectionFactory)
                    .Initialize();

                action(
                    new SqliteMaterializedTopologyRepository(
                        connectionFactory));
            }
            finally
            {
                System.Data.SQLite.SQLiteConnection
                    .ClearAllPools();

                DeleteIfExists(path);
                DeleteIfExists(path + "-wal");
                DeleteIfExists(path + "-shm");
            }
        }

        private static void DeleteIfExists(
            string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
