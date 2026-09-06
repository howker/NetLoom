using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Domain.Locations;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Topology;
using NetLoom.Topology.Map;
using NetLoom.Topology.Resolution;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint15_1cPhysicalLinkEvidenceTests
    {
        private static readonly DateTime T1 =
            new DateTime(
                2026, 1, 1, 10, 0, 0,
                DateTimeKind.Utc);

        private static readonly DateTime T2 = T1.AddMinutes(5);
        private static readonly DateTime T3 = T1.AddMinutes(10);
        private static readonly DateTime T4 = T1.AddMinutes(15);

        [TestMethod]
        public void ProvisionalReverseRefinementNewerPollAndEvidenceReadback()
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
                        repository.SavePhysicalLink(
                            CreateLink(
                                Guid.NewGuid(),
                                a.Id,
                                portA.Id,
                                b.Id,
                                null,
                                T1));

                    var materializer =
                        new PhysicalLinkEvidenceMaterializer();

                    repository.ReplacePhysicalLinkEvidence(
                        provisional.Id,
                        materializer.Materialize(
                            provisional.Id,
                            new[]
                            {
                                Evidence(
                                    TopologyEvidenceKind.Lldp,
                                    TopologyEvidenceStrength.Strong,
                                    Guid.NewGuid(),
                                    T1,
                                    "192.0.2.10",
                                    "lldp-local:index:1",
                                    "LLDP adjacency")
                            }));

                    var reverse =
                        repository.SavePhysicalLink(
                            CreateLink(
                                Guid.NewGuid(),
                                b.Id,
                                null,
                                a.Id,
                                portA.Id,
                                T2));

                    Assert.AreEqual(
                        provisional.Id,
                        reverse.Id);

                    var reverseObservation =
                        Guid.NewGuid();

                    repository.ReplacePhysicalLinkEvidence(
                        reverse.Id,
                        materializer.Materialize(
                            reverse.Id,
                            new[]
                            {
                                Evidence(
                                    TopologyEvidenceKind.Lldp,
                                    TopologyEvidenceStrength.Strong,
                                    Guid.NewGuid(),
                                    T1,
                                    "192.0.2.10",
                                    "lldp-local:index:1",
                                    "LLDP adjacency"),
                                Evidence(
                                    TopologyEvidenceKind.Lldp,
                                    TopologyEvidenceStrength.Strong,
                                    reverseObservation,
                                    T2,
                                    "192.0.2.20",
                                    "lldp-local:index:2",
                                    "LLDP adjacency")
                            }));

                    Assert.AreEqual(
                        2,
                        repository
                            .GetPhysicalLinkEvidence(reverse.Id)
                            .Count);

                    var refined =
                        repository.SavePhysicalLink(
                            CreateLink(
                                Guid.NewGuid(),
                                b.Id,
                                portB.Id,
                                a.Id,
                                portA.Id,
                                T3));

                    Assert.AreEqual(
                        provisional.Id,
                        refined.Id);

                    var newestA = Guid.NewGuid();
                    var newestB = Guid.NewGuid();

                    var newer =
                        repository.SavePhysicalLink(
                            CreateLink(
                                Guid.NewGuid(),
                                a.Id,
                                portA.Id,
                                b.Id,
                                portB.Id,
                                T4));

                    Assert.AreEqual(
                        provisional.Id,
                        newer.Id);

                    repository.ReplacePhysicalLinkEvidence(
                        newer.Id,
                        materializer.Materialize(
                            newer.Id,
                            new[]
                            {
                                Evidence(
                                    TopologyEvidenceKind.Lldp,
                                    TopologyEvidenceStrength.Strong,
                                    Guid.NewGuid(),
                                    T3,
                                    "192.0.2.10",
                                    "lldp-local:index:1",
                                    "older duplicate"),
                                Evidence(
                                    TopologyEvidenceKind.Lldp,
                                    TopologyEvidenceStrength.Strong,
                                    newestA,
                                    T4,
                                    "192.0.2.10",
                                    "lldp-local:index:1",
                                    "newer duplicate wins"),
                                Evidence(
                                    TopologyEvidenceKind.Lldp,
                                    TopologyEvidenceStrength.Strong,
                                    newestB,
                                    T4,
                                    "192.0.2.20",
                                    "lldp-local:index:2",
                                    "LLDP adjacency"),
                                Evidence(
                                    TopologyEvidenceKind.ArpFdbCorrelation,
                                    TopologyEvidenceStrength.Weak,
                                    null,
                                    null,
                                    "192.0.2.10",
                                    "mac:001122334455",
                                    "ARP/FDB MAC correlation")
                            }));

                    var current =
                        repository
                            .GetPhysicalLinkEvidence(newer.Id);

                    Assert.AreEqual(
                        3,
                        current.Count);

                    Assert.AreEqual(
                        newestA,
                        current.Single(
                            item =>
                                item.SourceAddress == "192.0.2.10" &&
                                item.SlotDiscriminator ==
                                "lldp-local:index:1")
                            .ObservationId.Value);

                    Assert.AreEqual(
                        newestB,
                        current.Single(
                            item =>
                                item.SourceAddress == "192.0.2.20")
                            .ObservationId.Value);

                    Assert.AreEqual(
                        PhysicalLinkEvidenceStrength.Weak,
                        current.Single(
                            item =>
                                item.Kind ==
                                PhysicalLinkEvidenceKind
                                    .ArpFdbCorrelation)
                            .Strength);

                    var map =
                        new MaterializedTopologyMapProjector()
                            .Project(
                                repository.GetDevices(),
                                repository.GetInterfaces(),
                                repository.GetPhysicalLinks(),
                                repository.GetPhysicalLinkEvidence(),
                                new Location[0],
                                T4);

                    Assert.AreEqual(1, map.Links.Count);
                    Assert.AreEqual(3, map.Links[0].Evidence.Count);

                    Assert.IsTrue(
                        map.Links[0].Evidence.Any(
                            item =>
                                item.Kind ==
                                MapEvidenceKind.Lldp));

                    Assert.IsTrue(
                        map.Links[0].Evidence.Any(
                            item =>
                                item.Kind ==
                                MapEvidenceKind
                                    .ArpFdbCorrelation));
                });
        }

        [TestMethod]
        public void EmptyCurrentSnapshotRemovesPreviousEvidence()
        {
            WithRepository(
                repository =>
                {
                    var a = CreateDevice(Guid.NewGuid(), T1);
                    var b = CreateDevice(Guid.NewGuid(), T1);

                    repository.SaveDevice(a);
                    repository.SaveDevice(b);

                    var link =
                        repository.SavePhysicalLink(
                            CreateLink(
                                Guid.NewGuid(),
                                a.Id,
                                null,
                                b.Id,
                                null,
                                T1));

                    repository.ReplacePhysicalLinkEvidence(
                        link.Id,
                        new[]
                        {
                            new PhysicalLinkEvidence(
                                link.Id,
                                PhysicalLinkEvidenceKind.Cdp,
                                PhysicalLinkEvidenceStrength.Strong,
                                "192.0.2.10",
                                "cdp-local:unknown",
                                Guid.NewGuid(),
                                T1,
                                "CDP adjacency")
                        });

                    Assert.AreEqual(
                        1,
                        repository
                            .GetPhysicalLinkEvidence(link.Id)
                            .Count);

                    repository.ReplacePhysicalLinkEvidence(
                        link.Id,
                        new PhysicalLinkEvidence[0]);

                    Assert.AreEqual(
                        0,
                        repository
                            .GetPhysicalLinkEvidence(link.Id)
                            .Count);
                });
        }

        private static TopologyEvidence Evidence(
            TopologyEvidenceKind kind,
            TopologyEvidenceStrength strength,
            Guid? observationId,
            DateTime? capturedUtc,
            string sourceAddress,
            string slotDiscriminator,
            string detail)
        {
            return new TopologyEvidence(
                kind,
                strength,
                observationId,
                capturedUtc,
                sourceAddress,
                slotDiscriminator,
                detail);
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
                "test",
                T1,
                observedUtc,
                observedUtc,
                "15.1c-test",
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
    }
}
