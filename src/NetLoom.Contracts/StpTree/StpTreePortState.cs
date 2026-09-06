namespace NetLoom.Contracts.StpTree
{
    public enum StpTreePortState
    {
        Unknown = 0,
        Disabled = 1,
        Blocking = 2,
        Listening = 3,
        Learning = 4,
        Forwarding = 5,
        Broken = 6
    }
}
