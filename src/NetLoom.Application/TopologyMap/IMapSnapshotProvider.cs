using NetLoom.Contracts.TopologyMap;

namespace NetLoom.Application.TopologyMap
{
    public interface IMapSnapshotProvider
    {
        MapSnapshot GetSnapshot();
    }
}
