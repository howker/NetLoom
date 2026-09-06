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
    public sealed class StpObservationStoreTests
    {
        [TestMethod]
        public void NormalizedStpObservationRoundTripsWithRawParent()
        {
            var path =
                Path.Combine(
                    Path.GetTempPath(),
                    "netloom-stp-" +
                    Guid.NewGuid().ToString("N") +
                    ".db");

            try
            {
                var factory =
                    new SqliteConnectionFactory(
                        path);

                new DatabaseInitializer(
                    factory).Initialize();

                var rawStore =
                    new SqliteObservationStore(
                        factory);

                var stpStore =
                    new SqliteStpObservationStore(
                        factory);

                var observation =
                    new Observation(
                        Guid.NewGuid(),
                        ObservationKind.Stp,
                        "192.0.2.23",
                        new DateTime(
                            2026, 1, 5, 14, 0, 0,
                            DateTimeKind.Utc));

                rawStore.SaveSnmp(
                    new SnmpObservation(
                        observation,
                        new[]
                        {
                            new SnmpVariable(
                                "1.3.6.1.2.1.17.2.15.1.3.5",
                                2,
                                "5",
                                new byte[] { 5 })
                        }));

                stpStore.Save(
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
                        }));

                var loaded =
                    stpStore.Get(
                        observation.Id);

                Assert.IsNotNull(loaded);

                Assert.AreEqual(
                    observation.Id,
                    loaded.Observation.Id);

                Assert.AreEqual(
                    ObservationKind.Stp,
                    loaded.Observation.Kind);

                Assert.AreEqual(
                    "cist",
                    loaded.InstanceId);

                Assert.AreEqual(
                    5,
                    loaded.RootPortBridgePortIndex);

                Assert.AreEqual(
                    205,
                    loaded.RootPortIfIndex);

                Assert.AreEqual(
                    1,
                    loaded.Ports.Count);

                Assert.AreEqual(
                    5,
                    loaded.Ports[0].BridgePortIndex);

                Assert.AreEqual(
                    205,
                    loaded.Ports[0].IfIndex);

                Assert.AreEqual(
                    200000L,
                    loaded.Ports[0].PathCost);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
