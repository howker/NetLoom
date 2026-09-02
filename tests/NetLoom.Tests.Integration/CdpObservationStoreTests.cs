using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Observations;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Cdp;
using NetLoom.Persistence.Sqlite.Cdp;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Observations;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class CdpObservationStoreTests
    {
        private string _databasePath;

        [TestInitialize]
        public void Initialize()
        {
            _databasePath = Path.Combine(
                Path.GetTempPath(),
                "NetLoom.Cdp." +
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
        public void NormalizedCdpObservationRoundTrips()
        {
            var factory =
                new SqliteConnectionFactory(
                    _databasePath);

            new DatabaseInitializer(factory)
                .Initialize();

            var rawStore =
                new SqliteObservationStore(factory);

            var cdpStore =
                new SqliteCdpObservationStore(factory);

            var observation =
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Cdp,
                    "192.0.2.10",
                    DateTime.UtcNow);

            rawStore.SaveSnmp(
                new SnmpObservation(
                    observation,
                    new NetLoom.Application.Snmp.SnmpVariable[0]));

            cdpStore.Save(
                new CdpObservation(
                    observation,
                    new[]
                    {
                        new CdpRemoteNeighbor(
                            17,
                            1,
                            1,
                            "192.0.2.50",
                            "IOS 15",
                            "edge-switch",
                            "GigabitEthernet0/1",
                            "Cisco",
                            "switch",
                            100,
                            1,
                            "edge-01",
                            "1.3.6.1.4.1.9.1.516",
                            1,
                            "192.0.2.50",
                            "Rack A",
                            1234)
                    }));

            var loaded =
                cdpStore.Get(observation.Id);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(1, loaded.Neighbors.Count);

            var neighbor =
                loaded.Neighbors[0];

            Assert.AreEqual(17, neighbor.CacheIfIndex);
            Assert.AreEqual(1, neighbor.DeviceIndex);
            Assert.AreEqual(
                "edge-switch",
                neighbor.DeviceId);
            Assert.AreEqual(
                "GigabitEthernet0/1",
                neighbor.DevicePort);
            Assert.AreEqual(
                "1.3.6.1.4.1.9.1.516",
                neighbor.SystemObjectId);
            Assert.AreEqual(
                "192.0.2.50",
                neighbor.PrimaryManagementAddress);
            Assert.AreEqual(
                1234L,
                neighbor.LastChange);
        }
    }
}
