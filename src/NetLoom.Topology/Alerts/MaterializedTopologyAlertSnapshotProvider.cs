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
            var normalizedInstanceId =
                NormalizeInstanceId(
                    instanceId);

            return Evaluate(
                normalizedInstanceId,
                _topologyRepository
                    .GetPhysicalLinks()
                    .ToArray(),
                _topologyRepository
                    .GetInterfaces()
                    .ToArray(),
                _stpObservationReader
                    .GetLatest(
                        normalizedInstanceId));
        }

        public TopologyAlertSnapshot GetSnapshot(
            string instanceId,
            MaterializedTopologyReadSet readSet)
        {
            if (readSet == null)
            {
                throw new ArgumentNullException(
                    nameof(readSet));
            }

            var normalizedInstanceId =
                NormalizeInstanceId(
                    instanceId);

            return Evaluate(
                normalizedInstanceId,
                readSet.PhysicalLinks,
                readSet.Interfaces,
                readSet.LatestStp);
        }

        private TopologyAlertSnapshot Evaluate(
            string normalizedInstanceId,
            System.Collections.Generic.IEnumerable<
                NetLoom.Domain.Topology.PhysicalLink> linkSource,
            System.Collections.Generic.IEnumerable<
                NetLoom.Domain.Topology.DeviceInterface> interfaceSource,
            System.Collections.Generic.IEnumerable<
                BoundStpObservation> stpSource)
        {
            var now =
                _utcNow();

            if (now.Kind != DateTimeKind.Utc)
            {
                throw new InvalidOperationException(
                    "Topology alert snapshot clock must return UTC.");
            }

            var links =
                linkSource.ToArray();

            var interfaces =
                interfaceSource.ToArray();

            var projector =
                new StpTreeProjector();

            var stpSnapshots =
                stpSource
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

        private static string NormalizeInstanceId(
            string instanceId)
        {
            if (string.IsNullOrWhiteSpace(
                instanceId))
            {
                throw new ArgumentException(
                    "STP instance id is required.",
                    nameof(instanceId));
            }

            return instanceId.Trim();
        }

    }
}
