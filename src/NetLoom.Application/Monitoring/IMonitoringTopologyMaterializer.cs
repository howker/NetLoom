using System;
using NetLoom.Domain.Observations.Lldp;

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

        void MaterializeLldp(
            Guid deviceId,
            LldpObservation observation);
    }
}
