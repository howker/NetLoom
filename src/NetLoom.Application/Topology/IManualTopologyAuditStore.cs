using NetLoom.Domain.Observations;

namespace NetLoom.Application.Topology
{
    public interface IManualTopologyAuditStore
    {
        void Record(Observation observation);
    }
}
