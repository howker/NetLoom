using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace NetLoom.Application.Discovery
{
    public static class Ipv4RangeExpander
    {
        public static IReadOnlyList<IPAddress> Expand(
            string startAddress,
            string endAddress,
            string subnetMask,
            int maxAddresses)
        {
            if (maxAddresses < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxAddresses));
            }

            var start =
                ParseIpv4(
                    startAddress,
                    "DISCOVERY_START_ADDRESS_INVALID");

            var end =
                ParseIpv4(
                    endAddress,
                    "DISCOVERY_END_ADDRESS_INVALID");

            var mask =
                ParseIpv4(
                    subnetMask,
                    "DISCOVERY_SUBNET_MASK_INVALID");

            var startValue =
                ToUInt32(start);

            var endValue =
                ToUInt32(end);

            var maskValue =
                ToUInt32(mask);

            if (!IsContiguousMask(maskValue))
            {
                throw new FormatException(
                    "DISCOVERY_SUBNET_MASK_INVALID");
            }

            if (startValue > endValue)
            {
                throw new ArgumentException(
                    "DISCOVERY_RANGE_ORDER_INVALID");
            }

            if ((startValue & maskValue) !=
                (endValue & maskValue))
            {
                throw new ArgumentException(
                    "DISCOVERY_RANGE_CROSSES_SUBNET");
            }

            var count =
                ((ulong)endValue -
                    startValue) +
                1UL;

            if (count > (ulong)maxAddresses)
            {
                throw new InvalidOperationException(
                    "DISCOVERY_RANGE_TOO_LARGE");
            }

            var result =
                new List<IPAddress>(
                    (int)count);

            for (ulong offset = 0;
                 offset < count;
                 offset++)
            {
                result.Add(
                    FromUInt32(
                        startValue +
                        (uint)offset));
            }

            return result;
        }

        private static IPAddress ParseIpv4(
            string value,
            string error)
        {
            IPAddress address;

            if (string.IsNullOrWhiteSpace(value) ||
                !IPAddress.TryParse(
                    value.Trim(),
                    out address) ||
                address.AddressFamily !=
                    AddressFamily.InterNetwork)
            {
                throw new FormatException(
                    error);
            }

            return address;
        }

        private static bool IsContiguousMask(
            uint mask)
        {
            var inverted =
                ~mask;

            return
                (inverted &
                    unchecked(inverted + 1U)) ==
                0U;
        }

        private static uint ToUInt32(
            IPAddress address)
        {
            var bytes =
                address.GetAddressBytes();

            return
                ((uint)bytes[0] << 24) |
                ((uint)bytes[1] << 16) |
                ((uint)bytes[2] << 8) |
                bytes[3];
        }

        private static IPAddress FromUInt32(
            uint value)
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
