namespace NetLoom.Application.TopologyRefresh
{
    public interface ITopologyRefreshSnapshotProvider
    {
        TopologyRefreshSnapshot GetSnapshot(
            string stpInstanceId);
    }
}
