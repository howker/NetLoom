using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Topology;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint15ManualLinkProtectionTests
    {
        [TestMethod]
        public void AutomaticSaveCannotReplaceManualPhysicalLink()
        {
            var directory =
                Path.Combine(
                    Path.GetTempPath(),
                    "NetLoom.Tests",
                    Guid.NewGuid().ToString("N"));

            try
            {
                var connectionFactory =
                    new SqliteConnectionFactory(
                        Path.Combine(
                            directory,
                            "manual-link.db"));

                new DatabaseInitializer(
                    connectionFactory)
                    .Initialize();

                var repository =
                    new SqliteMaterializedTopologyRepository(
                        connectionFactory);

                var factory =
                    new ManualTopologyFactory();

                var a =
                    factory.CreateDevice(
                        Guid.NewGuid(),
                        null,
                        "A",
                        DeviceCategory.MediaConverter,
                        null);

                var b =
                    factory.CreateDevice(
                        Guid.NewGuid(),
                        null,
                        "B",
                        DeviceCategory.MediaConverter,
                        null);

                repository.SaveDevice(a);
                repository.SaveDevice(b);

                var now =
                    new DateTime(
                        2026,
                        1,
                        1,
                        12,
                        0,
                        0,
                        DateTimeKind.Utc);

                var manual =
                    factory.CreateLink(
                        Guid.NewGuid(),
                        a.Id,
                        null,
                        b.Id,
                        null,
                        "Fiber",
                        null,
                        now);

                repository.SavePhysicalLink(manual);

                var automatic =
                    new PhysicalLink(
                        Guid.NewGuid(),
                        a.Id,
                        null,
                        b.Id,
                        null,
                        PhysicalLinkStrength.Confirmed,
                        PhysicalLinkFreshness.Fresh,
                        "Fiber",
                        null,
                        "LLDP",
                        now,
                        now,
                        now,
                        "test",
                        false,
                        false,
                        null);

                AssertInvalidOperation(
                    () =>
                        repository.SavePhysicalLink(
                            automatic));

                Assert.AreEqual(
                    PhysicalLinkStrength.Manual,
                    repository
                        .GetPhysicalLinks()[0]
                        .Strength);
            }
            finally
            {
                System.Data.SQLite.SQLiteConnection
                    .ClearAllPools();

                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void AssertInvalidOperation(
            Action action)
        {
            try
            {
                action();

                Assert.Fail(
                    "InvalidOperationException was expected.");
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
