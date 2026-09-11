using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Observations.Stp;
using NetLoom.Application.Topology;
using NetLoom.Contracts.Alerts;
using NetLoom.Domain.Locations;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Stp;
using NetLoom.Domain.Topology;
using NetLoom.Topology.Alerts;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class
        MaterializedTopologyAlertSnapshotProviderTests
    {
        private static readonly DateTime Now =
            new DateTime(
                2026,
                9,
                8,
                16,
                0,
                0,
                DateTimeKind.Utc);

        [TestMethod]
        public void
            MissingStpEvidenceRemainsReadOnlyAndDoesNotCreateFalseAlert()
        {
            var repository =
                new ReadOnlyTopologyRepository(
                    new[]
                    {
                        new PhysicalLink(
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            null,
                            Guid.NewGuid(),
                            null,
                            PhysicalLinkStrength.Confirmed,
                            PhysicalLinkFreshness.Fresh,
                            null,
                            null,
                            "provider-test",
                            Now,
                            Now,
                            Now,
                            "sprint31b",
                            false,
                            false,
                            null)
                    },
                    new DeviceInterface[0]);

            var reader =
                new FixedLatestStpReader(
                    new BoundStpObservation[0]);

            var snapshot =
                new MaterializedTopologyAlertSnapshotProvider(
                    repository,
                    reader,
                    () => Now)
                    .GetSnapshot(
                        "cist");

            Assert.AreEqual(
                0,
                snapshot.Alerts.Count);

            Assert.IsTrue(
                reader.WasRead);

            Assert.IsTrue(
                repository.PhysicalLinksWereRead);

            Assert.IsTrue(
                repository.InterfacesWereRead);
        }

        [TestMethod]
        public void
            ReadSetPathDoesNotReadRepositories()
        {
            var repository =
                new ReadOnlyTopologyRepository(
                    new PhysicalLink[0],
                    new DeviceInterface[0]);

            var reader =
                new FixedLatestStpReader(
                    new BoundStpObservation[0]);

            var readSet =
                new MaterializedTopologyReadSet(
                    new TopologyDevice[0],
                    new DeviceInterface[0],
                    new PhysicalLink[0],
                    new PhysicalLinkEvidence[0],
                    new Location[0],
                    new BoundStpObservation[0]);

            var snapshot =
                new MaterializedTopologyAlertSnapshotProvider(
                    repository,
                    reader,
                    () => Now)
                    .GetSnapshot(
                        "cist",
                        readSet);

            Assert.AreEqual(
                0,
                snapshot.Alerts.Count);

            Assert.IsFalse(
                reader.WasRead);

            Assert.IsFalse(
                repository.PhysicalLinksWereRead);

            Assert.IsFalse(
                repository.InterfacesWereRead);
        }

        [TestMethod]
        public void
            AllForwardingTriangleCreatesCurrentCriticalCycleAlert()
        {
            var a =
                Guid.NewGuid();

            var b =
                Guid.NewGuid();

            var c =
                Guid.NewGuid();

            var a1 =
                Guid.NewGuid();

            var a2 =
                Guid.NewGuid();

            var b1 =
                Guid.NewGuid();

            var b2 =
                Guid.NewGuid();

            var c1 =
                Guid.NewGuid();

            var c2 =
                Guid.NewGuid();

            var links =
                new[]
                {
                    Link(
                        a,
                        a1,
                        b,
                        b1),
                    Link(
                        b,
                        b2,
                        c,
                        c1),
                    Link(
                        c,
                        c2,
                        a,
                        a2)
                };

            var interfaces =
                new[]
                {
                    Interface(
                        a1,
                        a,
                        1),
                    Interface(
                        a2,
                        a,
                        2),
                    Interface(
                        b1,
                        b,
                        1),
                    Interface(
                        b2,
                        b,
                        2),
                    Interface(
                        c1,
                        c,
                        1),
                    Interface(
                        c2,
                        c,
                        2)
                };

            var reader =
                new FixedLatestStpReader(
                    new[]
                    {
                        BoundObservation(
                            a,
                            "192.0.2.1"),
                        BoundObservation(
                            b,
                            "192.0.2.2"),
                        BoundObservation(
                            c,
                            "192.0.2.3")
                    });

            var snapshot =
                new MaterializedTopologyAlertSnapshotProvider(
                    new ReadOnlyTopologyRepository(
                        links,
                        interfaces),
                    reader,
                    () => Now)
                    .GetSnapshot(
                        "cist");

            Assert.AreEqual(
                1,
                snapshot.Alerts.Count);

            Assert.IsTrue(
                snapshot.HasCritical);

            var alert =
                snapshot.Alerts[0];

            Assert.AreEqual(
                TopologyAlertKind.ForwardingCycle,
                alert.Kind);

            Assert.AreEqual(
                TopologyAlertSeverity.Critical,
                alert.Severity);

            CollectionAssert.AreEqual(
                links
                    .Select(
                        link => link.Id)
                    .OrderBy(
                        id => id)
                    .ToArray(),
                alert.PhysicalLinkIds
                    .ToArray());
        }

        private static BoundStpObservation
            BoundObservation(
                Guid deviceId,
                string sourceAddress)
        {
            return new BoundStpObservation(
                deviceId,
                new StpObservation(
                    new Observation(
                        Guid.NewGuid(),
                        ObservationKind.Stp,
                        sourceAddress,
                        Now),
                    "cist",
                    null,
                    null,
                    null,
                    null,
                    null,
                    new[]
                    {
                        Port(
                            1,
                            1),
                        Port(
                            2,
                            2)
                    }));
        }

        private static StpPortState Port(
            int bridgePortIndex,
            int ifIndex)
        {
            return new StpPortState(
                bridgePortIndex,
                ifIndex,
                null,
                5,
                1,
                10,
                null,
                null,
                null,
                null,
                null);
        }

        private static DeviceInterface Interface(
            Guid id,
            Guid deviceId,
            int ifIndex)
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
                Now,
                Now);
        }

        private static PhysicalLink Link(
            Guid deviceAId,
            Guid interfaceAId,
            Guid deviceBId,
            Guid interfaceBId)
        {
            return new PhysicalLink(
                Guid.NewGuid(),
                deviceAId,
                interfaceAId,
                deviceBId,
                interfaceBId,
                PhysicalLinkStrength.Confirmed,
                PhysicalLinkFreshness.Fresh,
                null,
                null,
                "provider-test",
                Now,
                Now,
                Now,
                "sprint31b",
                false,
                false,
                null);
        }

        private sealed class FixedLatestStpReader :
            ILatestStpObservationReader
        {
            private readonly IReadOnlyList<
                BoundStpObservation> _items;

            public FixedLatestStpReader(
                IReadOnlyList<BoundStpObservation> items)
            {
                _items = items;
            }

            public bool WasRead
            {
                get;
                private set;
            }

            public IReadOnlyList<BoundStpObservation>
                GetLatest(
                    string instanceId)
            {
                WasRead = true;

                return _items;
            }
        }

        private sealed class ReadOnlyTopologyRepository :
            IMaterializedTopologyRepository
        {
            private readonly IReadOnlyList<PhysicalLink>
                _links;

            private readonly IReadOnlyList<DeviceInterface>
                _interfaces;

            public ReadOnlyTopologyRepository(
                IReadOnlyList<PhysicalLink> links,
                IReadOnlyList<DeviceInterface> interfaces)
            {
                _links = links;
                _interfaces = interfaces;
            }

            public bool PhysicalLinksWereRead
            {
                get;
                private set;
            }

            public bool InterfacesWereRead
            {
                get;
                private set;
            }

            public IReadOnlyList<PhysicalLink>
                GetPhysicalLinks()
            {
                PhysicalLinksWereRead = true;
                return _links;
            }

            public IReadOnlyList<DeviceInterface>
                GetInterfaces()
            {
                InterfacesWereRead = true;
                return _interfaces;
            }

            public IReadOnlyList<TopologyDevice>
                GetDevices()
            {
                return new TopologyDevice[0];
            }

            public IReadOnlyList<PhysicalLinkEvidence>
                GetPhysicalLinkEvidence()
            {
                return new PhysicalLinkEvidence[0];
            }

            public IReadOnlyList<PhysicalLinkEvidence>
                GetPhysicalLinkEvidence(
                    Guid physicalLinkId)
            {
                return new PhysicalLinkEvidence[0];
            }

            public TopologyDevice GetDevice(
                Guid id)
            {
                return null;
            }

            public void SaveDevice(
                TopologyDevice device)
            {
                throw new InvalidOperationException(
                    "Provider test must not write.");
            }

            public void SaveInterface(
                DeviceInterface networkInterface)
            {
                throw new InvalidOperationException(
                    "Provider test must not write.");
            }

            public PhysicalLink SavePhysicalLink(
                PhysicalLink link)
            {
                throw new InvalidOperationException(
                    "Provider test must not write.");
            }

            public void ReplacePhysicalLinkEvidence(
                Guid physicalLinkId,
                IEnumerable<PhysicalLinkEvidence> evidence)
            {
                throw new InvalidOperationException(
                    "Provider test must not write.");
            }

            public void DeleteManualPhysicalLink(
                Guid id)
            {
                throw new InvalidOperationException(
                    "Provider test must not write.");
            }

            public void DeleteManualInterface(
                Guid id)
            {
                throw new InvalidOperationException(
                    "Provider test must not write.");
            }

            public void DeleteManualDevice(
                Guid id)
            {
                throw new InvalidOperationException(
                    "Provider test must not write.");
            }
        }
    }
}
