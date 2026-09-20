using System;
using System.Net;
using NetLoom.Application.Discovery;
using NetLoom.Protocols.Snmp.Discovery;
using NetLoom.Protocols.Snmp.Inventory;
using NetLoom.Protocols.Snmp.Transport;

namespace NetLoom.Engine
{
    internal static class EngineDiscoveryComposition
    {
        public static DiscoveryEngine CreateEngine()
        {
            return new DiscoveryEngine(
                new SystemNetworkDiscoveryProbe(),
                new SnmpInventoryCollector(
                    new SharpSnmpTransport()));
        }

        public static DiscoveryRequest CreateRequest(
            EngineCommandLine options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (!options.AccessProfileId.HasValue)
            {
                throw new InvalidOperationException(
                    "DISCOVERY_ACCESS_PROFILE_ID_REQUIRED");
            }

            var addresses =
                string.IsNullOrWhiteSpace(
                    options.DiscoveryCidr)
                    ? Ipv4RangeExpander.Expand(
                        options.DiscoveryStartAddress,
                        options.DiscoveryEndAddress,
                        options.DiscoverySubnetMask,
                        options.DiscoveryMaxAddresses)
                    : Ipv4CidrExpander.Expand(
                        options.DiscoveryCidr,
                        options.DiscoveryMaxAddresses);

            var profile =
                new DiscoverySnmpProfile(
                    options.AccessProfileId.Value,
                    options.Port,
                    options.Version,
                    EngineSnmpCredentialFactory.Create(
                        options.Version),
                    options.TimeoutMilliseconds,
                    options.RetryCount,
                    options.MaxRepetitions);

            return new DiscoveryRequest(
                addresses,
                new IPAddress[0],
                options.DiscoveryTcpPorts,
                profile,
                options.DiscoveryIcmpTimeoutMilliseconds,
                options.DiscoveryTcpTimeoutMilliseconds,
                options.DiscoveryInterAddressDelayMilliseconds);
        }
    }
}
