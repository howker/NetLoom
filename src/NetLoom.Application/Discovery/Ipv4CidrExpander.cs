using System;
using System.Collections.Generic;
using System.Net;

namespace NetLoom.Application.Discovery
{
    public static class Ipv4CidrExpander
    {
        public static IReadOnlyList<IPAddress> Expand(
            string cidr,
            int maxAddresses)
        {
            if (string.IsNullOrWhiteSpace(cidr))
            {
                throw new ArgumentException(
                    "CIDR is required.",
                    nameof(cidr));
            }

            if (maxAddresses < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxAddresses));
            }

            var parts = cidr.Split('/');

            if (parts.Length != 2)
            {
                throw new FormatException(
                    "CIDR must contain an address and prefix length.");
            }

            IPAddress address;

            if (!IPAddress.TryParse(parts[0], out address) ||
                address.AddressFamily !=
                System.Net.Sockets.AddressFamily.InterNetwork)
            {
                throw new FormatException(
                    "CIDR must contain an IPv4 address.");
            }

            int prefixLength;

            if (!int.TryParse(parts[1], out prefixLength) ||
                prefixLength < 0 ||
                prefixLength > 32)
            {
                throw new FormatException(
                    "CIDR prefix length must be between 0 and 32.");
            }

            var hostBits = 32 - prefixLength;
            var count = 1UL << hostBits;

            if (count > (ulong)maxAddresses)
            {
                throw new InvalidOperationException(
                    "CIDR contains more addresses than allowed.");
            }

            var addressValue = ToUInt32(address);

            var mask = prefixLength == 0
                ? 0U
                : uint.MaxValue << hostBits;

            var network = addressValue & mask;
            var result = new List<IPAddress>((int)count);

            for (ulong offset = 0; offset < count; offset++)
            {
                result.Add(
                    FromUInt32(
                        network + (uint)offset));
            }

            return result;
        }

        private static uint ToUInt32(IPAddress address)
        {
            var bytes = address.GetAddressBytes();

            return
                ((uint)bytes[0] << 24) |
                ((uint)bytes[1] << 16) |
                ((uint)bytes[2] << 8) |
                bytes[3];
        }

        private static IPAddress FromUInt32(uint value)
        {
            return new IPAddress(
                new[]
                {
                    (byte)(value >> 24),
                    (byte)(value >> 16),
                    (byte)(value >> 8),
                    (byte)value
                });
        }
    }
}
