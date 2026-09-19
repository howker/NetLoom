using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using NetLoom.Application.Discovery;

namespace NetLoom.Protocols.Snmp.Discovery
{
    public sealed class SystemNetworkDiscoveryProbe
        : INetworkDiscoveryProbe
    {
        public bool IsIcmpReachable(
            IPAddress address,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            if (address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }

            if (timeoutMilliseconds < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutMilliseconds));
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using (var ping = new Ping())
                {
                    var reply = ping.Send(
                        address,
                        timeoutMilliseconds);

                    cancellationToken.ThrowIfCancellationRequested();

                    return reply != null &&
                        reply.Status == IPStatus.Success;
                }
            }
            catch (PingException)
            {
                return false;
            }
        }

        public IReadOnlyList<int> FindOpenTcpPorts(
            IPAddress address,
            IReadOnlyList<int> ports,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            if (address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }

            if (ports == null)
            {
                throw new ArgumentNullException(nameof(ports));
            }

            if (timeoutMilliseconds < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutMilliseconds));
            }

            var result = new List<int>();

            foreach (var port in ports)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (port < 1 || port > 65535)
                {
                    throw new ArgumentOutOfRangeException(nameof(ports));
                }

                if (CanConnect(
                    address,
                    port,
                    timeoutMilliseconds,
                    cancellationToken))
                {
                    result.Add(port);
                }
            }

            return result;
        }

        private static bool CanConnect(
            IPAddress address,
            int port,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            try
            {
                using (var client =
                    new TcpClient(address.AddressFamily))
                {
                    var asyncResult =
                        client.BeginConnect(
                            address,
                            port,
                            null,
                            null);

                    using (var waitHandle =
                        asyncResult.AsyncWaitHandle)
                    {
                        var signaled =
                            WaitHandle.WaitAny(
                                new[]
                                {
                                    waitHandle,
                                    cancellationToken.WaitHandle
                                },
                                timeoutMilliseconds);

                        if (signaled == 1)
                        {
                            throw new OperationCanceledException(
                                cancellationToken);
                        }

                        if (signaled == WaitHandle.WaitTimeout)
                        {
                            client.Close();
                            return false;
                        }

                        client.EndConnect(asyncResult);

                        return client.Connected;
                    }
                }
            }
            catch (SocketException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }
    }
}
