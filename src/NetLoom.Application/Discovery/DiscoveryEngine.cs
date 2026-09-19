using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
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
            return Discover(
                    request,
                    CancellationToken.None)
                .Where(progress => progress.CandidateFound)
                .Select(progress => progress.Candidate)
                .ToArray();
        }

        public IEnumerable<DiscoveryProgress> Discover(
            DiscoveryRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var exclusions = new HashSet<string>(
                request.Exclusions.Select(
                    address => address.ToString()),
                StringComparer.OrdinalIgnoreCase);

            var addresses = request.Addresses
                .Where(
                    address =>
                        !exclusions.Contains(
                            address.ToString()))
                .ToArray();

            for (var index = 0;
                 index < addresses.Length;
                 index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (index > 0)
                {
                    WaitBeforeNextAddress(
                        request.InterAddressDelayMilliseconds,
                        cancellationToken);
                }

                var address = addresses[index];
                var candidate = Probe(
                    address,
                    request,
                    cancellationToken);

                yield return new DiscoveryProgress(
                    address,
                    index + 1,
                    addresses.Length,
                    candidate);
            }
        }

        private DiscoveryCandidate Probe(
            IPAddress address,
            DiscoveryRequest request,
            CancellationToken cancellationToken)
        {
            var icmpReachable =
                _networkProbe.IsIcmpReachable(
                    address,
                    request.IcmpTimeoutMilliseconds,
                    cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var openTcpPorts =
                _networkProbe.FindOpenTcpPorts(
                    address,
                    request.TcpPorts,
                    request.TcpTimeoutMilliseconds,
                    cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            InventorySnapshot inventory = null;
            Guid? accessProfileId = null;

            try
            {
                var profile =
                    request.SnmpProfile;

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
            }
            catch (SnmpTransportException)
            {
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!icmpReachable &&
                openTcpPorts.Count == 0 &&
                inventory == null)
            {
                return null;
            }

            return new DiscoveryCandidate(
                address,
                icmpReachable,
                openTcpPorts,
                inventory,
                accessProfileId);
        }

        private static void WaitBeforeNextAddress(
            int delayMilliseconds,
            CancellationToken cancellationToken)
        {
            if (delayMilliseconds <= 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return;
            }

            if (cancellationToken.WaitHandle.WaitOne(
                delayMilliseconds))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }
}
