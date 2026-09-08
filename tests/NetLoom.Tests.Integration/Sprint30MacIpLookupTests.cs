using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Lookup;
using NetLoom.Application.Observations;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Arp;
using NetLoom.Domain.Observations.Fdb;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Arp;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Fdb;
using NetLoom.Persistence.Sqlite.Lookup;
using NetLoom.Persistence.Sqlite.Observations;
using NetLoom.Persistence.Sqlite.Topology;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint30MacIpLookupTests
    {
        private static readonly DateTime T1 =
            new DateTime(
                2026, 9, 8, 10, 0, 0,
                DateTimeKind.Utc);

        private static readonly DateTime T2 =
            new DateTime(
                2026, 9, 8, 10, 1, 0,
                DateTimeKind.Utc);

        private string _databasePath;

        [TestInitialize]
        public void Initialize()
        {
            _databasePath =
                Path.Combine(
                    Path.GetTempPath(),
                    "NetLoom.Sprint30." +
                    Guid.NewGuid().ToString("N") +
                    ".sqlite");
        }

        [TestCleanup]
        public void Cleanup()
        {
            DeleteIfExists(_databasePath);
            DeleteIfExists(_databasePath + "-wal");
            DeleteIfExists(_databasePath + "-shm");
        }

        [TestMethod]
        public void
            BindingDoesNotRequireMaterializedDeviceAndCascadesWithObservation()
        {
            var factory =
                Factory();

            var observation =
                SaveRaw(
                    factory,
                    ObservationKind.Fdb,
                    "192.0.2.10",
                    T1);

            var deviceId =
                Guid.NewGuid();

            var bindings =
                new SqliteObservationDeviceBindingStore(
                    factory);

            bindings.Bind(
                observation.Id,
                deviceId);

            Assert.AreEqual(
                deviceId,
                bindings
                    .GetDeviceId(
                        observation.Id));

            using (var connection =
                factory.OpenConnection())
            using (var delete =
                connection.CreateCommand())
            {
                delete.CommandText = @"
DELETE FROM observations
WHERE observation_id = @id;";

                delete.Parameters.AddWithValue(
                    "@id",
                    observation.Id.ToString("D"));

                delete.ExecuteNonQuery();
            }

            Assert.IsNull(
                bindings.GetDeviceId(
                    observation.Id));
        }

        [TestMethod]
        public void
            MacLookupReturnsStableDeviceAndInterfaceWithoutClaimingPhysicalLink()
        {
            var factory =
                Factory();

            Guid deviceId;
            Guid interfaceId;

            CreateDeviceAndInterface(
                factory,
                101,
                out deviceId,
                out interfaceId);

            var fdb =
                SaveFdb(
                    factory,
                    "192.0.2.10",
                    T2,
                    "00:11:22:33:44:55",
                    new[]
                    {
                        new BridgePortMapping(
                            5,
                            101)
                    },
                    5);

            new SqliteObservationDeviceBindingStore(
                factory)
                .Bind(
                    fdb.Observation.Id,
                    deviceId);

            var result =
                new SqliteMacIpLookupReader(
                    factory)
                    .FindByMac(
                        "00-11-22-33-44-55",
                        50);

            Assert.AreEqual(
                "00:11:22:33:44:55",
                result.NormalizedQuery);

            Assert.AreEqual(
                1,
                result.Candidates.Count);

            var candidate =
                result.Candidates[0];

            Assert.AreEqual(
                MacIpLookupCandidateStatus
                    .ResolvedInterface,
                candidate.Status);

            Assert.AreEqual(
                deviceId,
                candidate.DeviceId);

            Assert.AreEqual(
                interfaceId,
                candidate.InterfaceId);

            Assert.AreEqual(
                101,
                candidate.IfIndex);

            Assert.AreEqual(
                5,
                candidate.BridgePortIndex);

            Assert.AreEqual(
                "192.0.2.10",
                candidate.FdbSourceAddress);
        }

        [TestMethod]
        public void
            AmbiguousBridgePortMappingRemainsExplicitAndDoesNotGuessInterface()
        {
            var factory =
                Factory();

            Guid deviceId;
            Guid firstInterfaceId;

            CreateDeviceAndInterface(
                factory,
                301,
                out deviceId,
                out firstInterfaceId);

            SaveInterface(
                factory,
                deviceId,
                Guid.NewGuid(),
                302);

            var fdb =
                SaveFdb(
                    factory,
                    "192.0.2.20",
                    T2,
                    "AA:BB:CC:DD:EE:FF",
                    new[]
                    {
                        new BridgePortMapping(
                            9,
                            301),
                        new BridgePortMapping(
                            9,
                            302)
                    },
                    9);

            new SqliteObservationDeviceBindingStore(
                factory)
                .Bind(
                    fdb.Observation.Id,
                    deviceId);

            var candidate =
                new SqliteMacIpLookupReader(
                    factory)
                    .FindByMac(
                        "aa:bb:cc:dd:ee:ff",
                        50)
                    .Candidates
                    .Single();

            Assert.AreEqual(
                MacIpLookupCandidateStatus
                    .BridgePortAmbiguous,
                candidate.Status);

            Assert.IsFalse(
                candidate.InterfaceId.HasValue);

            Assert.IsFalse(
                candidate.IfIndex.HasValue);
        }

        [TestMethod]
        public void
            UnboundFdbObservationDoesNotTurnSourceAddressIntoDeviceId()
        {
            var factory =
                Factory();

            SaveFdb(
                factory,
                "192.0.2.30",
                T2,
                "10:20:30:40:50:60",
                new[]
                {
                    new BridgePortMapping(
                        7,
                        700)
                },
                7);

            var candidate =
                new SqliteMacIpLookupReader(
                    factory)
                    .FindByMac(
                        "10:20:30:40:50:60",
                        50)
                    .Candidates
                    .Single();

            Assert.AreEqual(
                MacIpLookupCandidateStatus
                    .ObservationUnbound,
                candidate.Status);

            Assert.IsFalse(
                candidate.DeviceId.HasValue);

            Assert.AreEqual(
                "192.0.2.30",
                candidate.FdbSourceAddress);
        }

        [TestMethod]
        public void
            IpLookupUsesLatestUsableArpMacThenReturnsFdbEvidenceCandidate()
        {
            var factory =
                Factory();

            Guid deviceId;
            Guid interfaceId;

            CreateDeviceAndInterface(
                factory,
                55,
                out deviceId,
                out interfaceId);

            var fdb =
                SaveFdb(
                    factory,
                    "192.0.2.40",
                    T2,
                    "00:AA:BB:CC:DD:EE",
                    new[]
                    {
                        new BridgePortMapping(
                            4,
                            55)
                    },
                    4);

            new SqliteObservationDeviceBindingStore(
                factory)
                .Bind(
                    fdb.Observation.Id,
                    deviceId);

            var arp =
                SaveArp(
                    factory,
                    "192.0.2.1",
                    T1,
                    "198.51.100.25",
                    "00-AA-BB-CC-DD-EE");

            var candidate =
                new SqliteMacIpLookupReader(
                    factory)
                    .FindByIp(
                        "198.51.100.25",
                        50)
                    .Candidates
                    .Single();

            Assert.AreEqual(
                MacIpLookupCandidateStatus
                    .ResolvedInterface,
                candidate.Status);

            Assert.AreEqual(
                "198.51.100.25",
                candidate.IpAddress);

            Assert.AreEqual(
                "00:AA:BB:CC:DD:EE",
                candidate.MacAddress);

            Assert.AreEqual(
                arp.Observation.Id,
                candidate.ArpObservationId);

            Assert.AreEqual(
                fdb.Observation.Id,
                candidate.FdbObservationId);

            Assert.AreEqual(
                deviceId,
                candidate.DeviceId);

            Assert.AreEqual(
                interfaceId,
                candidate.InterfaceId);
        }

        [TestMethod]
        public void
            IpLookupKeepsArpOnlyEvidenceWhenMacHasNoFdbSighting()
        {
            var factory =
                Factory();

            var arp =
                SaveArp(
                    factory,
                    "192.0.2.1",
                    T1,
                    "203.0.113.50",
                    "DE:AD:BE:EF:00:01");

            var candidate =
                new SqliteMacIpLookupReader(
                    factory)
                    .FindByIp(
                        "203.0.113.50",
                        50)
                    .Candidates
                    .Single();

            Assert.AreEqual(
                MacIpLookupCandidateStatus
                    .FdbNotObserved,
                candidate.Status);

            Assert.AreEqual(
                arp.Observation.Id,
                candidate.ArpObservationId);

            Assert.IsFalse(
                candidate.FdbObservationId.HasValue);

            Assert.IsFalse(
                candidate.DeviceId.HasValue);

            Assert.IsFalse(
                candidate.InterfaceId.HasValue);
        }

        private SqliteConnectionFactory Factory()
        {
            var factory =
                new SqliteConnectionFactory(
                    _databasePath);

            new DatabaseInitializer(
                factory)
                .Initialize();

            return factory;
        }

        private static Observation SaveRaw(
            SqliteConnectionFactory factory,
            ObservationKind kind,
            string sourceAddress,
            DateTime capturedUtc)
        {
            var observation =
                new Observation(
                    Guid.NewGuid(),
                    kind,
                    sourceAddress,
                    capturedUtc);

            new SqliteObservationStore(factory)
                .SaveSnmp(
                    new SnmpObservation(
                        observation,
                        new SnmpVariable[0]));

            return observation;
        }

        private static FdbObservation SaveFdb(
            SqliteConnectionFactory factory,
            string sourceAddress,
            DateTime capturedUtc,
            string macAddress,
            BridgePortMapping[] mappings,
            int bridgePortIndex)
        {
            var observation =
                SaveRaw(
                    factory,
                    ObservationKind.Fdb,
                    sourceAddress,
                    capturedUtc);

            var fdb =
                new FdbObservation(
                    observation,
                    mappings,
                    new[]
                    {
                        new FdbEntry(
                            macAddress,
                            bridgePortIndex,
                            3)
                    });

            new SqliteFdbObservationStore(factory)
                .Save(fdb);

            return fdb;
        }

        private static ArpObservation SaveArp(
            SqliteConnectionFactory factory,
            string sourceAddress,
            DateTime capturedUtc,
            string ipAddress,
            string macAddress)
        {
            var observation =
                SaveRaw(
                    factory,
                    ObservationKind.Arp,
                    sourceAddress,
                    capturedUtc);

            var arp =
                new ArpObservation(
                    observation,
                    new[]
                    {
                        new ArpEntry(
                            10,
                            1,
                            ipAddress,
                            macAddress,
                            3,
                            1,
                            ArpTableKind
                                .IpNetToPhysical)
                    });

            new SqliteArpObservationStore(factory)
                .Save(arp);

            return arp;
        }

        private static void
            CreateDeviceAndInterface(
                SqliteConnectionFactory factory,
                int ifIndex,
                out Guid deviceId,
                out Guid interfaceId)
        {
            deviceId =
                Guid.NewGuid();

            interfaceId =
                Guid.NewGuid();

            var repository =
                new SqliteMaterializedTopologyRepository(
                    factory);

            repository.SaveDevice(
                new TopologyDevice(
                    deviceId,
                    null,
                    "switch",
                    (DeviceCategory)0,
                    (DeviceDiscoveryOrigin)0,
                    (MonitoringCapability)0,
                    null,
                    null,
                    null,
                    false,
                    false,
                    T1,
                    T1,
                    T1));

            repository.SaveInterface(
                Interface(
                    interfaceId,
                    deviceId,
                    ifIndex));
        }

        private static void SaveInterface(
            SqliteConnectionFactory factory,
            Guid deviceId,
            Guid interfaceId,
            int ifIndex)
        {
            new SqliteMaterializedTopologyRepository(
                factory)
                .SaveInterface(
                    Interface(
                        interfaceId,
                        deviceId,
                        ifIndex));
        }

        private static DeviceInterface Interface(
            Guid interfaceId,
            Guid deviceId,
            int ifIndex)
        {
            return new DeviceInterface(
                interfaceId,
                deviceId,
                ifIndex,
                "if" + ifIndex,
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
                T1);
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