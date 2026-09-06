using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Contracts.StpTree;
using NetLoom.Domain.Observations.Stp;
using NetLoom.Domain.Topology;

namespace NetLoom.Topology.Stp
{
    public sealed class StpTreeProjector
    {
        public StpTreeSnapshot Project(
            Guid deviceId,
            IEnumerable<DeviceInterface> interfaces,
            StpObservation observation)
        {
            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Device id is required.",
                    nameof(deviceId));
            }

            if (interfaces == null)
            {
                throw new ArgumentNullException(
                    nameof(interfaces));
            }

            if (observation == null)
            {
                throw new ArgumentNullException(
                    nameof(observation));
            }

            var availableInterfaces =
                interfaces.ToArray();

            if (availableInterfaces.Any(
                item => item == null))
            {
                throw new ArgumentException(
                    "Interface collection cannot contain null.",
                    nameof(interfaces));
            }

            var projectedPorts =
                observation.Ports
                    .OrderBy(
                        port => port.BridgePortIndex)
                    .Select(
                        port =>
                            ProjectPort(
                                deviceId,
                                availableInterfaces,
                                observation,
                                port))
                    .ToArray();

            var rootInterface =
                ResolveInterface(
                    deviceId,
                    observation.RootPortIfIndex,
                    availableInterfaces);

            return new StpTreeSnapshot(
                deviceId,
                observation.Observation.Id,
                observation.Observation.CapturedUtc,
                observation.InstanceId,
                observation.DesignatedRoot,
                observation.RootCost,
                observation.RootPortBridgePortIndex,
                observation.RootPortIfIndex,
                rootInterface != null
                    ? (Guid?)rootInterface.Id
                    : null,
                projectedPorts);
        }

        private static StpTreePort ProjectPort(
            Guid deviceId,
            DeviceInterface[] interfaces,
            StpObservation observation,
            StpPortState port)
        {
            var networkInterface =
                ResolveInterface(
                    deviceId,
                    port.IfIndex,
                    interfaces);

            return new StpTreePort(
                port.BridgePortIndex,
                port.IfIndex,
                networkInterface != null
                    ? (Guid?)networkInterface.Id
                    : null,
                networkInterface != null
                    ? InterfaceLabel(networkInterface)
                    : null,
                MapState(port.State),
                observation.RootPortBridgePortIndex.HasValue &&
                observation.RootPortBridgePortIndex.Value > 0 &&
                observation.RootPortBridgePortIndex.Value ==
                    port.BridgePortIndex,
                port.PathCost);
        }

        private static DeviceInterface ResolveInterface(
            Guid deviceId,
            int? ifIndex,
            IEnumerable<DeviceInterface> interfaces)
        {
            if (!ifIndex.HasValue)
            {
                return null;
            }

            var candidates =
                interfaces
                    .Where(
                        item =>
                            item.DeviceId == deviceId &&
                            item.IfIndex.HasValue &&
                            item.IfIndex.Value ==
                                ifIndex.Value)
                    .Take(2)
                    .ToArray();

            return candidates.Length == 1
                ? candidates[0]
                : null;
        }

        private static string InterfaceLabel(
            DeviceInterface networkInterface)
        {
            if (!string.IsNullOrWhiteSpace(
                networkInterface.CustomName))
            {
                return networkInterface.CustomName;
            }

            if (!string.IsNullOrWhiteSpace(
                networkInterface.IfAlias))
            {
                return networkInterface.IfAlias;
            }

            if (!string.IsNullOrWhiteSpace(
                networkInterface.IfName))
            {
                return networkInterface.IfName;
            }

            if (!string.IsNullOrWhiteSpace(
                networkInterface.IfDescription))
            {
                return networkInterface.IfDescription;
            }

            return null;
        }

        private static StpTreePortState MapState(
            int? state)
        {
            switch (state)
            {
                case 1:
                    return StpTreePortState.Disabled;

                case 2:
                    return StpTreePortState.Blocking;

                case 3:
                    return StpTreePortState.Listening;

                case 4:
                    return StpTreePortState.Learning;

                case 5:
                    return StpTreePortState.Forwarding;

                case 6:
                    return StpTreePortState.Broken;

                default:
                    return StpTreePortState.Unknown;
            }
        }
    }
}
