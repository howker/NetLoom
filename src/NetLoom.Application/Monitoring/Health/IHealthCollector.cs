namespace NetLoom.Application.Monitoring.Health
{
    public interface IHealthCollector
    {
        HealthSnapshot Collect(
            HealthCollectionRequest request);
    }
}
