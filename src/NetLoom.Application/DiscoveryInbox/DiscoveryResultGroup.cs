namespace NetLoom.Application.DiscoveryInbox
{
    public enum DiscoveryResultGroup
    {
        New = 1,
        Changed = 2,
        Ambiguous = 3,
        Missing = 4,
        Excluded = 5,
        Error = 6,
        KnownUnchanged = 7
    }
}
