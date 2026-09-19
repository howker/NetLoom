using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.MapLayout;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.MapLayout;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class MapLayoutPersistenceBehaviorTests
    {
        [TestMethod]
        public void DeviceAndViewportLayoutRoundTripSurvivesRestartAndUpdatesInPlace()
        {
            WithDatabase(
                factory =>
                {
                    var mapId =
                        MapLayoutScope.PhysicalTopologyMapId;

                    var deviceId =
                        Guid.NewGuid();

                    SaveDeviceFixture(
                        factory,
                        deviceId);

                    var first =
                        new SqliteMapLayoutStore(
                            factory);

                    first.SaveViewport(
                        mapId,
                        new MapViewportLayout(
                            1.75,
                            123.5,
                            -80.25));

                    first.SaveDevice(
                        mapId,
                        new MapDeviceLayout(
                            deviceId,
                            250.25,
                            -30.5,
                            true));

                    var restarted =
                        new SqliteMapLayoutStore(
                            factory);

                    var snapshot =
                        restarted.Load(
                            mapId);

                    Assert.AreEqual(
                        1.75,
                        snapshot.Viewport.Zoom,
                        0.0001);

                    Assert.AreEqual(
                        123.5,
                        snapshot.Viewport.PanX,
                        0.0001);

                    Assert.AreEqual(
                        -80.25,
                        snapshot.Viewport.PanY,
                        0.0001);

                    var saved =
                        snapshot.Devices
                            .Single(
                                item =>
                                    item.DeviceId ==
                                    deviceId);

                    Assert.AreEqual(
                        250.25,
                        saved.X,
                        0.0001);

                    Assert.AreEqual(
                        -30.5,
                        saved.Y,
                        0.0001);

                    Assert.IsTrue(
                        saved.IsLocked);

                    restarted.SaveViewport(
                        mapId,
                        new MapViewportLayout(
                            0.65,
                            -400.0,
                            900.0));

                    restarted.SaveDevice(
                        mapId,
                        new MapDeviceLayout(
                            deviceId,
                            -15.0,
                            42.0,
                            false));

                    snapshot =
                        new SqliteMapLayoutStore(
                            factory)
                            .Load(
                                mapId);

                    Assert.AreEqual(
                        0.65,
                        snapshot.Viewport.Zoom,
                        0.0001);

                    Assert.AreEqual(
                        -400.0,
                        snapshot.Viewport.PanX,
                        0.0001);

                    Assert.AreEqual(
                        900.0,
                        snapshot.Viewport.PanY,
                        0.0001);

                    Assert.AreEqual(
                        1,
                        snapshot.Devices.Count(
                            item =>
                                item.DeviceId ==
                                deviceId));

                    saved =
                        snapshot.Devices
                            .Single(
                                item =>
                                    item.DeviceId ==
                                    deviceId);

                    Assert.AreEqual(
                        -15.0,
                        saved.X,
                        0.0001);

                    Assert.AreEqual(
                        42.0,
                        saved.Y,
                        0.0001);

                    Assert.IsFalse(
                        saved.IsLocked);
                });
        }

        private static void SaveDeviceFixture(
            SqliteConnectionFactory factory,
            Guid deviceId)
        {
            using (var connection =
                factory.OpenConnection())
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
INSERT INTO devices
(
    id,
    created_at_utc,
    updated_at_utc
)
VALUES
(
    @id,
    @createdAtUtc,
    @updatedAtUtc
);";

                var now =
                    DateTime.UtcNow.ToString("O");

                command.Parameters.AddWithValue(
                    "@id",
                    deviceId.ToString());

                command.Parameters.AddWithValue(
                    "@createdAtUtc",
                    now);

                command.Parameters.AddWithValue(
                    "@updatedAtUtc",
                    now);

                command.ExecuteNonQuery();
            }
        }

        private static void WithDatabase(
            Action<SqliteConnectionFactory> action)
        {
            var path =
                Path.Combine(
                    Path.GetTempPath(),
                    "netloom-map-layout-behavior-" +
                    Guid.NewGuid().ToString("N") +
                    ".db");

            try
            {
                var factory =
                    new SqliteConnectionFactory(
                        path);

                new DatabaseInitializer(
                    factory)
                    .Initialize();

                action(
                    factory);
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

                    if (File.Exists(
                        candidate))
                    {
                        File.Delete(
                            candidate);
                    }
                }
            }
        }
    }
}
