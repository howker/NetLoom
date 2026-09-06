using System;
using System.Collections.Generic;
using NetLoom.Domain.Topology;

namespace NetLoom.Application.Topology
{
    public interface IMaterializedTopologyRepository
    {
        void SaveDevice(TopologyDevice device);

        void SaveInterface(DeviceInterface networkInterface);

        PhysicalLink SavePhysicalLink(PhysicalLink link);

        TopologyDevice GetDevice(Guid id);

        IReadOnlyList<TopologyDevice> GetDevices();

        IReadOnlyList<DeviceInterface> GetInterfaces();

        IReadOnlyList<PhysicalLink> GetPhysicalLinks();

        void DeleteManualPhysicalLink(Guid id);

        void DeleteManualInterface(Guid id);

        void DeleteManualDevice(Guid id);
    }
}
