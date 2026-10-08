using System;

namespace NetLoom.Application.Topology
{
    public interface IDeviceIgnoreStore
    {
        void SetIgnored(Guid deviceId, DateTime? ignoredUtc);
    }
}
