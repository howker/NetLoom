namespace NetLoom.Application.Monitoring.Interfaces
{
    public enum InterfaceDegradationTransitionKind
    {
        Unchanged = 0,
        FirstAppearance = 1,
        Changed = 2,
        Resolved = 3,
        Indeterminate = 4
    }
}
