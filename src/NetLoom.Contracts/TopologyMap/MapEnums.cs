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
        ArpFdbCorrelation
    }

    public enum MapEvidenceStrength
    {
        Weak,
        Strong
    }
}
