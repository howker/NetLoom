using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Application.Inventory;
using NetLoom.Application.Snmp;

namespace NetLoom.Application.Discovery
{
    public sealed class DiscoveryEngine
    {
        private readonly INetworkDiscoveryProbe _networkProbe;
        private readonly IInventoryCollector _inventoryCollector;

        public DiscoveryEngine(
            INetworkDiscoveryProbe networkProbe,
            IInventoryCollector inventoryCollector)
        {
            _networkProbe = networkProbe ??
                throw new ArgumentNullException(nameof(networkProbe));

            _inventoryCollector = inventoryCollector ??
                throw new ArgumentNullException(nameof(inventoryCollector));
        }

        public IReadOnlyList<DiscoveryCandidate> Discover(
            DiscoveryRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var exclusions = new HashSet<string>(
                request.Exclusions.Select(
                    address => address.ToString()),
                StringComparer.OrdinalIgnoreCase);

            var results = new List<DiscoveryCandidate>();

            foreach (var address in request.Addresses)
            {
                if (exclusions.Contains(address.ToString()))
                {
                    continue;
                }

                var icmpReachable =
                    _networkProbe.IsIcmpReachable(
                        address,
                        request.IcmpTimeoutMilliseconds);

                var openTcpPorts =
                    _networkProbe.FindOpenTcpPorts(
                        address,
                        request.TcpPorts,
                        request.TcpTimeoutMilliseconds);

                InventorySnapshot inventory = null;
                Guid? accessProfileId = null;

                foreach (var profile in request.SnmpProfiles)
                {
                    try
                    {
                        inventory = _inventoryCollector.Collect(
                            new InventoryCollectionRequest(
                                address,
                                profile.Port,
                                profile.Version,
                                profile.Credentials,
                                profile.TimeoutMilliseconds,
                                profile.RetryCount,
                                profile.MaxRepetitions));

                        accessProfileId =
                            profile.AccessProfileId;

                        break;
                    }
                    catch (SnmpTransportException)
                    {
                    }
                }

                if (!icmpReachable &&
                    openTcpPorts.Count == 0 &&
                    inventory == null)
                {
                    continue;
                }

                results.Add(
                    new DiscoveryCandidate(
                        address,
                        icmpReachable,
                        openTcpPorts,
                        inventory,
                        accessProfileId));
            }

            return results;
        }
    }
}
