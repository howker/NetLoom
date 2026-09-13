using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Observations;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Lldp;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Lldp;
using NetLoom.Persistence.Sqlite.Observations;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class LldpObservationStoreTests
    {
        private string _databasePath;

        [TestInitialize]
        public void Initialize()
        {
            _databasePath = Path.Combine(
                Path.GetTempPath(),
                "NetLoom.Lldp." +
                Guid.NewGuid().ToString("N") +
                ".sqlite");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }

        [TestMethod]
        public void NormalizedLldpObservationRoundTrips()
        {
            var factory =
                new SqliteConnectionFactory(
                    _databasePath);

            new DatabaseInitializer(factory)
                .Initialize();

            var rawStore =
                new SqliteObservationStore(factory);

            var lldpStore =
                new SqliteLldpObservationStore(factory);

            var observation =
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Lldp,
                    "192.0.2.10",
                    DateTime.UtcNow);

            rawStore.SaveSnmp(
                new SnmpObservation(
                    observation,
                    new NetLoom.Application.Snmp.SnmpVariable[0]));

            var localPort =
                new LldpLocalPort(
                    17,
                    5,
                    "Gi1/0/17",
                    "Uplink");

            lldpStore.Save(
                new LldpObservation(
                    observation,
                    new[]
                    {
                        new LldpRemoteNeighbor(
                            100,
                            17,
                            1,
                            4,
                            "00:11:22:33:44:55",
                            5,
                            "Gi0/1",
                            "Access port",
                            "edge-switch",
                            "Edge switch",
                            "bridge",
                            "bridge",
                            localPort)
                    },
                    new LldpLocalSystem(
                        4,
                        "00:AA:BB:CC:DD:EE",
                        "core-switch")));

            var loaded =
                lldpStore.Get(
                    observation.Id);

            Assert.IsNotNull(loaded);
            Assert.IsNotNull(loaded.LocalSystem);

            Assert.AreEqual(
                "00:AA:BB:CC:DD:EE",
                loaded.LocalSystem.ChassisId);

            Assert.AreEqual(
                "core-switch",
                loaded.LocalSystem.SystemName);

            Assert.AreEqual(
                1,
                loaded.Neighbors.Count);

            var neighbor =
                loaded.Neighbors[0];

            Assert.AreEqual(
                17,
                neighbor.LocalPortNumber);

            Assert.AreEqual(
                "00:11:22:33:44:55",
                neighbor.ChassisId);

            Assert.AreEqual(
                "edge-switch",
                neighbor.SystemName);

            Assert.IsNotNull(
                neighbor.LocalPort);

            Assert.AreEqual(
                "Gi1/0/17",
                neighbor.LocalPort.PortId);
        }
    }
}
