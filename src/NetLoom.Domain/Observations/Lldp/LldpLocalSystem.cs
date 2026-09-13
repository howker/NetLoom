using System;

namespace NetLoom.Domain.Observations.Lldp
{
    public sealed class LldpLocalSystem
    {
        public LldpLocalSystem(
            int? chassisIdSubtype,
            string chassisId,
            string systemName)
        {
            ChassisIdSubtype = chassisIdSubtype;
            ChassisId = Normalize(chassisId);
            SystemName = Normalize(systemName);
        }

        public int? ChassisIdSubtype { get; }

        public string ChassisId { get; }

        public string SystemName { get; }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
