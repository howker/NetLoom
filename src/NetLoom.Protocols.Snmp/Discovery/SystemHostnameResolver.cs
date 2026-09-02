using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using NetLoom.Application.Discovery;

namespace NetLoom.Protocols.Snmp.Discovery
{
    public sealed class SystemHostnameResolver
        : IHostnameResolver
    {
        public IReadOnlyList<IPAddress> Resolve(
            string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname))
            {
                throw new ArgumentException(
                    "Hostname is required.",
                    nameof(hostname));
            }

            try
            {
                return Dns.GetHostAddresses(hostname)
                    .Where(
                        address =>
                            address.AddressFamily ==
                            AddressFamily.InterNetwork)
                    .ToArray();
            }
            catch (SocketException)
            {
                return new IPAddress[0];
            }
        }
    }
}
