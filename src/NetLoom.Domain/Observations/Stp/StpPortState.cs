using System;

namespace NetLoom.Domain.Observations.Stp
{
    public sealed class StpPortState
    {
        public StpPortState(
            int bridgePortIndex,
            int? ifIndex,
            int? priority,
            int? state,
            int? enabled,
            long? pathCost,
            string designatedRoot,
            long? designatedCost,
            string designatedBridge,
            string designatedPort,
            long? forwardTransitions)
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

            if (pathCost.HasValue &&
                pathCost.Value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pathCost));
            }

            if (designatedCost.HasValue &&
                designatedCost.Value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(designatedCost));
            }

            if (forwardTransitions.HasValue &&
                forwardTransitions.Value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(forwardTransitions));
            }

            BridgePortIndex = bridgePortIndex;
            IfIndex = ifIndex;
            Priority = priority;
            State = state;
            Enabled = enabled;
            PathCost = pathCost;
            DesignatedRoot = Normalize(designatedRoot);
            DesignatedCost = designatedCost;
            DesignatedBridge = Normalize(designatedBridge);
            DesignatedPort = Normalize(designatedPort);
            ForwardTransitions = forwardTransitions;
        }

        public int BridgePortIndex { get; }

        public int? IfIndex { get; }

        public int? Priority { get; }

        public int? State { get; }

        public int? Enabled { get; }

        public long? PathCost { get; }

        public string DesignatedRoot { get; }

        public long? DesignatedCost { get; }

        public string DesignatedBridge { get; }

        public string DesignatedPort { get; }

        public long? ForwardTransitions { get; }

        private static string Normalize(
            string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
