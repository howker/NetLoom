using System.Collections.Generic;
using System.Net;

namespace NetLoom.Application.Inventory
{
    public sealed class InventorySnapshot
    {
        public InventorySnapshot(
            IPAddress managementAddress,
            string sysName,
            string sysDescription,
            string sysObjectId,
            string sysContact,
            string sysLocation,
            string sysUpTime,
            IReadOnlyList<InventoryInterface> interfaces)
        {
            ManagementAddress = managementAddress;
            SysName = sysName;
            SysDescription = sysDescription;
            SysObjectId = sysObjectId;
            SysContact = sysContact;
            SysLocation = sysLocation;
            SysUpTime = sysUpTime;
            Interfaces = interfaces;
        }

        public IPAddress ManagementAddress { get; }

        public string SysName { get; }

        public string SysDescription { get; }

        public string SysObjectId { get; }

        public string SysContact { get; }

        public string SysLocation { get; }

        public string SysUpTime { get; }

        public IReadOnlyList<InventoryInterface> Interfaces { get; }
    }
}
