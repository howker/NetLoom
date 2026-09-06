namespace NetLoom.Application.Monitoring.Metrics
{
    public interface IMonitoringMetricStore
    {
        void Append(
            MonitoringMetricSample sample);
    }
}
