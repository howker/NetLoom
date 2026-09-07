using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Contracts.StpTree;

namespace NetLoom.Topology.Stp
{
    internal enum StpEndpointState
    {
        Unresolved = 0,
        Disabled = 1,
        Blocking = 2,
        Forwarding = 3
    }

    internal sealed class StpEndpointStateResolver
    {
        private readonly IDictionary<
            Guid,
            StpTreeSnapshot[]> _snapshots;

        public StpEndpointStateResolver(
            IEnumerable<StpTreeSnapshot> snapshots,
            string instanceId)
        {
            if (snapshots == null)
            {
                throw new ArgumentNullException(
                    nameof(snapshots));
            }

            if (string.IsNullOrWhiteSpace(
                instanceId))
            {
                throw new ArgumentException(
                    "STP instance id is required.",
                    nameof(instanceId));
            }

            var input =
                snapshots.ToArray();

            if (input.Any(
                snapshot => snapshot == null))
            {
                throw new ArgumentException(
                    "STP snapshots cannot contain null.",
                    nameof(snapshots));
            }

            InstanceId =
                instanceId.Trim();

            _snapshots =
                input
                    .Where(
                        snapshot =>
                            string.Equals(
                                snapshot.InstanceId,
                                InstanceId,
                                StringComparison.Ordinal))
                    .GroupBy(
                        snapshot =>
                            snapshot.DeviceId)
                    .ToDictionary(
                        group =>
                            group.Key,
                        group =>
                            group.ToArray());
        }

        public string InstanceId { get; }

        public StpEndpointState Resolve(
            Guid deviceId,
            Guid? interfaceId)
        {
            if (deviceId == Guid.Empty ||
                !interfaceId.HasValue)
            {
                return
                    StpEndpointState.Unresolved;
            }

            StpTreeSnapshot[] deviceSnapshots;

            if (!_snapshots.TryGetValue(
                    deviceId,
                    out deviceSnapshots) ||
                deviceSnapshots.Length != 1)
            {
                return
                    StpEndpointState.Unresolved;
            }

            var matchingPorts =
                deviceSnapshots[0]
                    .Ports
                    .Where(
                        port =>
                            port != null &&
                            port.InterfaceId.HasValue &&
                            port.InterfaceId.Value ==
                                interfaceId.Value)
                    .ToArray();

            if (matchingPorts.Length != 1)
            {
                return
                    StpEndpointState.Unresolved;
            }

            switch (matchingPorts[0].State)
            {
                case StpTreePortState.Forwarding:
                    return
                        StpEndpointState.Forwarding;

                case StpTreePortState.Blocking:
                    return
                        StpEndpointState.Blocking;

                case StpTreePortState.Disabled:
                    return
                        StpEndpointState.Disabled;

                default:
                    return
                        StpEndpointState.Unresolved;
            }
        }
    }
}
