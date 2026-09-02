using System;

namespace NetLoom.Domain.Observations.Arp
{
    public sealed class ArpEntry
    {
        public ArpEntry(
            int ifIndex,
            int addressType,
            string ipAddress,
            string physicalAddress,
            int? type,
            int? state,
            ArpTableKind tableKind)
        {
            if (ifIndex < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ifIndex));
            }

            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                throw new ArgumentException(
                    "IP address is required.",
                    nameof(ipAddress));
            }

            IfIndex = ifIndex;
            AddressType = addressType;
            IpAddress = ipAddress;
            PhysicalAddress = physicalAddress;
            Type = type;
            State = state;
            TableKind = tableKind;
        }

        public int IfIndex { get; }

        public int AddressType { get; }

        public string IpAddress { get; }

        public string PhysicalAddress { get; }

        public int? Type { get; }

        public int? State { get; }

        public ArpTableKind TableKind { get; }
    }
}
