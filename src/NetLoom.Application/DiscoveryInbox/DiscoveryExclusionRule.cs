using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace NetLoom.Application.DiscoveryInbox
{
    public sealed class DiscoveryExclusionRule
    {
        public DiscoveryExclusionRule(
            string targetKind,
            string targetValue)
        {
            TargetKind = targetKind ??
                throw new ArgumentNullException(
                    nameof(targetKind));
            TargetValue = targetValue ??
                throw new ArgumentNullException(
                    nameof(targetValue));
        }

        public string TargetKind { get; }

        public string TargetValue { get; }

        public bool Matches(
            IPAddress address)
        {
            if (address == null ||
                address.AddressFamily != AddressFamily.InterNetwork)
            {
                return false;
            }

            IPAddress target;

            if (string.Equals(
                TargetKind,
                "IpAddress",
                StringComparison.OrdinalIgnoreCase))
            {
                return IPAddress.TryParse(TargetValue, out target) &&
                    target.AddressFamily == AddressFamily.InterNetwork &&
                    string.Equals(
                        address.ToString(),
                        TargetValue,
                        StringComparison.OrdinalIgnoreCase);
            }

            if (!string.Equals(
                TargetKind,
                "Cidr",
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var parts = TargetValue.Split('/');
            int prefixLength;

            if (parts.Length != 2 ||
                !IPAddress.TryParse(parts[0], out target) ||
                target.AddressFamily != AddressFamily.InterNetwork ||
                !int.TryParse(
                    parts[1],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out prefixLength) ||
                prefixLength < 0 || prefixLength > 32)
            {
                return false;
            }

            var mask = prefixLength == 0
                ? 0U
                : uint.MaxValue << (32 - prefixLength);

            return (ToUInt32(address) & mask) ==
                (ToUInt32(target) & mask);
        }

        private static uint ToUInt32(
            IPAddress address)
        {
            var bytes = address.GetAddressBytes();

            return
                ((uint)bytes[0] << 24) |
                ((uint)bytes[1] << 16) |
                ((uint)bytes[2] << 8) |
                bytes[3];
        }
    }
}
