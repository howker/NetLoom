using NetLoom.Domain.Observations.Fdb;

namespace NetLoom.Application.Observations.Fdb
{
    public interface IFdbCollector
    {
        FdbObservation Collect(
            FdbCollectionRequest request);
    }
}
