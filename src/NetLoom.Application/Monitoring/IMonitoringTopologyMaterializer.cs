using System;
using NetLoom.Domain.Observations.Cdp;
using NetLoom.Domain.Observations.Lldp;

namespace NetLoom.Application.Monitoring
{
    public interface IMonitoringTopologyMaterializer
    {
        void MaterializeDevice(
            Guid deviceId,
            DateTime observedUtc,
            string managementAddress = null);

        void MaterializeInterface(
            Guid deviceId,
            int ifIndex,
            DateTime observedUtc,
            string ifName = null,
            string ifDescription = null,
            string ifAlias = null,
            int? ifType = null);

        void MaterializeLldp(
            Guid deviceId,
            LldpObservation observation);
    }

    public interface IMonitoringCdpTopologyMaterializer
    {
        void MaterializeCdp(
            Guid deviceId,
            CdpObservation observation);
    }
}
