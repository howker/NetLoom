using System;

namespace NetLoom.Contracts.StpTree
{
    public sealed class StpTreePort
    {
        public StpTreePort(
            int bridgePortIndex,
            int? ifIndex,
            Guid? interfaceId,
            string interfaceLabel,
            StpTreePortState state,
            bool isRootPort,
            long? pathCost)
        {
            if (bridgePortIndex < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bridgePortIndex));
            }

            if (ifIndex.HasValue &&
                ifIndex.Value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ifIndex));
            }

            if (interfaceId.HasValue &&
                interfaceId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Interface id cannot be empty.",
                    nameof(interfaceId));
            }

            if (pathCost.HasValue &&
                pathCost.Value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pathCost));
            }

            BridgePortIndex = bridgePortIndex;
            IfIndex = ifIndex;
            InterfaceId = interfaceId;
            InterfaceLabel = Normalize(interfaceLabel);
            State = state;
            IsRootPort = isRootPort;
            PathCost = pathCost;
        }

        public int BridgePortIndex { get; }

        public int? IfIndex { get; }

        public Guid? InterfaceId { get; }

        public string InterfaceLabel { get; }

        public StpTreePortState State { get; }

        public bool IsRootPort { get; }

        public long? PathCost { get; }

        private static string Normalize(
            string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
