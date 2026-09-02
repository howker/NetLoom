using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Observations;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Fdb;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Fdb;
using NetLoom.Persistence.Sqlite.Observations;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class FdbObservationStoreTests
    {
        private string _databasePath;

        [TestInitialize]
        public void Initialize()
        {
            _databasePath = Path.Combine(
                Path.GetTempPath(),
                "NetLoom.Fdb." +
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
        public void FdbObservationRoundTripsWithoutLosingMapping()
        {
            var factory =
                new SqliteConnectionFactory(
                    _databasePath);

            new DatabaseInitializer(factory)
                .Initialize();

            var rawStore =
                new SqliteObservationStore(factory);

            var fdbStore =
                new SqliteFdbObservationStore(factory);

            var observation =
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Fdb,
                    "192.0.2.80",
                    DateTime.UtcNow);

            rawStore.SaveSnmp(
                new SnmpObservation(
                    observation,
                    new SnmpVariable[0]));

            fdbStore.Save(
                new FdbObservation(
                    observation,
                    new[]
                    {
                        new BridgePortMapping(5, 101),
                        new BridgePortMapping(9, 301),
                        new BridgePortMapping(9, 302)
                    },
                    new[]
                    {
                        new FdbEntry(
                            "00:11:22:33:44:55",
                            5,
                            3),

                        new FdbEntry(
                            "AA:BB:CC:DD:EE:FF",
                            9,
                            3),

                        new FdbEntry(
                            "10:20:30:40:50:60",
                            0,
                            3)
                    }));

            var loaded =
                fdbStore.Get(observation.Id);

            Assert.IsNotNull(loaded);

            Assert.AreEqual(
                observation.Id,
                loaded.Observation.Id);

            Assert.AreEqual(
                3,
                loaded.BridgePortMappings.Count);

            Assert.AreEqual(
                3,
                loaded.Entries.Count);

            Assert.AreEqual(
                5,
                loaded.BridgePortMappings[0].BridgePortIndex);

            Assert.AreEqual(
                101,
                loaded.BridgePortMappings[0].IfIndex);

            Assert.AreEqual(
                9,
                loaded.BridgePortMappings[1].BridgePortIndex);

            Assert.AreEqual(
                301,
                loaded.BridgePortMappings[1].IfIndex);

            Assert.AreEqual(
                9,
                loaded.BridgePortMappings[2].BridgePortIndex);

            Assert.AreEqual(
                302,
                loaded.BridgePortMappings[2].IfIndex);

            var firstEntry =
                loaded.Entries
                    .Single(
                        entry =>
                            entry.MacAddress ==
                            "00:11:22:33:44:55");

            var ambiguousEntry =
                loaded.Entries
                    .Single(
                        entry =>
                            entry.MacAddress ==
                            "AA:BB:CC:DD:EE:FF");

            var unresolvedEntry =
                loaded.Entries
                    .Single(
                        entry =>
                            entry.MacAddress ==
                            "10:20:30:40:50:60");

            Assert.AreEqual(
                5,
                firstEntry.BridgePortIndex);

            Assert.AreEqual(
                9,
                ambiguousEntry.BridgePortIndex);

            Assert.AreEqual(
                0,
                unresolvedEntry.BridgePortIndex);
        }
    }
}
