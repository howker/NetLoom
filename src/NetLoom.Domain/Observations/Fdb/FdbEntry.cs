using System;

namespace NetLoom.Domain.Observations.Fdb
{
    public sealed class FdbEntry
    {
        public FdbEntry(
            string macAddress,
            int? bridgePortIndex,
            int? status)
        {
            if (string.IsNullOrWhiteSpace(macAddress))
            {
                throw new ArgumentException(
                    "MAC address is required.",
                    nameof(macAddress));
            }

            if (bridgePortIndex.HasValue &&
                (bridgePortIndex.Value < 0 ||
                 bridgePortIndex.Value > 65535))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bridgePortIndex));
            }

            MacAddress = macAddress;
            BridgePortIndex = bridgePortIndex;
            Status = status;
        }

        public string MacAddress { get; }

        public int? BridgePortIndex { get; }

        public int? Status { get; }
    }
}
