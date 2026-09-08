using System;
using System.IO;
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
            string instanceId)
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
                    new StpPortState[0]);

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
