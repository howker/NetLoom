using System;
using System.Collections.Generic;
using NetLoom.Application.Topology;
using NetLoom.Domain.Topology;

namespace NetLoom.Application.DiscoveryInbox
{
    public sealed class MaterializedTopologyDiscoveryReader :
        IDiscoveryTopologyReader
    {
        private readonly IMaterializedTopologyRepository _repository;

        public MaterializedTopologyDiscoveryReader(
            IMaterializedTopologyRepository repository)
        {
            _repository = repository ??
                throw new ArgumentNullException(
                    nameof(repository));
        }

        public IReadOnlyList<TopologyDevice> GetDevices()
        {
            return _repository.GetDevices();
        }

        public IReadOnlyList<DeviceInterface> GetInterfaces()
        {
            return _repository.GetInterfaces();
        }
    }
}
