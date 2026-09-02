using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Observations;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Arp;
using NetLoom.Persistence.Sqlite.Arp;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Observations;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class ArpObservationStoreTests
    {
        private string _databasePath;

        [TestInitialize]
        public void Initialize()
        {
            _databasePath = Path.Combine(
                Path.GetTempPath(),
                "NetLoom.Arp." +
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
        public void ArpObservationRoundTrips()
        {
            var factory =
                new SqliteConnectionFactory(
                    _databasePath);

            new DatabaseInitializer(factory)
                .Initialize();

            var rawStore =
                new SqliteObservationStore(factory);

            var arpStore =
                new SqliteArpObservationStore(factory);

            var observation =
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Arp,
                    "192.0.2.1",
                    DateTime.UtcNow);

            rawStore.SaveSnmp(
                new SnmpObservation(
                    observation,
                    new SnmpVariable[0]));

            arpStore.Save(
                new ArpObservation(
                    observation,
                    new[]
                    {
                        new ArpEntry(
                            10,
                            1,
                            "192.0.2.50",
                            "00:11:22:33:44:55",
                            3,
                            1,
                            ArpTableKind.IpNetToPhysical),

                        new ArpEntry(
                            17,
                            1,
                            "198.51.100.7",
                            "AA:BB:CC:DD:EE:FF",
                            3,
                            null,
                            ArpTableKind.IpNetToMedia)
                    }));

            var loaded =
                arpStore.Get(observation.Id);

            Assert.IsNotNull(loaded);

            Assert.AreEqual(
                observation.Id,
                loaded.Observation.Id);

            Assert.AreEqual(
                2,
                loaded.Entries.Count);

            Assert.AreEqual(
                "192.0.2.50",
                loaded.Entries[0].IpAddress);

            Assert.AreEqual(
                "00:11:22:33:44:55",
                loaded.Entries[0].PhysicalAddress);

            Assert.AreEqual(
                ArpTableKind.IpNetToPhysical,
                loaded.Entries[0].TableKind);

            Assert.AreEqual(
                "198.51.100.7",
                loaded.Entries[1].IpAddress);

            Assert.AreEqual(
                ArpTableKind.IpNetToMedia,
                loaded.Entries[1].TableKind);
        }
    }
}
