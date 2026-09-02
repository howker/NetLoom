using System.Collections.Generic;
using System.Net;

namespace NetLoom.Application.Discovery
{
    public interface INetworkDiscoveryProbe
    {
        bool IsIcmpReachable(
            IPAddress address,
            int timeoutMilliseconds);

        IReadOnlyList<int> FindOpenTcpPorts(
            IPAddress address,
            IReadOnlyList<int> ports,
            int timeoutMilliseconds);
    }
}
