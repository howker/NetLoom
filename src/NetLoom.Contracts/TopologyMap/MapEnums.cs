namespace NetLoom.Contracts.TopologyMap
{
    public enum MapConfidence
    {
        Low,
        Medium,
        High
    }

    public enum MapFreshness
    {
        Fresh,
        Aging,
        Stale
    }

    public enum MapEvidenceKind
    {
        Lldp,
        Cdp,
        ArpFdbCorrelation,
        Manual
    }

    public enum MapEvidenceStrength
    {
        Weak,
        Strong
    }

    public enum MapNodeOrigin
    {
        Unknown,
        Automatic,
        Manual,
        Imported
    }

    public enum MapMonitoringCapability
    {
        Unknown,
        None
    }

    public enum MapNodeCategory
    {
        Unknown,
        MediaConverter,
        UnmanagedSwitch,
        OpticalConverter,
        PassiveNetworkEquipment
    }
}
