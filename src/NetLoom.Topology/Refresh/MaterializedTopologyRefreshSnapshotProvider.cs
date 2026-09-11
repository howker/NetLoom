using System;
using NetLoom.Application.Topology;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Topology.Alerts;
using NetLoom.Topology.Map;

namespace NetLoom.Topology.Refresh
{
    public sealed class
        MaterializedTopologyRefreshSnapshotProvider :
        ITopologyRefreshSnapshotProvider
    {
        private readonly IMaterializedTopologyReadSetReader
            _readSetReader;

        private readonly MaterializedMapSnapshotProvider
            _mapSnapshotProvider;

        private readonly MaterializedTopologyAlertSnapshotProvider
            _alertSnapshotProvider;

        public MaterializedTopologyRefreshSnapshotProvider(
            IMaterializedTopologyReadSetReader readSetReader,
            MaterializedMapSnapshotProvider mapSnapshotProvider,
            MaterializedTopologyAlertSnapshotProvider alertSnapshotProvider)
        {
            _readSetReader =
                readSetReader ??
                throw new ArgumentNullException(
                    nameof(readSetReader));

            _mapSnapshotProvider =
                mapSnapshotProvider ??
                throw new ArgumentNullException(
                    nameof(mapSnapshotProvider));

            _alertSnapshotProvider =
                alertSnapshotProvider ??
                throw new ArgumentNullException(
                    nameof(alertSnapshotProvider));
        }

        public TopologyRefreshSnapshot GetSnapshot(
            string stpInstanceId)
        {
            if (string.IsNullOrWhiteSpace(
                stpInstanceId))
            {
                throw new ArgumentException(
                    "STP instance id is required.",
                    nameof(stpInstanceId));
            }

            var normalizedInstanceId =
                stpInstanceId.Trim();

            var readSet =
                _readSetReader.Read(
                    normalizedInstanceId);

            return new TopologyRefreshSnapshot(
                _mapSnapshotProvider.GetSnapshot(
                    readSet),
                _alertSnapshotProvider.GetSnapshot(
                    normalizedInstanceId,
                    readSet));
        }
    }
}
