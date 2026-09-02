using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using NetLoom.Domain.Access;

namespace NetLoom.Application.Discovery
{
    public sealed class DiscoveryAddressResolver
    {
        private readonly IHostnameResolver _hostnameResolver;

        public DiscoveryAddressResolver(
            IHostnameResolver hostnameResolver)
        {
            _hostnameResolver = hostnameResolver ??
                throw new ArgumentNullException(nameof(hostnameResolver));
        }

        public IReadOnlyList<IPAddress> Resolve(
            IEnumerable<DiscoveryTarget> targets,
            IEnumerable<DiscoveryTarget> exclusions,
            int maxAddressesPerCidr)
        {
            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            if (exclusions == null)
            {
                throw new ArgumentNullException(nameof(exclusions));
            }

            if (maxAddressesPerCidr < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxAddressesPerCidr));
            }

            var excluded = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var exclusion in exclusions)
            {
                foreach (var address in Expand(
                    exclusion,
                    maxAddressesPerCidr))
                {
                    excluded.Add(address.ToString());
                }
            }

            var seen = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            var result = new List<IPAddress>();

            foreach (var target in targets)
            {
                foreach (var address in Expand(
                    target,
                    maxAddressesPerCidr))
                {
                    var key = address.ToString();

                    if (excluded.Contains(key))
                    {
                        continue;
                    }

                    if (seen.Add(key))
                    {
                        result.Add(address);
                    }
                }
            }

            return result;
        }

        private IReadOnlyList<IPAddress> Expand(
            DiscoveryTarget target,
            int maxAddressesPerCidr)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            switch (target.Kind)
            {
                case AccessTargetKind.IpAddress:
                    return new[]
                    {
                        ParseIpv4(target.Value)
                    };

                case AccessTargetKind.Cidr:
                    return Ipv4CidrExpander.Expand(
                        target.Value,
                        maxAddressesPerCidr);

                case AccessTargetKind.Hostname:
                    return _hostnameResolver
                        .Resolve(target.Value)
                        .Where(
                            address =>
                                address != null &&
                                address.AddressFamily ==
                                AddressFamily.InterNetwork)
                        .GroupBy(address => address.ToString())
                        .Select(group => group.First())
                        .ToArray();

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(target));
            }
        }

        private static IPAddress ParseIpv4(string value)
        {
            IPAddress address;

            if (!IPAddress.TryParse(value, out address) ||
                address.AddressFamily !=
                AddressFamily.InterNetwork)
            {
                throw new FormatException(
                    "Discovery target must be an IPv4 address.");
            }

            return address;
        }
    }
}
