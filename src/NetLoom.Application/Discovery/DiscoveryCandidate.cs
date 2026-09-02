using System;
using System.Collections.Generic;
using System.Net;
using NetLoom.Application.Inventory;

namespace NetLoom.Application.Discovery
{
    public sealed class DiscoveryCandidate
    {
        public DiscoveryCandidate(
            IPAddress address,
            bool icmpReachable,
            IReadOnlyList<int> openTcpPorts,
            InventorySnapshot inventory,
            Guid? accessProfileId)
        {
            Address = address ??
                throw new ArgumentNullException(nameof(address));

            OpenTcpPorts = openTcpPorts ??
                throw new ArgumentNullException(nameof(openTcpPorts));

            IcmpReachable = icmpReachable;
            Inventory = inventory;
            AccessProfileId = accessProfileId;
        }

        public IPAddress Address { get; }

        public bool IcmpReachable { get; }

        public IReadOnlyList<int> OpenTcpPorts { get; }

        public InventorySnapshot Inventory { get; }

        public Guid? AccessProfileId { get; }

        public bool SnmpResponded
        {
            get { return Inventory != null; }
        }
    }
}
