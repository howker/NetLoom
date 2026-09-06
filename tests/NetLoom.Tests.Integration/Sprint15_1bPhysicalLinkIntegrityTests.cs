using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Topology;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint15_1bPhysicalLinkIntegrityTests
    {
        private static readonly DateTime T1 =
            new DateTime(
                2026, 1, 1, 10, 0, 0,
                DateTimeKind.Utc);

        private static readonly DateTime T2 =
            T1.AddMinutes(5);

        private static readonly DateTime T3 =
            T1.AddMinutes(10);

        [TestMethod]
        public void ProvisionalReverseRediscoveryAndRefinementPreserveIdentity()
        {
            WithRepository(
                repository =>
                {
                    var a = CreateDevice(Guid.NewGuid(), T1);
                    var b = CreateDevice(Guid.NewGuid(), T1);

                    repository.SaveDevice(a);
                    repository.SaveDevice(b);

                    var portA =
                        CreateInterface(
                            Guid.NewGuid(),
                            a.Id,
                            1,
                            T1);

                    var portB =
                        CreateInterface(
                            Guid.NewGuid(),
                            b.Id,
                            2,
                            T1);

                    repository.SaveInterface(portA);
                    repository.SaveInterface(portB);

                    var provisional =
                        CreateLink(
                            Guid.NewGuid(),
                            a.Id,
                            portA.Id,
                            b.Id,
                            null,
                            T1,
                            T1,
                            T1);

                    var first =
                        repository.SavePhysicalLink(
                            provisional);

                    var reverse =
                        CreateLink(
                            Guid.NewGuid(),
                            b.Id,
                            null,
                            a.Id,
                            portA.Id,
                            T1,
                            T2,
                            T2);

                    Assert.AreEqual(
                        provisional.LinkKey,
                        reverse.LinkKey);

                    var second =
                        repository.SavePhysicalLink(
                            reverse);

                    Assert.AreEqual(
                        first.Id,
                        second.Id);

                    var refined =
                        CreateLink(
                            Guid.NewGuid(),
                            b.Id,
                            portB.Id,
                            a.Id,
                            portA.Id,
                            T2,
                            T3,
                            T3);

                    var third =
                        repository.SavePhysicalLink(
                            refined);

                    Assert.AreEqual(
                        first.Id,
                        third.Id);

                    Assert.AreEqual(
                        T1,
                        third.FirstSeenUtc);

                    Assert.AreEqual(
                        T3,
                        third.LastSeenUtc);

                    Assert.AreEqual(
                        T3,
                        third.LastConfirmedUtc.Value);
                    var thirdPortA =
                        third.DeviceAId == a.Id
                            ? third.InterfaceAId
                            : third.InterfaceBId;

                    var thirdPortB =
                        third.DeviceAId == b.Id
                            ? third.InterfaceAId
                            : third.InterfaceBId;

                    Assert.IsTrue(thirdPortA.HasValue);
                    Assert.IsTrue(thirdPortB.HasValue);

                    Assert.AreEqual(
                        portA.Id,
                        thirdPortA.Value);

                    Assert.AreEqual(
                        portB.Id,
                        thirdPortB.Value);
var stored =
                        repository.GetPhysicalLinks();

                    Assert.AreEqual(1, stored.Count);
                    Assert.AreEqual(first.Id, stored[0].Id);
                    Assert.AreEqual(T1, stored[0].FirstSeenUtc);
                    Assert.AreEqual(T3, stored[0].LastSeenUtc);
                });
        }

        [TestMethod]
        public void ParallelLinksRemainDistinct()
        {
            WithRepository(
                repository =>
                {
                    var a = CreateDevice(Guid.NewGuid(), T1);
                    var b = CreateDevice(Guid.NewGuid(), T1);

                    repository.SaveDevice(a);
                    repository.SaveDevice(b);

                    var portA1 =
                        CreateInterface(Guid.NewGuid(), a.Id, 1, T1);

                    var portA2 =
                        CreateInterface(Guid.NewGuid(), a.Id, 2, T1);

                    repository.SaveInterface(portA1);
                    repository.SaveInterface(portA2);

                    repository.SavePhysicalLink(
                        CreateLink(
                            Guid.NewGuid(),
                            a.Id,
                            portA1.Id,
                            b.Id,
                            null,
                            T1,
                            T1,
                            T1));

                    repository.SavePhysicalLink(
                        CreateLink(
                            Guid.NewGuid(),
                            a.Id,
                            portA2.Id,
                            b.Id,
                            null,
                            T1,
                            T1,
                            T1));

                    var links =
                        repository.GetPhysicalLinks();

                    Assert.AreEqual(2, links.Count);
                    Assert.AreNotEqual(
                        links[0].LinkKey,
                        links[1].LinkKey);
                });
        }

        [TestMethod]
        public void AmbiguousRefinementDoesNotGuess()
        {
            WithRepository(
                repository =>
                {
                    var a = CreateDevice(Guid.NewGuid(), T1);
                    var b = CreateDevice(Guid.NewGuid(), T1);

                    repository.SaveDevice(a);
                    repository.SaveDevice(b);

                    var portA1 =
                        CreateInterface(Guid.NewGuid(), a.Id, 1, T1);

                    var portA2 =
                        CreateInterface(Guid.NewGuid(), a.Id, 2, T1);

                    var portB =
                        CreateInterface(Guid.NewGuid(), b.Id, 1, T1);

                    repository.SaveInterface(portA1);
                    repository.SaveInterface(portA2);
                    repository.SaveInterface(portB);

                    repository.SavePhysicalLink(
                        CreateLink(
                            Guid.NewGuid(),
                            a.Id,
                            portA1.Id,
                            b.Id,
                            null,
                            T1,
                            T1,
                            T1));

                    repository.SavePhysicalLink(
                        CreateLink(
                            Guid.NewGuid(),
                            a.Id,
                            portA2.Id,
                            b.Id,
                            null,
                            T1,
                            T1,
                            T1));

                    AssertInvalidOperation(
                        () =>
                            repository.SavePhysicalLink(
                                CreateLink(
                                    Guid.NewGuid(),
                                    a.Id,
                                    null,
                                    b.Id,
                                    portB.Id,
                                    T2,
                                    T2,
                                    T2)));

                    Assert.AreEqual(
                        2,
                        repository.GetPhysicalLinks().Count);
                });
        }
        [TestMethod]
        public void DeviceAndInterfaceTimestampsAreMonotonic()
        {
            WithRepository(
                repository =>
                {
                    var deviceId = Guid.NewGuid();

                    repository.SaveDevice(
                        CreateDevice(
                            deviceId,
                            T2));

                    repository.SaveDevice(
                        CreateDevice(
                            deviceId,
                            T1));

                    var device =
                        repository.GetDevice(deviceId);

                    Assert.AreEqual(
                        T1,
                        device.FirstSeenUtc.Value);

                    Assert.AreEqual(
                        T2,
                        device.LastSeenUtc.Value);

                    Assert.AreEqual(
                        T2,
                        device.LastResolvedUtc.Value);

                    var interfaceId = Guid.NewGuid();

                    repository.SaveInterface(
                        CreateInterface(
                            interfaceId,
                            deviceId,
                            1,
                            T2));

                    repository.SaveInterface(
                        CreateInterface(
                            interfaceId,
                            deviceId,
                            1,
                            T1));

                    DeviceInterface stored = null;

                    foreach (var item in repository.GetInterfaces())
                    {
                        if (item.Id == interfaceId)
                        {
                            stored = item;
                            break;
                        }
                    }

                    Assert.IsNotNull(stored);
                    Assert.AreEqual(T1, stored.FirstSeenUtc.Value);
                    Assert.AreEqual(T2, stored.LastSeenUtc.Value);
                });
        }

        private static TopologyDevice CreateDevice(
            Guid id,
            DateTime observedUtc)
        {
            return new TopologyDevice(
                id,
                null,
                null,
                DeviceCategory.Unknown,
                DeviceDiscoveryOrigin.Automatic,
                MonitoringCapability.Unknown,
                null,
                null,
                null,
                false,
                false,
                observedUtc,
                observedUtc,
                observedUtc);
        }

        private static DeviceInterface CreateInterface(
            Guid id,
            Guid deviceId,
            int ifIndex,
            DateTime observedUtc)
        {
            return new DeviceInterface(
                id,
                deviceId,
                ifIndex,
                "if" + ifIndex,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                false,
                observedUtc,
                observedUtc);
        }

        private static PhysicalLink CreateLink(
            Guid id,
            Guid deviceAId,
            Guid? interfaceAId,
            Guid deviceBId,
            Guid? interfaceBId,
            DateTime firstSeenUtc,
            DateTime lastSeenUtc,
            DateTime? lastConfirmedUtc)
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
                "test",
                firstSeenUtc,
                lastSeenUtc,
                lastConfirmedUtc,
                "15.1b-test",
                false,
                false,
                null);
        }

        private static void WithRepository(
            Action<SqliteMaterializedTopologyRepository> action)
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
                            "topology.db"));

                new DatabaseInitializer(factory).Initialize();

                action(
                    new SqliteMaterializedTopologyRepository(
                        factory));
            }
            finally
            {
                System.Data.SQLite.SQLiteConnection.ClearAllPools();

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
