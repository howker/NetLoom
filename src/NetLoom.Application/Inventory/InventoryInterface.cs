namespace NetLoom.Application.Inventory
{
    public sealed class InventoryInterface
    {
        public InventoryInterface(
            int ifIndex,
            string description,
            string name,
            string alias,
            int? ifType,
            string physicalAddress,
            int? adminStatus,
            int? operStatus,
            long? highSpeedMbps)
        {
            IfIndex = ifIndex;
            Description = description;
            Name = name;
            Alias = alias;
            IfType = ifType;
            PhysicalAddress = physicalAddress;
            AdminStatus = adminStatus;
            OperStatus = operStatus;
            HighSpeedMbps = highSpeedMbps;
        }

        public int IfIndex { get; }

        public string Description { get; }

        public string Name { get; }

        public string Alias { get; }

        public int? IfType { get; }

        public string PhysicalAddress { get; }

        public int? AdminStatus { get; }

        public int? OperStatus { get; }

        public long? HighSpeedMbps { get; }
    }
}
