using NetLoom.Domain.Observations.Arp;

namespace NetLoom.Application.Observations.Arp
{
    public interface IArpCollector
    {
        ArpObservation Collect(
            ArpCollectionRequest request);
    }
}
