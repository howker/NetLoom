using NetLoom.Domain.Observations.Lldp;

namespace NetLoom.Application.Observations.Lldp
{
    public interface ILldpCollector
    {
        LldpObservation Collect(
            LldpCollectionRequest request);
    }
}
