using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Domain.Observations.Stp
{
    public sealed class StpObservation
    {
        public StpObservation(
            Observation observation,
            string instanceId,
            int? protocolSpecification,
            string designatedRoot,
            long? rootCost,
            int? rootPortBridgePortIndex,
            int? rootPortIfIndex,
            IEnumerable<StpPortState> ports)
        {
            Observation = observation ??
                throw new ArgumentNullException(
                    nameof(observation));

            if (observation.Kind !=
                ObservationKind.Stp)
            {
                throw new ArgumentException(
                    "Observation kind must be Stp.",
                    nameof(observation));
            }

            if (string.IsNullOrWhiteSpace(
                instanceId))
            {
                throw new ArgumentException(
                    "STP InstanceId is required.",
                    nameof(instanceId));
            }

            if (rootCost.HasValue &&
                rootCost.Value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rootCost));
            }

            if (rootPortBridgePortIndex.HasValue &&
                rootPortBridgePortIndex.Value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rootPortBridgePortIndex));
            }

            if (rootPortIfIndex.HasValue &&
                rootPortIfIndex.Value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rootPortIfIndex));
            }

            if (ports == null)
            {
                throw new ArgumentNullException(
                    nameof(ports));
            }

            InstanceId = instanceId.Trim();
            ProtocolSpecification =
                protocolSpecification;
            DesignatedRoot =
                Normalize(designatedRoot);
            RootCost = rootCost;
            RootPortBridgePortIndex =
                rootPortBridgePortIndex;
            RootPortIfIndex = rootPortIfIndex;
            Ports = ports.ToArray();
        }

        public Observation Observation { get; }

        public string InstanceId { get; }

        public int? ProtocolSpecification { get; }

        public string DesignatedRoot { get; }

        public long? RootCost { get; }

        public int? RootPortBridgePortIndex { get; }

        public int? RootPortIfIndex { get; }

        public IReadOnlyList<StpPortState> Ports { get; }

        private static string Normalize(
            string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
