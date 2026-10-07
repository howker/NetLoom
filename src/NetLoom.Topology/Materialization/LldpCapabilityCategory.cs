using System;
using System.Globalization;
using NetLoom.Domain.Topology;

namespace NetLoom.Topology.Materialization
{
    // Sprint 48 (Г1): категория устройства из включённых возможностей LLDP (IEEE 802.1AB, LldpSystemCapabilitiesMap).
    // Первый байт BITS: 0x20 — bridge, 0x08 — router. Bridge с router (коммутатор L3) — коммутатор:
    // На объекте это ядро сети, а физическая топология строится по коммутации. Остальное — без категории.
    public static class LldpCapabilityCategory
    {
        private const byte Bridge = 0x20;
        private const byte Router = 0x08;

        public static DeviceCategory? FromEnabled(
            string capabilities)
        {
            byte first;

            if (!TryReadFirstByte(
                    capabilities,
                    out first))
            {
                return null;
            }

            if ((first & Bridge) != 0)
            {
                return DeviceCategory.Switch;
            }

            if ((first & Router) != 0)
            {
                return DeviceCategory.Router;
            }

            return null;
        }

        private static bool TryReadFirstByte(
            string capabilities,
            out byte value)
        {
            value = 0;

            if (string.IsNullOrWhiteSpace(
                capabilities))
            {
                return false;
            }

            var first =
                capabilities
                    .Trim()
                    .Split(':')[0];

            return first.Length == 2 &&
                byte.TryParse(
                    first,
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out value);
        }
    }
}
