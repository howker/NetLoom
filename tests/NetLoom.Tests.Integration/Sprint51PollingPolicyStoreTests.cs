using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.PollingPolicies;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Migrations;
using NetLoom.Persistence.Sqlite.PollingPolicies;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint51PollingPolicyStoreTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void NewAndUpgradedDatabasesHaveTheLegacyDefault()
        {
            WithDatabase(factory =>
            {
                var store = new SqlitePollingPolicyStore(factory);
                AssertDefault(store);
            });
            WithDatabase(factory =>
            {
                using (var connection = factory.OpenConnection())
                {
                    var migrationTypes = typeof(Migration001Initial).Assembly.GetTypes()
                        .Where(type => typeof(IMigration).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                        .Where(type => type.Name.StartsWith("Migration", StringComparison.Ordinal))
                        .Select(type => (IMigration)Activator.CreateInstance(type))
                        .Where(migration => migration.Version <= 27).ToArray();
                    Assert.AreEqual(27, migrationTypes.Length);
                    new MigrationRunner(migrationTypes).ApplyPending(connection);
                    InsertLocation(connection, Guid.Parse("00000000-0000-0000-0000-000000000001"), null);
                    InsertDevice(connection, Guid.Parse("00000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000001"));
                }
                new DatabaseInitializer(factory).Initialize();
                var store = new SqlitePollingPolicyStore(factory);
                AssertDefault(store);
                Assert.AreEqual(0, store.GetAssignments().Count);
                Assert.AreEqual(PollingPolicySource.Default, store.LoadResolver().ResolveDevice(Guid.Parse("00000000-0000-0000-0000-000000000002")).Source);
                Assert.AreEqual(PollingPolicySource.Default, store.LoadResolver().ResolveLocation(Guid.Parse("00000000-0000-0000-0000-000000000001")).Source);
            }, false);
        }

        [TestMethod]
        public void SaveReadUpdateAndNameUniqueness()
        {
            WithDatabase(factory =>
            {
                var store = new SqlitePollingPolicyStore(factory);
                var policy = NewPolicy("Custom", new[] { 443, 22 });
                store.SavePolicy(policy, Now);
                var read = store.GetPolicy(policy.Id);
                Assert.AreEqual(PollingScheduleMode.Interval, read.StateSchedule.Mode);
                Assert.AreEqual(60, read.StateSchedule.IntervalSeconds);
                Assert.AreEqual(PollingScheduleMode.Once, read.TopologySchedule.Mode);
                CollectionAssert.AreEqual(new[] { 22, 443 }, read.TcpPorts.ToArray());
                Assert.AreEqual(PollingPolicy.DefaultPolicyId, store.GetPolicies()[0].Id);
                Assert.ThrowsExactly<InvalidOperationException>(() => store.SavePolicy(NewPolicy("cUsToM", new int[0]), Now));
                store.SavePolicy(new PollingPolicy(policy.Id, "Custom", false, true,
                    PollingSchedule.Off, PollingSchedule.General, new[] { 80 }), Now);
                Assert.AreEqual(PollingScheduleMode.Off, store.GetPolicy(policy.Id).StateSchedule.Mode);
                CollectionAssert.AreEqual(new[] { 80 }, store.GetPolicy(policy.Id).TcpPorts.ToArray());
                Assert.ThrowsExactly<InvalidOperationException>(() => store.SavePolicy(new PollingPolicy(
                    PollingPolicy.DefaultPolicyId, "Default", false, true, PollingSchedule.General,
                    PollingSchedule.General, new[] { 22 }), Now));
                Assert.ThrowsExactly<InvalidOperationException>(() => store.SavePolicy(new PollingPolicy(
                    Guid.NewGuid(), "Another", true, true, PollingSchedule.General,
                    PollingSchedule.General, new[] { 22 }), Now));
                Assert.ThrowsExactly<ArgumentException>(() => store.SavePolicy(policy, DateTime.Now));
            });
        }

        [TestMethod]
        public void AssignInheritanceUsageDeletionAndCascade()
        {
            WithDatabase(factory =>
            {
                var root = Guid.NewGuid();
                var child = Guid.NewGuid();
                var device = Guid.NewGuid();
                using (var connection = factory.OpenConnection())
                {
                    InsertLocation(connection, root, null);
                    InsertLocation(connection, child, root);
                    InsertDevice(connection, device, child);
                }
                var store = new SqlitePollingPolicyStore(factory);
                var policy = NewPolicy("Site", new[] { 22 });
                store.SavePolicy(policy, Now);
                store.Assign(PollingPolicySubjectKind.Location, root, policy.Id, Now);
                var effective = store.LoadResolver().ResolveDevice(device);
                Assert.AreEqual(policy.Id, effective.Policy.Id);
                Assert.AreEqual(PollingPolicySource.Location, effective.Source);
                Assert.AreEqual(root, effective.SourceLocationId);
                store.Assign(PollingPolicySubjectKind.Device, device, policy.Id, Now);
                Assert.AreEqual(PollingPolicySource.Device, store.LoadResolver().ResolveDevice(device).Source);
                Assert.AreEqual(1, store.GetUsage(policy.Id).DeviceCount);
                Assert.AreEqual(1, store.GetUsage(policy.Id).LocationCount);
                Assert.AreEqual(PollingPolicyDeleteOutcome.DefaultPolicy, store.DeletePolicy(PollingPolicy.DefaultPolicyId));
                Assert.AreEqual(PollingPolicyDeleteOutcome.InUse, store.DeletePolicy(policy.Id));
                using (var connection = factory.OpenConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM devices WHERE id = @id;";
                    command.Parameters.AddWithValue("@id", device.ToString("D"));
                    command.ExecuteNonQuery();
                }
                Assert.AreEqual(0, store.GetUsage(policy.Id).DeviceCount);
                store.Assign(PollingPolicySubjectKind.Location, root, null, Now);
                Assert.IsFalse(store.GetUsage(policy.Id).IsUsed);
                Assert.AreEqual(PollingPolicyDeleteOutcome.Deleted, store.DeletePolicy(policy.Id));
                Assert.AreEqual(PollingPolicyDeleteOutcome.NotFound, store.DeletePolicy(policy.Id));
                Assert.ThrowsExactly<InvalidOperationException>(() => store.Assign(PollingPolicySubjectKind.Location,
                    root, policy.Id, Now));
            });
        }

        private static PollingPolicy NewPolicy(string name, int[] ports) => new PollingPolicy(Guid.NewGuid(), name,
            false, true, PollingSchedule.Every(60), PollingSchedule.Once, ports);

        private static void AssertDefault(SqlitePollingPolicyStore store)
        {
            Assert.AreEqual(1, store.GetPolicies().Count);
            var policy = store.GetPolicies()[0];
            var expected = PollingPolicy.CreateDefault();
            Assert.AreEqual(expected.Id, policy.Id);
            Assert.AreEqual(expected.Name, policy.Name);
            Assert.IsTrue(policy.IsDefault);
            Assert.IsTrue(policy.ActivePolling);
            Assert.AreEqual(expected.StateSchedule, policy.StateSchedule);
            Assert.AreEqual(expected.TopologySchedule, policy.TopologySchedule);
            CollectionAssert.AreEqual(expected.TcpPorts.ToArray(), policy.TcpPorts.ToArray());
        }

        private static void InsertLocation(SQLiteConnection connection, Guid id, Guid? parent)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"INSERT INTO locations (location_id, parent_location_id, name, created_utc, updated_utc)
VALUES (@id, @parent, 'Location', @now, @now);";
                command.Parameters.AddWithValue("@id", id.ToString("D"));
                command.Parameters.AddWithValue("@parent", parent.HasValue ? (object)parent.Value.ToString("D") : DBNull.Value);
                command.Parameters.AddWithValue("@now", Now.ToString("o"));
                command.ExecuteNonQuery();
            }
        }

        private static void InsertDevice(SQLiteConnection connection, Guid id, Guid location)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"INSERT INTO devices (id, location_id, created_at_utc, updated_at_utc)
VALUES (@id, @location, @now, @now);";
                command.Parameters.AddWithValue("@id", id.ToString("D"));
                command.Parameters.AddWithValue("@location", location.ToString("D"));
                command.Parameters.AddWithValue("@now", Now.ToString("o"));
                command.ExecuteNonQuery();
            }
        }

        private static void WithDatabase(Action<SqliteConnectionFactory> action, bool initialize = true)
        {
            var directory = Path.Combine(Path.GetTempPath(), "NetLoom.Tests", Guid.NewGuid().ToString("N"));
            try
            {
                var factory = new SqliteConnectionFactory(Path.Combine(directory, "netloom.db"));
                if (initialize) new DatabaseInitializer(factory).Initialize();
                action(factory);
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
