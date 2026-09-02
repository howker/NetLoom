using System;

namespace NetLoom.Domain.Observations.Fdb
{
    public sealed class BridgePortMapping
    {
        public BridgePortMapping(
            int bridgePortIndex,
            int ifIndex)
        {
            if (bridgePortIndex < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bridgePortIndex));
            }

            if (ifIndex < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ifIndex));
            }

            BridgePortIndex = bridgePortIndex;
            IfIndex = ifIndex;
        }

        public int BridgePortIndex { get; }

        public int IfIndex { get; }
    }
}
