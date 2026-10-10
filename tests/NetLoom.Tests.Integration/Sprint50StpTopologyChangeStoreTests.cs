using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Observations;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Stp;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Observations;
using NetLoom.Persistence.Sqlite.Stp;
using NetLoom.Persistence.Sqlite.Topology;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint50StpTopologyChangeStoreTests
    {
        private string _databasePath;

        [TestInitialize]
        public void Initialize()
        {
            _databasePath =
                Path.Combine(
                    Path.GetTempPath(),
                    "NetLoom.Sprint50.StpChange." +
                    Guid.NewGuid().ToString("N") +
                    ".sqlite");
        }

        [TestCleanup]
        public void Cleanup()
        {
            SQLiteConnection.ClearAllPools();

            Delete(_databasePath);
            Delete(_databasePath + "-wal");
            Delete(_databasePath + "-shm");
        }

        [TestMethod]
        public void GetReturnsBothTopologyChangeFields()
        {
            var factory = Factory();
            var saved = Save(factory, 240000L, 7L);

            var loaded =
                new SqliteStpObservationStore(factory)
                    .Get(saved.Observation.Id);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(240000L, loaded.TimeSinceTopologyChangeCentiseconds);
            Assert.AreEqual(7L, loaded.TopologyChangeCount);
        }

        [TestMethod]
        public void GetLatestReturnsBothTopologyChangeFields()
        {
            var factory = Factory();
            var saved = Save(factory, 51840000L, 4L);
            var deviceId = Guid.NewGuid();

            new SqliteObservationDeviceBindingStore(factory)
                .Bind(saved.Observation.Id, deviceId);

            var result =
                new SqliteStpObservationStore(factory)
                    .GetLatest("cist");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(
                51840000L,
                result[0].Observation.TimeSinceTopologyChangeCentiseconds);
            Assert.AreEqual(
                4L,
                result[0].Observation.TopologyChangeCount);
        }

        [TestMethod]
        public void MaterializedReadSetReturnsBothTopologyChangeFields()
        {
            var factory = Factory();
            var saved = Save(factory, 123L, 9L);
            var deviceId = Guid.NewGuid();

            new SqliteObservationDeviceBindingStore(factory)
                .Bind(saved.Observation.Id, deviceId);

            var readSet =
                new SqliteMaterializedTopologyReadSetReader(factory)
                    .Read("cist");

            var stp =
                readSet.LatestStp.Single(
                    item => item.DeviceId == deviceId);

            Assert.AreEqual(
                123L,
                stp.Observation.TimeSinceTopologyChangeCentiseconds);
            Assert.AreEqual(
                9L,
                stp.Observation.TopologyChangeCount);
        }

        [TestMethod]
        public void NullFieldsStayNullInAllReadPaths()
        {
            var factory = Factory();
            var saved = Save(factory, null, null);
            var deviceId = Guid.NewGuid();

            new SqliteObservationDeviceBindingStore(factory)
                .Bind(saved.Observation.Id, deviceId);

            var store = new SqliteStpObservationStore(factory);

            var loaded = store.Get(saved.Observation.Id);
            Assert.IsNull(loaded.TimeSinceTopologyChangeCentiseconds);
            Assert.IsNull(loaded.TopologyChangeCount);

            var latest = store.GetLatest("cist").Single();
            Assert.IsNull(latest.Observation.TimeSinceTopologyChangeCentiseconds);
            Assert.IsNull(latest.Observation.TopologyChangeCount);

            var materialized =
                new SqliteMaterializedTopologyReadSetReader(factory)
                    .Read("cist")
                    .LatestStp.Single();

            Assert.IsNull(materialized.Observation.TimeSinceTopologyChangeCentiseconds);
            Assert.IsNull(materialized.Observation.TopologyChangeCount);
        }

        private SqliteConnectionFactory Factory()
        {
            var factory = new SqliteConnectionFactory(_databasePath);

            new DatabaseInitializer(factory).Initialize();

            return factory;
        }

        private static StpObservation Save(
            SqliteConnectionFactory factory,
            long? timeSince,
            long? count)
        {
            var observation =
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Stp,
                    "192.0.2.50",
                    new DateTime(
                        2026, 10, 10, 12, 0, 0,
                        DateTimeKind.Utc));

            new SqliteObservationStore(factory)
                .SaveSnmp(
                    new SnmpObservation(
                        observation,
                        new[]
                        {
                            new SnmpVariable(
                                "1.3.6.1.2.1.17.2.1.0",
                                2,
                                "3",
                                new byte[] { 3 })
                        }));

            var stp =
                new StpObservation(
                    observation,
                    "cist",
                    3,
                    "8000.001122334455",
                    20000,
                    5,
                    205,
                    new[]
                    {
                        new StpPortState(
                            5,
                            205,
                            128,
                            5,
                            1,
                            200000,
                            "8000.001122334455",
                            0,
                            "8000.00AABBCCDDEE",
                            "128.5",
                            9)
                    },
                    timeSince,
                    count);

            new SqliteStpObservationStore(factory).Save(stp);

            return stp;
        }

        private static void Delete(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
