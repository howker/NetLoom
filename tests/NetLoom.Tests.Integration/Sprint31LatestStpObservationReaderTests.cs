using System;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Observations;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Stp;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Observations;
using NetLoom.Persistence.Sqlite.Stp;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class
        Sprint31LatestStpObservationReaderTests
    {
        private static readonly DateTime T1 =
            new DateTime(
                2026,
                9,
                8,
                15,
                0,
                0,
                DateTimeKind.Utc);

        private static readonly DateTime T2 =
            new DateTime(
                2026,
                9,
                8,
                15,
                1,
                0,
                DateTimeKind.Utc);

        private string _databasePath;

        [TestInitialize]
        public void Initialize()
        {
            _databasePath =
                Path.Combine(
                    Path.GetTempPath(),
                    "NetLoom.Sprint31.Stp." +
                    Guid.NewGuid().ToString("N") +
                    ".sqlite");
        }

        [TestCleanup]
        public void Cleanup()
        {
            DeleteIfExists(
                _databasePath);

            DeleteIfExists(
                _databasePath + "-wal");

            DeleteIfExists(
                _databasePath + "-shm");
        }

        [TestMethod]
        public void
            LatestReadUsesStableBindingAndIgnoresUnboundObservation()
        {
            var factory =
                Factory();

            var deviceId =
                Guid.NewGuid();

            var older =
                SaveStp(
                    factory,
                    "192.0.2.60",
                    T1,
                    "cist");

            var newer =
                SaveStp(
                    factory,
                    "192.0.2.61",
                    T2,
                    "cist");

            SaveStp(
                factory,
                "192.0.2.62",
                T2,
                "cist");

            var bindings =
                new SqliteObservationDeviceBindingStore(
                    factory);

            bindings.Bind(
                older.Observation.Id,
                deviceId);

            bindings.Bind(
                newer.Observation.Id,
                deviceId);

            var result =
                new SqliteStpObservationStore(
                    factory)
                    .GetLatest(
                        "cist");

            Assert.AreEqual(
                1,
                result.Count);

            Assert.AreEqual(
                deviceId,
                result[0].DeviceId);

            Assert.AreEqual(
                newer.Observation.Id,
                result[0]
                    .Observation
                    .Observation
                    .Id);

            Assert.AreEqual(
                "192.0.2.61",
                result[0]
                    .Observation
                    .Observation
                    .SourceAddress);
        }

        [TestMethod]
        public void
            LatestReadDoesNotReuseAnotherStpInstance()
        {
            var factory =
                Factory();

            var deviceId =
                Guid.NewGuid();

            var observation =
                SaveStp(
                    factory,
                    "192.0.2.70",
                    T2,
                    "cist");

            new SqliteObservationDeviceBindingStore(
                factory)
                .Bind(
                    observation.Observation.Id,
                    deviceId);

            var result =
                new SqliteStpObservationStore(
                    factory)
                    .GetLatest(
                        "mst-1");

            Assert.AreEqual(
                0,
                result.Count);
        }

        [TestMethod]
        public void
            LatestReadKeepsNewestObservationAndAllPorts()
        {
            var factory =
                Factory();

            var deviceId =
                Guid.NewGuid();

            var older =
                SaveStp(
                    factory,
                    "192.0.2.90",
                    T1,
                    "cist",
                    new StpPortState(
                        99,
                        199,
                        128,
                        5,
                        1,
                        9999,
                        "older-root",
                        900,
                        "older-bridge",
                        "older-port",
                        9));

            var newer =
                SaveStp(
                    factory,
                    "192.0.2.91",
                    T2,
                    "cist",
                    new StpPortState(
                        17,
                        117,
                        64,
                        5,
                        1,
                        1700,
                        "new-root",
                        170,
                        "bridge-17",
                        "port-17",
                        1),
                    new StpPortState(
                        23,
                        123,
                        96,
                        2,
                        1,
                        2300,
                        "new-root",
                        230,
                        "bridge-23",
                        "port-23",
                        2));

            var bindings =
                new SqliteObservationDeviceBindingStore(
                    factory);

            bindings.Bind(
                older.Observation.Id,
                deviceId);

            bindings.Bind(
                newer.Observation.Id,
                deviceId);

            var result =
                new SqliteStpObservationStore(
                    factory)
                    .GetLatest(
                        "cist");

            Assert.AreEqual(
                1,
                result.Count);

            Assert.AreEqual(
                newer.Observation.Id,
                result[0]
                    .Observation
                    .Observation
                    .Id);

            Assert.AreEqual(
                2,
                result[0]
                    .Observation
                    .Ports
                    .Count);

            Assert.AreEqual(
                17,
                result[0]
                    .Observation
                    .Ports[0]
                    .BridgePortIndex);

            Assert.AreEqual(
                1700L,
                result[0]
                    .Observation
                    .Ports[0]
                    .PathCost);

            Assert.AreEqual(
                23,
                result[0]
                    .Observation
                    .Ports[1]
                    .BridgePortIndex);

            Assert.AreEqual(
                2300L,
                result[0]
                    .Observation
                    .Ports[1]
                    .PathCost);
        }

        [TestMethod]
        public void
            LatestReadUsesOneSqliteConnectionForMultipleDevices()
        {
            var setupFactory =
                Factory();

            var deviceA =
                Guid.NewGuid();

            var deviceB =
                Guid.NewGuid();

            var observationA =
                SaveStp(
                    setupFactory,
                    "192.0.2.80",
                    T1,
                    "cist");

            var observationB =
                SaveStp(
                    setupFactory,
                    "192.0.2.81",
                    T2,
                    "cist");

            var bindings =
                new SqliteObservationDeviceBindingStore(
                    setupFactory);

            bindings.Bind(
                observationA.Observation.Id,
                deviceA);

            bindings.Bind(
                observationB.Observation.Id,
                deviceB);

            var openedConnections =
                0;

            var countingFactory =
                new SqliteConnectionFactory(
                    _databasePath,
                    () =>
                        Interlocked.Increment(
                            ref openedConnections));

            var result =
                new SqliteStpObservationStore(
                    countingFactory)
                    .GetLatest(
                        "cist");

            Assert.AreEqual(
                2,
                result.Count);

            Assert.AreEqual(
                1,
                openedConnections,
                "GetLatest must use one SQLite connection " +
                "regardless of the number of selected devices.");
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

        private static StpObservation SaveStp(
            SqliteConnectionFactory factory,
            string sourceAddress,
            DateTime capturedUtc,
            string instanceId,
            params StpPortState[] ports)
        {
            var observation =
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Stp,
                    sourceAddress,
                    capturedUtc);

            new SqliteObservationStore(
                factory)
                .SaveSnmp(
                    new SnmpObservation(
                        observation,
                        new SnmpVariable[0]));

            var stp =
                new StpObservation(
                    observation,
                    instanceId,
                    null,
                    null,
                    null,
                    null,
                    null,
                    ports);

            new SqliteStpObservationStore(
                factory)
                .Save(
                    stp);

            return stp;
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
