using System;
using NetLoom.Application.MapLayout;
using NetLoom.Application.TopologyRefresh;

namespace NetLoom.Application.Export
{
    public sealed class TopologyExportSnapshotProvider :
        ITopologyExportSnapshotProvider
    {
        private readonly ITopologyRefreshSnapshotProvider
            _topologyRefreshSnapshotProvider;

        private readonly IMapLayoutStore
            _mapLayoutStore;

        private readonly Guid
            _mapId;

        private readonly string
            _stpInstanceId;

        public TopologyExportSnapshotProvider(
            ITopologyRefreshSnapshotProvider topologyRefreshSnapshotProvider,
            IMapLayoutStore mapLayoutStore,
            Guid mapId,
            string stpInstanceId)
        {
            _topologyRefreshSnapshotProvider =
                topologyRefreshSnapshotProvider ??
                throw new ArgumentNullException(
                    nameof(topologyRefreshSnapshotProvider));

            _mapLayoutStore =
                mapLayoutStore ??
                throw new ArgumentNullException(
                    nameof(mapLayoutStore));

            if (mapId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Map id is required.",
                    nameof(mapId));
            }

            if (string.IsNullOrWhiteSpace(
                stpInstanceId))
            {
                throw new ArgumentException(
                    "STP instance id is required.",
                    nameof(stpInstanceId));
            }

            _mapId = mapId;
            _stpInstanceId =
                stpInstanceId.Trim();
        }

        public TopologyExportSnapshot
            GetSnapshot()
        {
            var topology =
                _topologyRefreshSnapshotProvider
                    .GetSnapshot(
                        _stpInstanceId);

            if (topology == null)
            {
                throw new InvalidOperationException(
                    "Topology refresh provider returned no snapshot.");
            }

            var persistedLayout =
                _mapLayoutStore.Load(
                    _mapId);

            return new TopologyExportSnapshot(
                topology,
                TopologyExportLayoutSnapshot
                    .FromPersisted(
                        _mapId,
                        persistedLayout));
        }
    }
}
