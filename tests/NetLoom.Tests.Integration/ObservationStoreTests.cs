using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Observations;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Observations;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class ObservationStoreTests
    {
        private string _databasePath;

        [TestInitialize]
        public void Initialize()
        {
            _databasePath = Path.Combine(
                Path.GetTempPath(),
                "NetLoom.Tests." +
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
        public void RawSnmpObservationRoundTrips()
        {
            var factory =
                new SqliteConnectionFactory(_databasePath);

            new DatabaseInitializer(factory)
                .Initialize();

            var store =
                new SqliteObservationStore(factory);

            var observation =
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.SnmpInventory,
                    "192.0.2.10",
                    DateTime.UtcNow);

            store.SaveSnmp(
                new SnmpObservation(
                    observation,
                    new[]
                    {
                        new SnmpVariable(
                            "1.3.6.1.2.1.1.5.0",
                            4,
                            "switch-core-01",
                            new byte[] { 1, 2, 3 }),

                        new SnmpVariable(
                            "1.3.6.1.2.1.1.2.0",
                            6,
                            "1.3.6.1.4.1.9999",
                            new byte[] { 4, 5, 6 })
                    }));

            var loaded =
                store.GetSnmp(observation.Id);

            Assert.IsNotNull(loaded);

            Assert.AreEqual(
                observation.Id,
                loaded.Observation.Id);

            Assert.AreEqual(
                ObservationKind.SnmpInventory,
                loaded.Observation.Kind);

            Assert.AreEqual(
                "192.0.2.10",
                loaded.Observation.SourceAddress);

            Assert.AreEqual(
                observation.CapturedUtc,
                loaded.Observation.CapturedUtc);

            Assert.AreEqual(
                2,
                loaded.Variables.Count);

            Assert.AreEqual(
                "1.3.6.1.2.1.1.5.0",
                loaded.Variables[0].Oid);

            Assert.AreEqual(
                "switch-core-01",
                loaded.Variables[0].DisplayValue);

            CollectionAssert.AreEqual(
                new byte[] { 1, 2, 3 },
                loaded.Variables[0].GetEncodedValue());
        }

        [TestMethod]
        public void RemovingObservationCascadesRawVarbinds()
        {
            var factory =
                new SqliteConnectionFactory(_databasePath);

            new DatabaseInitializer(factory)
                .Initialize();

            var id = Guid.NewGuid();

            var store =
                new SqliteObservationStore(factory);

            store.SaveSnmp(
                new SnmpObservation(
                    new Observation(
                        id,
                        ObservationKind.SnmpInventory,
                        "192.0.2.20",
                        DateTime.UtcNow),
                    new[]
                    {
                        new SnmpVariable(
                            "1.3.6.1.2.1.1.5.0",
                            4,
                            "switch-20",
                            new byte[] { 10 })
                    }));

            using (var connection =
                factory.OpenConnection())
            {
                using (var delete =
                    connection.CreateCommand())
                {
                    delete.CommandText = @"
DELETE FROM observations
WHERE observation_id = @id;";

                    delete.Parameters.AddWithValue(
                        "@id",
                        id.ToString("D"));

                    delete.ExecuteNonQuery();
                }

                using (var count =
                    connection.CreateCommand())
                {
                    count.CommandText = @"
SELECT COUNT(*)
FROM snmp_varbinds
WHERE observation_id = @id;";

                    count.Parameters.AddWithValue(
                        "@id",
                        id.ToString("D"));

                    Assert.AreEqual(
                        0L,
                        Convert.ToInt64(
                            count.ExecuteScalar()));
                }
            }
        }

        [TestMethod]
        public void UnknownObservationReturnsNull()
        {
            var factory =
                new SqliteConnectionFactory(_databasePath);

            new DatabaseInitializer(factory)
                .Initialize();

            var store =
                new SqliteObservationStore(factory);

            Assert.IsNull(
                store.GetSnmp(Guid.NewGuid()));
        }
    }
}
