using NetLoom.Domain.Observations.Cdp;

namespace NetLoom.Application.Observations.Cdp
{
    public interface ICdpCollector
    {
        CdpObservation Collect(
            CdpCollectionRequest request);
    }
}
