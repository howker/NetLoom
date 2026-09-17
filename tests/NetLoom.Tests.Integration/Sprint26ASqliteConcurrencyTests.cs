using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Migrations;
using NetLoom.Persistence.Sqlite.Topology;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint26ASqliteConcurrencyTests
    {
        private static readonly DateTime T1 =
            new DateTime(
                2026,
                1,
                1,
                10,
                0,
                0,
                DateTimeKind.Utc);

        private static readonly DateTime T2 =
            new DateTime(
                2026,
                1,
                1,
                11,
                0,
                0,
                DateTimeKind.Utc);

        [TestMethod]
        public void ConnectionFactoryUsesWalBusyTimeoutAndNormalSync()
        {
            WithDatabase(
                (databasePath, factory) =>
                {
                    using (var connection =
                        factory.OpenConnection())
                    {
                        Assert.AreEqual(
                            "wal",
                            ScalarString(
                                connection,
                                "PRAGMA journal_mode;").
                                ToLowerInvariant());

                        Assert.AreEqual(
                            5000L,
                            ScalarInt64(
                                connection,
                                "PRAGMA busy_timeout;"));

                        Assert.AreEqual(
                            1L,
                            ScalarInt64(
                                connection,
                                "PRAGMA synchronous;"));

                        Assert.AreEqual(
                            1L,
                            ScalarInt64(
                                connection,
                                "PRAGMA foreign_keys;"));
                    }
                });
        }

        [TestMethod]
        public void WalAllowsWriterWhileReaderTransactionIsOpen()
        {
            WithDatabase(
                (databasePath, factory) =>
                {
                    new DatabaseInitializer(factory)
                        .Initialize();

                    var repository =
                        new SqliteMaterializedTopologyRepository(
                            factory);

                    repository.SaveDevice(
                        AutomaticDevice(
                            Guid.NewGuid(),
                            "reader-anchor"));

                    using (var readerConnection =
                        factory.OpenConnection())
                    using (var begin =
                        readerConnection.CreateCommand())
                    {
                        begin.CommandText =
                            "BEGIN;";

                        begin.ExecuteNonQuery();

                        try
                        {
                            using (var command =
                                readerConnection.CreateCommand())
                            {
                                command.CommandText =
                                    "SELECT id FROM devices ORDER BY id;";

                                using (var reader =
                                    command.ExecuteReader())
                                {
                                    Assert.IsTrue(
                                        reader.Read());

                                    Exception writerError =
                                        null;

                                    var writer =
                                        Task.Run(
                                            () =>
                                            {
                                                try
                                                {
                                                    new SqliteMaterializedTopologyRepository(
                                                        factory)
                                                        .SaveDevice(
                                                            AutomaticDevice(
                                                                Guid.NewGuid(),
                                                                "writer-during-reader"));
                                                }
                                                catch (Exception exception)
                                                {
                                                    writerError =
                                                        exception;
                                                }
                                            });

                                    var completed =
                                        writer.Wait(3000);

                                    Assert.IsTrue(
                                        completed,
                                        "Writer did not complete while read transaction remained open.");

                                    Assert.IsNull(
                                        writerError,
                                        writerError != null
                                            ? writerError.ToString()
                                            : string.Empty);
                                }
                            }
                        }
                        finally
                        {
                            using (var rollback =
                                readerConnection.CreateCommand())
                            {
                                rollback.CommandText =
                                    "ROLLBACK;";

                                rollback.ExecuteNonQuery();
                            }
                        }
                    }
                });
        }

        [TestMethod]
        public void ConcurrentProvisionalAndRefinedSavePhysicalLinkProduceOneStableLink()
        {
            WithDatabase(
                (databasePath, factory) =>
                {
                    new DatabaseInitializer(factory)
                        .Initialize();

                    for (var attempt = 0;
                         attempt < 12;
                         attempt++)
                    {
                        var setup =
                            new SqliteMaterializedTopologyRepository(
                                factory);

                        var a =
                            AutomaticDevice(
                                Guid.NewGuid(),
                                "A-" +
                                attempt.ToString(
                                    CultureInfo.InvariantCulture));

                        var b =
                            AutomaticDevice(
                                Guid.NewGuid(),
                                "B-" +
                                attempt.ToString(
                                    CultureInfo.InvariantCulture));

                        setup.SaveDevice(a);
                        setup.SaveDevice(b);

                        var portA =
                            AutomaticInterface(
                                Guid.NewGuid(),
                                a.Id,
                                1,
                                "A1");

                        var portB =
                            AutomaticInterface(
                                Guid.NewGuid(),
                                b.Id,
                                1,
                                "B1");

                        setup.SaveInterface(portA);
                        setup.SaveInterface(portB);

                        var provisional =
                            Link(
                                Guid.NewGuid(),
                                a.Id,
                                portA.Id,
                                b.Id,
                                null,
                                T1);

                        var refined =
                            Link(
                                Guid.NewGuid(),
                                a.Id,
                                portA.Id,
                                b.Id,
                                portB.Id,
                                T2);

                        PhysicalLink firstResult =
                            null;

                        PhysicalLink secondResult =
                            null;

                        Exception firstError =
                            null;

                        Exception secondError =
                            null;

                        RunConcurrent(
                            () =>
                            {
                                firstResult =
                                    new SqliteMaterializedTopologyRepository(
                                        factory)
                                        .SavePhysicalLink(
                                            provisional);
                            },
                            () =>
                            {
                                secondResult =
                                    new SqliteMaterializedTopologyRepository(
                                        factory)
                                        .SavePhysicalLink(
                                            refined);
                            },
                            out firstError,
                            out secondError);

                        AssertNoError(
                            firstError);

                        AssertNoError(
                            secondError);

                        Assert.IsNotNull(
                            firstResult);

                        Assert.IsNotNull(
                            secondResult);

                        var matching =
                            setup.GetPhysicalLinks()
                                .Where(
                                    link =>
                                        SamePair(
                                            link,
                                            a.Id,
                                            b.Id))
                                .ToArray();

                        Assert.AreEqual(
                            1,
                            matching.Length);

                        var stored =
                            matching[0];

                        Assert.IsTrue(
                            stored.Id ==
                                provisional.Id ||
                            stored.Id ==
                                refined.Id);

                        Assert.AreEqual(
                            stored.Id,
                            firstResult.Id);

                        Assert.AreEqual(
                            stored.Id,
                            secondResult.Id);

                        Assert.AreEqual(
                            T1,
                            stored.FirstSeenUtc);

                        Assert.AreEqual(
                            T2,
                            stored.LastSeenUtc);

                        AssertEndpoint(
                            stored,
                            a.Id,
                            portA.Id);

                        AssertEndpoint(
                            stored,
                            b.Id,
                            portB.Id);
                    }
                });
        }

        [TestMethod]
        public void ConcurrentManualDeviceTransitionCannotBeOverwrittenByAutomaticSave()
        {
            WithDatabase(
                (databasePath, factory) =>
                {
                    new DatabaseInitializer(factory)
                        .Initialize();

                    for (var attempt = 0;
                         attempt < 12;
                         attempt++)
                    {
                        var id =
                            Guid.NewGuid();

                        var manual =
                            new ManualTopologyFactory()
                                .CreateDevice(
                                    id,
                                    null,
                                    "manual-device",
                                    DeviceCategory.PassiveNetworkEquipment,
                                    null);

                        var automatic =
                            AutomaticDevice(
                                id,
                                "automatic-device");

                        Exception manualError =
                            null;

                        Exception automaticError =
                            null;

                        RunConcurrent(
                            () =>
                            {
                                new SqliteMaterializedTopologyRepository(
                                    factory)
                                    .SaveDevice(
                                        manual);
                            },
                            () =>
                            {
                                try
                                {
                                    new SqliteMaterializedTopologyRepository(
                                        factory)
                                        .SaveDevice(
                                            automatic);
                                }
                                catch (InvalidOperationException)
                                {
                                }
                            },
                            out manualError,
                            out automaticError);

                        AssertNoError(
                            manualError);

                        AssertNoError(
                            automaticError);

                        var stored =
                            new SqliteMaterializedTopologyRepository(
                                factory)
                                .GetDevice(id);

                        Assert.IsNotNull(
                            stored);

                        Assert.AreEqual(
                            DeviceDiscoveryOrigin.Manual,
                            stored.DiscoveryOrigin);
                    }
                });
        }

        [TestMethod]
        public void ConcurrentManualInterfaceTransitionCannotBeOverwrittenByAutomaticSave()
        {
            WithDatabase(
                (databasePath, factory) =>
                {
                    new DatabaseInitializer(factory)
                        .Initialize();

                    var repository =
                        new SqliteMaterializedTopologyRepository(
                            factory);

                    var device =
                        AutomaticDevice(
                            Guid.NewGuid(),
                            "interface-owner");

                    repository.SaveDevice(
                        device);

                    for (var attempt = 0;
                         attempt < 12;
                         attempt++)
                    {
                        var interfaceId =
                            Guid.NewGuid();

                        var manual =
                            new ManualTopologyFactory()
                                .CreateInterface(
                                    interfaceId,
                                    device.Id,
                                    "manual-port",
                                    "Ethernet");

                        var automatic =
                            AutomaticInterface(
                                interfaceId,
                                device.Id,
                                attempt + 1,
                                "auto-port");

                        Exception manualError =
                            null;

                        Exception automaticError =
                            null;

                        RunConcurrent(
                            () =>
                            {
                                new SqliteMaterializedTopologyRepository(
                                    factory)
                                    .SaveInterface(
                                        manual);
                            },
                            () =>
                            {
                                try
                                {
                                    new SqliteMaterializedTopologyRepository(
                                        factory)
                                        .SaveInterface(
                                            automatic);
                                }
                                catch (InvalidOperationException)
                                {
                                }
                            },
                            out manualError,
                            out automaticError);

                        AssertNoError(
                            manualError);

                        AssertNoError(
                            automaticError);

                        var stored =
                            repository.GetInterfaces()
                                .Single(
                                    item =>
                                        item.Id ==
                                            interfaceId);

                        Assert.IsTrue(
                            stored.IsManual);
                    }
                });
        }

        [TestMethod]
        public void ConcurrentInitializeOnCleanDatabaseAppliesAllMigrations()
        {
            WithDatabase(
                (databasePath, factory) =>
                {
                    Exception firstError =
                        null;

                    Exception secondError =
                        null;

                    RunConcurrent(
                        () =>
                        {
                            new DatabaseInitializer(
                                new SqliteConnectionFactory(
                                    databasePath))
                                .Initialize();
                        },
                        () =>
                        {
                            new DatabaseInitializer(
                                new SqliteConnectionFactory(
                                    databasePath))
                                .Initialize();
                        },
                        out firstError,
                        out secondError);

                    AssertNoError(
                        firstError);

                    AssertNoError(
                        secondError);

                    using (var connection =
                        factory.OpenConnection())
                    {
                        Assert.AreEqual(
                            21L,
                            ScalarInt64(
                                connection,
                                "SELECT COUNT(*) FROM schema_migrations;"));

                        Assert.AreEqual(
                            21L,
                            ScalarInt64(
                                connection,
                                "SELECT COUNT(DISTINCT version) FROM schema_migrations;"));
                    }
                },
                initialize: false);
        }

        [TestMethod]
        public void FailedPendingMigrationBatchRollsBackEarlierPendingMigration()
        {
            WithDatabase(
                (databasePath, factory) =>
                {
                    using (var connection =
                        factory.OpenConnection())
                    {
                        var runner =
                            new MigrationRunner(
                                new IMigration[]
                                {
                                    new TestMigration(
                                        101,
                                        "Batch first",
                                        new[]
                                        {
                                            @"
CREATE TABLE sprint26a_batch_first
(
    id INTEGER NOT NULL PRIMARY KEY
);"
                                        }),
                                    new TestMigration(
                                        102,
                                        "Batch failure",
                                        new[]
                                        {
                                            @"
CREATE TABLE sprint26a_batch_second
(
    id INTEGER NOT NULL PRIMARY KEY
);",
                                            "THIS IS NOT VALID SQL;"
                                        })
                                });

                        try
                        {
                            runner.ApplyPending(
                                connection);

                            Assert.Fail(
                                "Failing pending batch must throw.");
                        }
                        catch (SQLiteException)
                        {
                        }

                        Assert.AreEqual(
                            0L,
                            ScalarInt64(
                                connection,
                                @"
SELECT COUNT(*)
FROM sqlite_master
WHERE type = 'table'
  AND name = 'sprint26a_batch_first';"));

                        Assert.AreEqual(
                            0L,
                            ScalarInt64(
                                connection,
                                @"
SELECT COUNT(*)
FROM sqlite_master
WHERE type = 'table'
  AND name = 'sprint26a_batch_second';"));

                        Assert.AreEqual(
                            0L,
                            ScalarInt64(
                                connection,
                                @"
SELECT COUNT(*)
FROM schema_migrations
WHERE version IN (101, 102);"));
                    }
                },
                initialize: false);
        }

        private static void WithDatabase(
            Action<string, SqliteConnectionFactory> action,
            bool initialize = false)
        {
            var directory =
                Path.Combine(
                    Path.GetTempPath(),
                    "NetLoom.Tests",
                    Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(
                directory);

            var databasePath =
                Path.Combine(
                    directory,
                    "sprint26a.db");

            try
            {
                var factory =
                    new SqliteConnectionFactory(
                        databasePath);

                if (initialize)
                {
                    new DatabaseInitializer(factory)
                        .Initialize();
                }

                action(
                    databasePath,
                    factory);
            }
            finally
            {
                SQLiteConnection.ClearAllPools();

                if (Directory.Exists(
                    directory))
                {
                    Directory.Delete(
                        directory,
                        true);
                }
            }
        }

        private static void RunConcurrent(
            Action first,
            Action second,
            out Exception firstError,
            out Exception secondError)
        {
            Exception firstFailure =
                null;

            Exception secondFailure =
                null;

            using (var gate =
                new ManualResetEventSlim(false))
            {
                var firstTask =
                    Task.Run(
                        () =>
                        {
                            gate.Wait();

                            try
                            {
                                first();
                            }
                            catch (Exception exception)
                            {
                                firstFailure =
                                    exception;
                            }
                        });

                var secondTask =
                    Task.Run(
                        () =>
                        {
                            gate.Wait();

                            try
                            {
                                second();
                            }
                            catch (Exception exception)
                            {
                                secondFailure =
                                    exception;
                            }
                        });

                gate.Set();

                var completed =
                    Task.WaitAll(
                        new[]
                        {
                            firstTask,
                            secondTask
                        },
                        15000);

                Assert.IsTrue(
                    completed,
                    "Concurrent operations did not finish within 15 seconds.");
            }

            firstError =
                firstFailure;

            secondError =
                secondFailure;
        }

        private static void AssertNoError(
            Exception exception)
        {
            if (exception != null)
            {
                Assert.Fail(
                    exception.ToString());
            }
        }

        private static TopologyDevice AutomaticDevice(
            Guid id,
            string name)
        {
            return new TopologyDevice(
                id,
                null,
                name,
                DeviceCategory.Unknown,
                DeviceDiscoveryOrigin.Automatic,
                MonitoringCapability.Unknown,
                null,
                null,
                null,
                false,
                false,
                T1,
                T1,
                T1);
        }

        private static DeviceInterface AutomaticInterface(
            Guid id,
            Guid deviceId,
            int ifIndex,
            string ifName)
        {
            var constructor =
                typeof(DeviceInterface)
                    .GetConstructors()
                    .Single();

            var parameters =
                constructor.GetParameters();

            var arguments =
                new object[parameters.Length];

            for (var index = 0;
                 index < parameters.Length;
                 index++)
            {
                var parameter =
                    parameters[index];

                switch (parameter.Name)
                {
                    case "id":
                        arguments[index] = id;
                        break;

                    case "deviceId":
                        arguments[index] = deviceId;
                        break;

                    case "ifIndex":
                        arguments[index] = ifIndex;
                        break;

                    case "ifName":
                        arguments[index] = ifName;
                        break;

                    case "isManual":
                        arguments[index] = false;
                        break;

                    case "firstSeenUtc":
                    case "lastSeenUtc":
                        arguments[index] = T1;
                        break;

                    default:
                        arguments[index] =
                            DefaultArgument(
                                parameter);
                        break;
                }
            }

            return
                (DeviceInterface)
                    constructor.Invoke(
                        arguments);
        }

        private static object DefaultArgument(
            ParameterInfo parameter)
        {
            if (parameter.HasDefaultValue)
            {
                return parameter.DefaultValue;
            }

            var type =
                parameter.ParameterType;

            if (!type.IsValueType ||
                Nullable.GetUnderlyingType(type) !=
                    null)
            {
                return null;
            }

            if (type == typeof(bool))
            {
                return false;
            }

            if (type == typeof(DateTime))
            {
                return T1;
            }

            if (type == typeof(Guid))
            {
                return Guid.NewGuid();
            }

            return Activator.CreateInstance(
                type);
        }

        private static PhysicalLink Link(
            Guid id,
            Guid deviceAId,
            Guid? interfaceAId,
            Guid deviceBId,
            Guid? interfaceBId,
            DateTime observedUtc)
        {
            return new PhysicalLink(
                id,
                deviceAId,
                interfaceAId,
                deviceBId,
                interfaceBId,
                PhysicalLinkStrength.Confirmed,
                PhysicalLinkFreshness.Fresh,
                null,
                null,
                "sprint26a",
                observedUtc,
                observedUtc,
                observedUtc,
                "sprint26a",
                false,
                false,
                null);
        }

        private static bool SamePair(
            PhysicalLink link,
            Guid first,
            Guid second)
        {
            return
                (link.DeviceAId == first &&
                 link.DeviceBId == second) ||
                (link.DeviceAId == second &&
                 link.DeviceBId == first);
        }

        private static void AssertEndpoint(
            PhysicalLink link,
            Guid deviceId,
            Guid interfaceId)
        {
            var actual =
                link.DeviceAId == deviceId
                    ? link.InterfaceAId
                    : link.InterfaceBId;

            Assert.AreEqual(
                interfaceId,
                actual);
        }

        private static long ScalarInt64(
            SQLiteConnection connection,
            string sql)
        {
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText =
                    sql;

                return Convert.ToInt64(
                    command.ExecuteScalar(),
                    CultureInfo.InvariantCulture);
            }
        }

        private static string ScalarString(
            SQLiteConnection connection,
            string sql)
        {
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText =
                    sql;

                return Convert.ToString(
                    command.ExecuteScalar(),
                    CultureInfo.InvariantCulture);
            }
        }

        private sealed class TestMigration :
            IMigration
        {
            public TestMigration(
                int version,
                string name,
                IReadOnlyList<string> statements)
            {
                Version = version;
                Name = name;
                Statements = statements;
            }

            public int Version { get; }

            public string Name { get; }

            public IReadOnlyList<string>
                Statements { get; }
        }
    }
}
