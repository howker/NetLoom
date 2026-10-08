using System.Collections.Generic;
using NetLoom.Domain.Topology;

namespace NetLoom.Application.DiscoveryInbox
{
    public interface IDiscoveryTopologyReader
    {
        IReadOnlyList<TopologyDevice> GetDevices();

        IReadOnlyList<DeviceInterface> GetInterfaces();
    }
}
