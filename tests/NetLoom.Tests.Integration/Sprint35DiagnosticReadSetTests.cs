using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring.Interfaces;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Monitoring;
using NetLoom.Persistence.Sqlite.Topology;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint35DiagnosticReadSetTests
    {
        [TestMethod]
        public void CurrentReadSetIncludesDurableInterfaceDegradationState()
        {
            var path = TemporaryDatabasePath();

            try
            {
                var factory =
                    new SqliteConnectionFactory(path);

                new DatabaseInitializer(factory)
                    .Initialize();

                var deviceId = Guid.NewGuid();
                var interfaceId = Guid.NewGuid();
                var now =
                    new DateTime(
                        2026,
                        9,
                        16,
                        15,
                        0,
                        0,
                        DateTimeKind.Utc);

                var topology =
                    new SqliteMaterializedTopologyRepository(
                        factory);

                topology.SaveDevice(
                    new TopologyDevice(
                        deviceId,
                        null,
                        "Switch A",
                        DeviceCategory.Unknown,
                        DeviceDiscoveryOrigin.Automatic,
                        MonitoringCapability.Unknown,
                        null,
                        null,
                        null,
                        false,
                        false,
                        now.AddHours(-1),
                        now,
                        now,
                        "Switch A",
                        null));

                topology.SaveInterface(
                    new DeviceInterface(
                        interfaceId,
                        deviceId,
                        101,
                        "sfp1",
                        null,
                        null,
                        null,
                        null,
                        "up",
                        "up",
                        1000000000L,
                        null,
                        null,
                        false,
                        false,
                        now.AddHours(-1),
                        now,
                        "sfp1",
                        null));

                new SqliteInterfaceDegradationStateStore(
                    factory)
                    .ReplaceAndGetPrevious(
                        new InterfaceDegradationState(
                            deviceId,
                            101,
                            now,
                            InterfaceDegradationStatus.Degraded,
                            "4"));

                var readSet =
                    new SqliteMaterializedTopologyReadSetReader(
                        factory)
                        .Read("cist");

                var state =
                    readSet.InterfaceDegradationStates.Single();

                Assert.AreEqual(deviceId, state.DeviceId);
                Assert.AreEqual(101, state.IfIndex);
                Assert.AreEqual(InterfaceDegradationStatus.Degraded, state.Status);
                Assert.AreEqual("4", state.EvidenceFingerprint);
            }
            finally
            {
                DeleteDatabase(path);
            }
        }

        [TestMethod]
        public void EmptyCurrentSchemaProducesEmptyDiagnosticDegradationSet()
        {
            var path = TemporaryDatabasePath();

            try
            {
                var factory =
                    new SqliteConnectionFactory(path);

                new DatabaseInitializer(factory)
                    .Initialize();

                var readSet =
                    new SqliteMaterializedTopologyReadSetReader(
                        factory)
                        .Read("cist");

                Assert.AreEqual(
                    0,
                    readSet.InterfaceDegradationStates.Count);

                Assert.AreEqual(
                    0,
                    readSet.PhysicalLinkEvidenceExplanations.Count);
            }
            finally
            {
                DeleteDatabase(path);
            }
        }

        private static string TemporaryDatabasePath()
        {
            return Path.Combine(
                Path.GetTempPath(),
                "netloom-s35-" +
                Guid.NewGuid().ToString("N") +
                ".db");
        }

        private static void DeleteDatabase(
            string path)
        {
            System.Data.SQLite.SQLiteConnection.ClearAllPools();

            foreach (var suffix in
                new[]
                {
                    string.Empty,
                    "-wal",
                    "-shm"
                })
            {
                var candidate = path + suffix;

                if (File.Exists(candidate))
                {
                    File.Delete(candidate);
                }
            }
        }
    }
}
