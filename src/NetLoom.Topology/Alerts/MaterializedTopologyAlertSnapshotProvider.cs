using System;
using System.Linq;
using NetLoom.Application.Alerts;
using NetLoom.Application.Observations.Stp;
using NetLoom.Application.Topology;
using NetLoom.Contracts.Alerts;
using NetLoom.Topology.Rings;
using NetLoom.Topology.Safety;
using NetLoom.Topology.Stp;

namespace NetLoom.Topology.Alerts
{
    public sealed class
        MaterializedTopologyAlertSnapshotProvider :
        ITopologyAlertSnapshotProvider
    {
        private readonly IMaterializedTopologyRepository
            _topologyRepository;

        private readonly ILatestStpObservationReader
            _stpObservationReader;

        private readonly Func<DateTime> _utcNow;

        public MaterializedTopologyAlertSnapshotProvider(
            IMaterializedTopologyRepository topologyRepository,
            ILatestStpObservationReader stpObservationReader,
            Func<DateTime> utcNow = null)
        {
            _topologyRepository =
                topologyRepository ??
                throw new ArgumentNullException(
                    nameof(topologyRepository));

            _stpObservationReader =
                stpObservationReader ??
                throw new ArgumentNullException(
                    nameof(stpObservationReader));

            _utcNow =
                utcNow ??
                (() => DateTime.UtcNow);
        }

        public TopologyAlertSnapshot GetSnapshot(
            string instanceId)
        {
            if (string.IsNullOrWhiteSpace(
                instanceId))
            {
                throw new ArgumentException(
                    "STP instance id is required.",
                    nameof(instanceId));
            }

            var normalizedInstanceId =
                instanceId.Trim();

            var now =
                _utcNow();

            if (now.Kind != DateTimeKind.Utc)
            {
                throw new InvalidOperationException(
                    "Topology alert snapshot clock must return UTC.");
            }

            var links =
                _topologyRepository
                    .GetPhysicalLinks()
                    .ToArray();

            var interfaces =
                _topologyRepository
                    .GetInterfaces()
                    .ToArray();

            var projector =
                new StpTreeProjector();

            var stpSnapshots =
                _stpObservationReader
                    .GetLatest(
                        normalizedInstanceId)
                    .Select(
                        item =>
                            projector.Project(
                                item.DeviceId,
                                interfaces,
                                item.Observation))
                    .ToArray();

            var forwarding =
                new PhysicalGraphSafetyAnalyzer()
                    .AnalyzeForwardingCycles(
                        links,
                        stpSnapshots,
                        normalizedInstanceId);

            var regionDetector =
                new PhysicalRedundancyRegionDetector();

            var ringAnalyzer =
                new RingProtectionAnalyzer();

            var rings =
                regionDetector
                    .Detect(
                        links)
                    .Select(
                        region =>
                            ringAnalyzer.Analyze(
                                region,
                                links,
                                stpSnapshots,
                                normalizedInstanceId))
                    .ToArray();

            return new TopologyAlertEvaluator()
                .Evaluate(
                    now,
                    forwarding,
                    rings);
        }
    }
}
