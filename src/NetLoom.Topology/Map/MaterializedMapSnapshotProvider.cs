using System;
using NetLoom.Application.Locations;
using NetLoom.Application.Topology;
using NetLoom.Application.TopologyMap;
using NetLoom.Contracts.TopologyMap;

namespace NetLoom.Topology.Map
{
    public sealed class MaterializedMapSnapshotProvider :
        IMapSnapshotProvider
    {
        private readonly IMaterializedTopologyRepository
            _topologyRepository;

        private readonly ILocationRepository
            _locationRepository;

        private readonly MaterializedTopologyMapProjector
            _projector;

        private readonly Func<DateTime> _utcNow;

        public MaterializedMapSnapshotProvider(
            IMaterializedTopologyRepository topologyRepository,
            ILocationRepository locationRepository,
            MaterializedTopologyMapProjector projector,
            Func<DateTime> utcNow = null)
        {
            _topologyRepository =
                topologyRepository ??
                throw new ArgumentNullException(
                    nameof(topologyRepository));

            _locationRepository =
                locationRepository ??
                throw new ArgumentNullException(
                    nameof(locationRepository));

            _projector =
                projector ??
                throw new ArgumentNullException(
                    nameof(projector));

            _utcNow =
                utcNow ??
                (() => DateTime.UtcNow);
        }

        public MapSnapshot GetSnapshot()
        {
            var now = GetUtcNow();

            return _projector.Project(
                _topologyRepository.GetDevices(),
                _topologyRepository.GetInterfaces(),
                _topologyRepository.GetPhysicalLinks(),
                _topologyRepository.GetPhysicalLinkEvidence(),
                _locationRepository.GetAll(),
                now);
        }

        public MapSnapshot GetSnapshot(
            MaterializedTopologyReadSet readSet)
        {
            if (readSet == null)
            {
                throw new ArgumentNullException(
                    nameof(readSet));
            }

            var now = GetUtcNow();

            return _projector.Project(
                readSet.Devices,
                readSet.Interfaces,
                readSet.PhysicalLinks,
                readSet.PhysicalLinkEvidence,
                readSet.Locations,
                now);
        }

        private DateTime GetUtcNow()
        {
            var now = _utcNow();

            if (now.Kind != DateTimeKind.Utc)
            {
                throw new InvalidOperationException(
                    "Map snapshot clock must return UTC.");
            }

            return now;
        }
    }
}
