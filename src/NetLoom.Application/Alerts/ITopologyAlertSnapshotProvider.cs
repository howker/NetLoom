using NetLoom.Contracts.Alerts;

namespace NetLoom.Application.Alerts
{
    public interface ITopologyAlertSnapshotProvider
    {
        TopologyAlertSnapshot GetSnapshot(
            string instanceId);
    }
}
