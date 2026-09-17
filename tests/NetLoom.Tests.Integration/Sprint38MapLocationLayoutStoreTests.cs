using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.MapLayout;
using NetLoom.Domain.Locations;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Locations;
using NetLoom.Persistence.Sqlite.MapLayout;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint38MapLocationLayoutStoreTests
    {
        [TestMethod]
        public void Migration021CreatesLocationLayoutTable()
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
SELECT COUNT(*)
FROM schema_migrations
WHERE version = 21;";

                        Assert.AreEqual(
                            1L,
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
  AND name = 'map_location_layout';";

                        Assert.AreEqual(
                            1L,
                            Convert.ToInt64(
                                command.ExecuteScalar()));
                    }
                });
        }

        [TestMethod]
        public void LocationContainerLayoutSurvivesRestartAndUpdatesInPlace()
        {
            WithDatabase(
                factory =>
                {
                    var locationId =
                        SaveLocation(
                            factory);

                    var first =
                        new SqliteMapLayoutStore(
                            factory);

                    first.SaveLocation(
                        MapLayoutScope.PhysicalTopologyMapId,
                        new MapLocationLayout(
                            locationId,
                            100.5,
                            220.25,
                            640.0,
                            360.0,
                            true,
                            true));

                    var restarted =
                        new SqliteMapLayoutStore(
                            factory);

                    var saved =
                        restarted
                            .Load(
                                MapLayoutScope.PhysicalTopologyMapId)
                            .Locations
                            .Single();

                    Assert.AreEqual(
                        locationId,
                        saved.LocationId);

                    Assert.AreEqual(
                        100.5,
                        saved.X,
                        0.0001);

                    Assert.AreEqual(
                        220.25,
                        saved.Y,
                        0.0001);

                    Assert.AreEqual(
                        640.0,
                        saved.Width,
                        0.0001);

                    Assert.AreEqual(
                        360.0,
                        saved.Height,
                        0.0001);

                    Assert.IsTrue(
                        saved.IsCollapsed);

                    Assert.IsTrue(
                        saved.IsLocked);

                    restarted.SaveLocation(
                        MapLayoutScope.PhysicalTopologyMapId,
                        new MapLocationLayout(
                            locationId,
                            -50.0,
                            15.0,
                            480.0,
                            240.0,
                            false,
                            false));

                    saved =
                        new SqliteMapLayoutStore(
                            factory)
                            .Load(
                                MapLayoutScope.PhysicalTopologyMapId)
                            .Locations
                            .Single();

                    Assert.AreEqual(
                        -50.0,
                        saved.X,
                        0.0001);

                    Assert.AreEqual(
                        15.0,
                        saved.Y,
                        0.0001);

                    Assert.AreEqual(
                        480.0,
                        saved.Width,
                        0.0001);

                    Assert.AreEqual(
                        240.0,
                        saved.Height,
                        0.0001);

                    Assert.IsFalse(
                        saved.IsCollapsed);

                    Assert.IsFalse(
                        saved.IsLocked);
                });
        }

        [TestMethod]
        public void LocationLayoutRequiresRealLocationAndCascadesWhenLocationIsDeleted()
        {
            WithDatabase(
                factory =>
                {
                    var store =
                        new SqliteMapLayoutStore(
                            factory);

                    AssertSqliteFailure(
                        () =>
                            store.SaveLocation(
                                MapLayoutScope.PhysicalTopologyMapId,
                                new MapLocationLayout(
                                    Guid.NewGuid(),
                                    0.0,
                                    0.0,
                                    300.0,
                                    200.0,
                                    false,
                                    false)));

                    var locationId =
                        SaveLocation(
                            factory);

                    store.SaveLocation(
                        MapLayoutScope.PhysicalTopologyMapId,
                        new MapLocationLayout(
                            locationId,
                            10.0,
                            20.0,
                            300.0,
                            200.0,
                            false,
                            false));

                    new SqliteLocationRepository(
                        factory)
                        .Delete(
                            locationId);

                    Assert.AreEqual(
                        0,
                        store.Load(
                                MapLayoutScope.PhysicalTopologyMapId)
                            .Locations.Count);
                });
        }

        private static Guid SaveLocation(
            SqliteConnectionFactory factory)
        {
            var id =
                Guid.NewGuid();

            new SqliteLocationRepository(
                factory)
                .Save(
                    new Location(
                        id,
                        null,
                        "Plant room",
                        null));

            return id;
        }

        private static void AssertSqliteFailure(
            Action action)
        {
            try
            {
                action();

                Assert.Fail(
                    "SQLiteException was expected.");
            }
            catch (SQLiteException)
            {
            }
        }

        private static void WithDatabase(
            Action<SqliteConnectionFactory> action)
        {
            var path =
                Path.Combine(
                    Path.GetTempPath(),
                    "netloom-s38-location-layout-" +
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
