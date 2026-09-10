using System;

namespace NetLoom.Application.Monitoring
{
    public interface IMonitoringTopologyMaterializer
    {
        void MaterializeDevice(
            Guid deviceId,
            DateTime observedUtc);

        void MaterializeInterface(
            Guid deviceId,
            int ifIndex,
            DateTime observedUtc);
    }
}
