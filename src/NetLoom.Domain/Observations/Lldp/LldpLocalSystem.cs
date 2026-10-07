using System;

namespace NetLoom.Domain.Observations.Lldp
{
    public sealed class LldpLocalSystem
    {
        public LldpLocalSystem(
            int? chassisIdSubtype,
            string chassisId,
            string systemName,
            string systemCapabilitiesEnabled = null)
        {
            ChassisIdSubtype = chassisIdSubtype;
            ChassisId = Normalize(chassisId);
            SystemName = Normalize(systemName);
            SystemCapabilitiesEnabled = Normalize(systemCapabilitiesEnabled);
        }

        public int? ChassisIdSubtype { get; }

        public string ChassisId { get; }

        public string SystemName { get; }

        public string SystemCapabilitiesEnabled { get; }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
