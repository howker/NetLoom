using System;

namespace NetLoom.Topology.Correlation
{
    public sealed class MacCorrelation
    {
        public MacCorrelation(
            string ipAddress,
            string macAddress,
            string arpSourceAddress,
            int arpIfIndex,
            string fdbSourceAddress,
            int bridgePortIndex,
            int? fdbIfIndex)
        {
            IpAddress = ipAddress ??
                throw new ArgumentNullException(nameof(ipAddress));

            MacAddress = macAddress ??
                throw new ArgumentNullException(nameof(macAddress));

            ArpSourceAddress = arpSourceAddress ??
                throw new ArgumentNullException(
                    nameof(arpSourceAddress));

            FdbSourceAddress = fdbSourceAddress ??
                throw new ArgumentNullException(
                    nameof(fdbSourceAddress));

            ArpIfIndex = arpIfIndex;
            BridgePortIndex = bridgePortIndex;
            FdbIfIndex = fdbIfIndex;
        }

        public string IpAddress { get; }

        public string MacAddress { get; }

        public string ArpSourceAddress { get; }

        public int ArpIfIndex { get; }

        public string FdbSourceAddress { get; }

        public int BridgePortIndex { get; }

        public int? FdbIfIndex { get; }
    }
}
