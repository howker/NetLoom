using NetLoom.Domain.Observations.Stp;

namespace NetLoom.Application.Observations.Stp
{
    public interface IStpCollector
    {
        StpObservation Collect(
            StpCollectionRequest request);
    }
}
