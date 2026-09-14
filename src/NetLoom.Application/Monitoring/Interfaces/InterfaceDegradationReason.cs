namespace NetLoom.Application.Monitoring.Interfaces
{
    public enum InterfaceDegradationReason
    {
        Unknown = 0,
        NoBaseline = 1,
        CounterDiscontinuity = 2,
        IncompleteCounterData = 3,
        ErrorRateThresholdExceeded = 4,
        DiscardRateThresholdExceeded = 5
    }
}
