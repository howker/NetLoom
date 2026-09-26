using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Lldp;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Topology;
using NetLoom.Topology.Materialization;

namespace NetLoom.Tests.Modern
{
    [TestClass]
    public sealed class Sprint45LldpLocalPortPreReconciliationTests
    {
        private static readonly DateTime T1 =
            new DateTime(
                2026, 9, 26, 17, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void UnresolvedRemoteStillReconcilesNumericLocalLldpPort()
        {
            var databasePath =
                Path.Combine(
                    Path.GetTempPath(),
                    "netloom-s45-lldp-local-" +
                    Guid.NewGuid().ToString("N") +
                    ".db");

            try
            {
                var connectionFactory =
                    new SqliteConnectionFactory(
                        databasePath);

                new DatabaseInitializer(
                    connectionFactory)
                    .Initialize();

                var repository =
                    new SqliteMaterializedTopologyRepository(
                        connectionFactory);

                var materializer =
                    new MonitoringTopologyMaterializer(
                        repository);

                var deviceId =
                    Guid.Parse(
                        "92929292-9292-9292-9292-929292929292");

                materializer.MaterializeDevice(
                    deviceId,
                    T1,
                    "192.0.2.92");

                materializer.MaterializeInterface(
                    deviceId,
                    7,
                    T1,
                    "Ethernet Port 7");

                var canonical =
                    repository
                        .GetInterfaces()
                        .Single(
                            item =>
                                item.DeviceId ==
                                    deviceId &&
                                item.IfIndex ==
                                    7);

                var syntheticId =
                    Guid.Parse(
                        "93939393-9393-9393-9393-939393939393");

                repository.SaveInterface(
                    new DeviceInterface(
                        syntheticId,
                        deviceId,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        false,
                        false,
                        T1,
                        T1,
                        "7",
                        "LLDP synthetic port"));

                var observation =
                    new LldpObservation(
                        new Observation(
                            Guid.Parse(
                                "94949494-9494-9494-9494-949494949494"),
                            ObservationKind.Lldp,
                            "192.0.2.92",
                            T1.AddSeconds(5)),
                        new[]
                        {
                            new LldpRemoteNeighbor(
                                1,
                                7,
                                1,
                                4,
                                "remote-chassis-not-materialized",
                                7,
                                "2",
                                "Remote port",
                                "Unknown remote",
                                null,
                                null,
                                null,
                                new LldpLocalPort(
                                    7,
                                    7,
                                    "7",
                                    "Ethernet Port 7"))
                        });

                materializer.MaterializeLldp(
                    deviceId,
                    observation);

                var localInterfaces =
                    repository
                        .GetInterfaces()
                        .Where(
                            item =>
                                item.DeviceId ==
                                    deviceId)
                        .ToArray();

                Assert.AreEqual(
                    1,
                    localInterfaces.Length);

                Assert.AreEqual(
                    canonical.Id,
                    localInterfaces[0].Id);

                Assert.AreEqual(
                    7,
                    localInterfaces[0].IfIndex);

                Assert.AreEqual(
                    "7",
                    localInterfaces[0].LldpPortId);

                Assert.IsFalse(
                    localInterfaces.Any(
                        item =>
                            item.Id ==
                            syntheticId));

                Assert.AreEqual(
                    0,
                    repository
                        .GetPhysicalLinks()
                        .Count);
            }
            finally
            {
                foreach (var path in new[]
                {
                    databasePath,
                    databasePath + "-wal",
                    databasePath + "-shm"
                })
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
            }
        }
    }
}
