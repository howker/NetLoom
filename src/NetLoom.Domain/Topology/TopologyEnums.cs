namespace NetLoom.Domain.Topology
{
    public enum DeviceDiscoveryOrigin
    {
        Automatic,
        Manual,
        Imported
    }

    public enum MonitoringCapability
    {
        Unknown,
        None
    }

    public enum DeviceCategory
    {
        Unknown,
        MediaConverter,
        UnmanagedSwitch,
        OpticalConverter,
        PassiveNetworkEquipment
    }

    public enum PhysicalLinkStrength
    {
        Confirmed,
        Observed,
        Inferred,
        Manual
    }

    public enum PhysicalLinkFreshness
    {
        Fresh,
        Aging,
        Stale
    }
}
