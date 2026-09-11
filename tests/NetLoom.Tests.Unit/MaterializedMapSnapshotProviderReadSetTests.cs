using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Locations;
using NetLoom.Application.Observations.Stp;
using NetLoom.Application.Topology;
using NetLoom.Domain.Locations;
using NetLoom.Domain.Topology;
using NetLoom.Topology.Map;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class
        MaterializedMapSnapshotProviderReadSetTests
    {
        private static readonly DateTime Now =
            new DateTime(
                2026,
                9,
                11,
                12,
                0,
                0,
                DateTimeKind.Utc);

        [TestMethod]
        public void
            ReadSetPathDoesNotReadRepositories()
        {
            var provider =
                new MaterializedMapSnapshotProvider(
                    new ThrowingTopologyRepository(),
                    new ThrowingLocationRepository(),
                    new MaterializedTopologyMapProjector(),
                    () => Now);

            var readSet =
                new MaterializedTopologyReadSet(
                    new TopologyDevice[0],
                    new DeviceInterface[0],
                    new PhysicalLink[0],
                    new PhysicalLinkEvidence[0],
                    new Location[0],
                    new BoundStpObservation[0]);

            var snapshot =
                provider.GetSnapshot(
                    readSet);

            Assert.IsNotNull(
                snapshot);
        }

        private sealed class ThrowingLocationRepository :
            ILocationRepository
        {
            public void Save(
                Location location)
            {
                throw UnexpectedRead();
            }

            public Location Get(
                Guid id)
            {
                throw UnexpectedRead();
            }

            public IReadOnlyList<Location> GetAll()
            {
                throw UnexpectedRead();
            }

            public void Delete(
                Guid id)
            {
                throw UnexpectedRead();
            }
        }

        private sealed class ThrowingTopologyRepository :
            IMaterializedTopologyRepository
        {
            public void SaveDevice(
                TopologyDevice device)
            {
                throw UnexpectedRead();
            }

            public void SaveInterface(
                DeviceInterface networkInterface)
            {
                throw UnexpectedRead();
            }

            public PhysicalLink SavePhysicalLink(
                PhysicalLink link)
            {
                throw UnexpectedRead();
            }

            public TopologyDevice GetDevice(
                Guid id)
            {
                throw UnexpectedRead();
            }

            public IReadOnlyList<TopologyDevice>
                GetDevices()
            {
                throw UnexpectedRead();
            }

            public IReadOnlyList<DeviceInterface>
                GetInterfaces()
            {
                throw UnexpectedRead();
            }

            public IReadOnlyList<PhysicalLink>
                GetPhysicalLinks()
            {
                throw UnexpectedRead();
            }

            public void ReplacePhysicalLinkEvidence(
                Guid physicalLinkId,
                IEnumerable<PhysicalLinkEvidence> evidence)
            {
                throw UnexpectedRead();
            }

            public IReadOnlyList<PhysicalLinkEvidence>
                GetPhysicalLinkEvidence()
            {
                throw UnexpectedRead();
            }

            public IReadOnlyList<PhysicalLinkEvidence>
                GetPhysicalLinkEvidence(
                    Guid physicalLinkId)
            {
                throw UnexpectedRead();
            }

            public void DeleteManualPhysicalLink(
                Guid id)
            {
                throw UnexpectedRead();
            }

            public void DeleteManualInterface(
                Guid id)
            {
                throw UnexpectedRead();
            }

            public void DeleteManualDevice(
                Guid id)
            {
                throw UnexpectedRead();
            }
        }

        private static InvalidOperationException
            UnexpectedRead()
        {
            return new InvalidOperationException(
                "Read-set projection must not read repositories.");
        }
    }
}
