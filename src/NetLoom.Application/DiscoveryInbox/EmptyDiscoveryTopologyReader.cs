using System;
using System.Collections.Generic;
using NetLoom.Domain.Topology;

namespace NetLoom.Application.DiscoveryInbox
{
    public sealed class EmptyDiscoveryTopologyReader :
        IDiscoveryTopologyReader
    {
        public IReadOnlyList<TopologyDevice> GetDevices()
        {
            return Array.AsReadOnly(new TopologyDevice[0]);
        }

        public IReadOnlyList<DeviceInterface> GetInterfaces()
        {
            return Array.AsReadOnly(new DeviceInterface[0]);
        }
    }
}
