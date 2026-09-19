namespace NetLoom.Application.MonitoringControl
{
    public enum MonitoringControlState
    {
        Stopped = 0,
        Starting = 1,
        Running = 2,
        Polling = 3,
        Stopping = 4,
        Faulted = 5
    }
}
