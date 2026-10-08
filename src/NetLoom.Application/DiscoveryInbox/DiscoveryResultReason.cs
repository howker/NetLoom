namespace NetLoom.Application.DiscoveryInbox
{
    public enum DiscoveryResultReason
    {
        None = 0,
        ProfileExclusion = 1,
        OperatorIgnored = 2,
        DuplicateManagementAddress = 3,
        NameMatchesOtherDevice = 4,
        NotFoundInRun = 5
    }
}
