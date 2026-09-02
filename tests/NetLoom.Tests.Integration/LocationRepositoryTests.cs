using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Domain.Locations;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Locations;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class LocationRepositoryTests
    {
        [TestMethod]
        public void HierarchyRoundTripRenameMoveAndDeleteRulesWork()
        {
            var directory =
                Path.Combine(
                    Path.GetTempPath(),
                    "NetLoom.Tests",
                    Guid.NewGuid().ToString("N"));

            var databasePath =
                Path.Combine(
                    directory,
                    "locations.db");

            try
            {
                var factory =
                    new SqliteConnectionFactory(
                        databasePath);

                new DatabaseInitializer(factory)
                    .Initialize();

                var repository =
                    new SqliteLocationRepository(
                        factory);

                var siteId = Guid.NewGuid();
                var floorId = Guid.NewGuid();
                var rackId = Guid.NewGuid();

                repository.Save(
                    new Location(
                        siteId,
                        null,
                        "лощадка ",
                        null));

                repository.Save(
                    new Location(
                        floorId,
                        siteId,
                        "таж 2",
                        null));

                repository.Save(
                    new Location(
                        rackId,
                        floorId,
                        "Шкаф 17",
                        "Серверная"));

                var rack =
                    repository.Get(rackId);

                Assert.IsNotNull(rack);
                Assert.AreEqual(
                    rackId,
                    rack.Id);
                Assert.AreEqual(
                    floorId,
                    rack.ParentLocationId);
                Assert.AreEqual(
                    "Шкаф 17",
                    rack.Name);

                repository.Save(
                    new Location(
                        rackId,
                        siteId,
                        "Шкаф ядра",
                        "еремещён"));

                rack = repository.Get(rackId);

                Assert.AreEqual(
                    rackId,
                    rack.Id);
                Assert.AreEqual(
                    siteId,
                    rack.ParentLocationId);
                Assert.AreEqual(
                    "Шкаф ядра",
                    rack.Name);

                Assert.AreEqual(
                    3,
                    repository.GetAll().Count);

                AssertInvalidOperation(
                    () => repository.Save(
                        new Location(
                            siteId,
                            rackId,
                            "лощадка ",
                            null)));

                repository.Save(
                    new Location(
                        rackId,
                        floorId,
                        "Шкаф ядра",
                        null));

                AssertInvalidOperation(
                    () => repository.Delete(
                        floorId));

                repository.Delete(rackId);
                repository.Delete(floorId);

                Assert.IsNull(
                    repository.Get(rackId));

                Assert.AreEqual(
                    1,
                    repository.GetAll().Count);
            }
            finally
            {
                System.Data.SQLite.SQLiteConnection
                    .ClearAllPools();

                if (Directory.Exists(directory))
                {
                    Directory.Delete(
                        directory,
                        true);
                }
            }
        }

        [TestMethod]
        public void MissingParentIsRejected()
        {
            var directory =
                Path.Combine(
                    Path.GetTempPath(),
                    "NetLoom.Tests",
                    Guid.NewGuid().ToString("N"));

            try
            {
                var factory =
                    new SqliteConnectionFactory(
                        Path.Combine(
                            directory,
                            "locations.db"));

                new DatabaseInitializer(factory)
                    .Initialize();

                var repository =
                    new SqliteLocationRepository(
                        factory);

                AssertInvalidOperation(
                    () => repository.Save(
                        new Location(
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            "едопустимая площадка",
                            null)));
            }
            finally
            {
                System.Data.SQLite.SQLiteConnection
                    .ClearAllPools();

                if (Directory.Exists(directory))
                {
                    Directory.Delete(
                        directory,
                        true);
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
