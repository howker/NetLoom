namespace NetLoom.Application.DiscoveryControl
{
    public enum DiscoveryControlState
    {
        Idle = 0,
        Starting = 1,
        Running = 2,
        Stopping = 3,
        Completed = 4,
        Stopped = 5,
        Faulted = 6
    }
}
