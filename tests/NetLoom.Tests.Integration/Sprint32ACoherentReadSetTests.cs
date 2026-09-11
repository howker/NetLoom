using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Topology;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint32ACoherentReadSetTests
    {
        private string _databasePath;

        [TestInitialize]
        public void Initialize()
        {
            _databasePath =
                Path.Combine(
                    Path.GetTempPath(),
                    "NetLoom.Sprint32A.ReadSet." +
                    Guid.NewGuid().ToString("N") +
                    ".sqlite");
        }

        [TestCleanup]
        public void Cleanup()
        {
            SQLiteConnection.ClearAllPools();

            DeleteIfExists(_databasePath);
            DeleteIfExists(_databasePath + "-wal");
            DeleteIfExists(_databasePath + "-shm");
        }

        [TestMethod]
        public void
            ReadSetKeepsOneSnapshotAcrossConcurrentWriterCommit()
        {
            var factory =
                new SqliteConnectionFactory(
                    _databasePath);

            new DatabaseInitializer(factory)
                .Initialize();

            var writerRepository =
                new SqliteMaterializedTopologyRepository(
                    factory);

            var manual =
                new ManualTopologyFactory();

            var newDevice =
                manual.CreateDevice(
                    Guid.NewGuid(),
                    null,
                    "Writer device",
                    DeviceCategory.UnmanagedSwitch,
                    null);

            var newInterface =
                manual.CreateInterface(
                    Guid.NewGuid(),
                    newDevice.Id,
                    "Writer port",
                    "Ethernet");

            using (var allowWriter =
                new ManualResetEventSlim(false))
            using (var writerCommitted =
                new ManualResetEventSlim(false))
            {
                var writer =
                    Task.Run(
                        () =>
                        {
                            allowWriter.Wait();

                            writerRepository.SaveDevice(
                                newDevice);

                            writerRepository.SaveInterface(
                                newInterface);

                            writerCommitted.Set();
                        });

                var reader =
                    new SqliteMaterializedTopologyReadSetReader(
                        factory,
                        () =>
                        {
                            allowWriter.Set();

                            Assert.IsTrue(
                                writerCommitted.Wait(
                                    TimeSpan.FromSeconds(5)),
                                "Concurrent writer did not commit " +
                                "while the read transaction was open.");
                        });

                var readSet =
                    reader.Read("cist");

                writer.GetAwaiter().GetResult();

                var deviceIds =
                    new HashSet<Guid>();

                foreach (var device in readSet.Devices)
                {
                    deviceIds.Add(device.Id);
                }

                foreach (var networkInterface in readSet.Interfaces)
                {
                    Assert.IsTrue(
                        deviceIds.Contains(
                            networkInterface.DeviceId),
                        "A coherent topology read-set must not contain " +
                        "an interface whose owning device was committed " +
                        "after the device portion of the same read-set.");
                }

                Assert.IsFalse(
                    deviceIds.Contains(newDevice.Id),
                    "The writer device must remain outside the reader's " +
                    "already-established SQLite snapshot.");

                Assert.AreEqual(
                    0,
                    readSet.Interfaces.Count,
                    "The interface committed after the reader snapshot " +
                    "must remain outside that same read-set.");
            }
        }

        private static void DeleteIfExists(
            string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
