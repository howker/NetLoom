using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.MapLayout;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.MapLayout;
using NetLoom.Persistence.Sqlite.Topology;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint36MapLayoutStoreTests
    {
        [TestMethod]
        public void Migration019CreatesMapAndDeviceLayoutTables()
        {
            WithDatabase(
                factory =>
                {
                    using (var connection =
                        factory.OpenReadOnlyConnection())
                    using (var command =
                        connection.CreateCommand())
                    {
                        command.CommandText = @"
SELECT MAX(version)
FROM schema_migrations;";

                        Assert.AreEqual(
                            19L,
                            Convert.ToInt64(
                                command.ExecuteScalar()));
                    }

                    using (var connection =
                        factory.OpenReadOnlyConnection())
                    using (var command =
                        connection.CreateCommand())
                    {
                        command.CommandText = @"
SELECT COUNT(*)
FROM sqlite_master
WHERE type = 'table'
  AND name IN ('maps', 'map_device_layout');";

                        Assert.AreEqual(
                            2L,
                            Convert.ToInt64(
                                command.ExecuteScalar()));
                    }
                });
        }

        [TestMethod]
        public void FreshStoreLoadsDefaultViewportWithoutCreatingRows()
        {
            WithDatabase(
                factory =>
                {
                    var snapshot =
                        new SqliteMapLayoutStore(
                            factory)
                            .Load(
                                MapLayoutScope.PhysicalTopologyMapId);

                    Assert.AreEqual(
                        1.0,
                        snapshot.Viewport.Zoom,
                        0.0001);

                    Assert.AreEqual(
                        0.0,
                        snapshot.Viewport.PanX,
                        0.0001);

                    Assert.AreEqual(
                        0.0,
                        snapshot.Viewport.PanY,
                        0.0001);

                    Assert.AreEqual(
                        0,
                        snapshot.Devices.Count);

                    using (var connection =
                        factory.OpenReadOnlyConnection())
                    using (var command =
                        connection.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT COUNT(*) FROM maps;";

                        Assert.AreEqual(
                            0L,
                            Convert.ToInt64(
                                command.ExecuteScalar()));
                    }
                });
        }

        [TestMethod]
        public void ViewportAndDeviceLayoutSurviveStoreRestart()
        {
            WithDatabase(
                factory =>
                {
                    var deviceId =
                        SaveDevice(factory);

                    var first =
                        new SqliteMapLayoutStore(
                            factory);

                    first.SaveViewport(
                        MapLayoutScope.PhysicalTopologyMapId,
                        new MapViewportLayout(
                            1.4,
                            125.0,
                            80.0));

                    first.SaveDevice(
                        MapLayoutScope.PhysicalTopologyMapId,
                        new MapDeviceLayout(
                            deviceId,
                            310.0,
                            245.0,
                            true));

                    var restarted =
                        new SqliteMapLayoutStore(
                            factory);

                    var snapshot =
                        restarted.Load(
                            MapLayoutScope.PhysicalTopologyMapId);

                    Assert.AreEqual(
                        1.4,
                        snapshot.Viewport.Zoom,
                        0.0001);

                    Assert.AreEqual(
                        125.0,
                        snapshot.Viewport.PanX,
                        0.0001);

                    Assert.AreEqual(
                        80.0,
                        snapshot.Viewport.PanY,
                        0.0001);

                    var device =
                        snapshot.Devices.Single();

                    Assert.AreEqual(
                        deviceId,
                        device.DeviceId);

                    Assert.AreEqual(
                        310.0,
                        device.X,
                        0.0001);

                    Assert.AreEqual(
                        245.0,
                        device.Y,
                        0.0001);

                    Assert.IsTrue(
                        device.IsLocked);
                });
        }

        [TestMethod]
        public void DeviceLayoutUpdatesInPlaceAndRejectsUnknownDevice()
        {
            WithDatabase(
                factory =>
                {
                    var deviceId =
                        SaveDevice(factory);

                    var store =
                        new SqliteMapLayoutStore(
                            factory);

                    store.SaveDevice(
                        MapLayoutScope.PhysicalTopologyMapId,
                        new MapDeviceLayout(
                            deviceId,
                            10.0,
                            20.0,
                            false));

                    store.SaveDevice(
                        MapLayoutScope.PhysicalTopologyMapId,
                        new MapDeviceLayout(
                            deviceId,
                            50.0,
                            60.0,
                            true));

                    var saved =
                        store.Load(
                                MapLayoutScope.PhysicalTopologyMapId)
                            .Devices
                            .Single();

                    Assert.AreEqual(
                        50.0,
                        saved.X,
                        0.0001);

                    Assert.AreEqual(
                        60.0,
                        saved.Y,
                        0.0001);

                    Assert.IsTrue(
                        saved.IsLocked);

                    AssertThrows<SQLiteException>(
                        () =>
                            store.SaveDevice(
                                MapLayoutScope.PhysicalTopologyMapId,
                                new MapDeviceLayout(
                                    Guid.NewGuid(),
                                    1.0,
                                    2.0,
                                    false)));
                });
        }

        private static void AssertThrows<TException>(
            Action action)
            where TException : Exception
        {
            try
            {
                action();

                Assert.Fail(
                    "Expected exception: " +
                    typeof(TException).FullName);
            }
            catch (TException)
            {
            }
        }

        private static Guid SaveDevice(
            SqliteConnectionFactory factory)
        {
            var deviceId =
                Guid.NewGuid();

            var now =
                new DateTime(
                    2026,
                    9,
                    16,
                    18,
                    0,
                    0,
                    DateTimeKind.Utc);

            new SqliteMaterializedTopologyRepository(
                factory)
                .SaveDevice(
                    new TopologyDevice(
                        deviceId,
                        null,
                        "Layout test switch",
                        DeviceCategory.Unknown,
                        DeviceDiscoveryOrigin.Automatic,
                        MonitoringCapability.Unknown,
                        null,
                        null,
                        null,
                        false,
                        false,
                        now,
                        now,
                        now,
                        "Layout test switch",
                        null));

            return deviceId;
        }

        private static void WithDatabase(
            Action<SqliteConnectionFactory> action)
        {
            var path =
                Path.Combine(
                    Path.GetTempPath(),
                    "netloom-s36-" +
                    Guid.NewGuid().ToString("N") +
                    ".db");

            try
            {
                var factory =
                    new SqliteConnectionFactory(
                        path);

                new DatabaseInitializer(factory)
                    .Initialize();

                action(factory);
            }
            finally
            {
                SQLiteConnection.ClearAllPools();

                foreach (var suffix in
                    new[]
                    {
                        string.Empty,
                        "-wal",
                        "-shm"
                    })
                {
                    var candidate =
                        path + suffix;

                    if (File.Exists(candidate))
                    {
                        File.Delete(candidate);
                    }
                }
            }
        }
    }
}
