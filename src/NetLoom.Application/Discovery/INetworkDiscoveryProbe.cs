using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace NetLoom.Application.Discovery
{
    public interface INetworkDiscoveryProbe
    {
        bool IsIcmpReachable(
            IPAddress address,
            int timeoutMilliseconds,
            CancellationToken cancellationToken);

        IReadOnlyList<int> FindOpenTcpPorts(
            IPAddress address,
            IReadOnlyList<int> ports,
            int timeoutMilliseconds,
            CancellationToken cancellationToken);
    }
}
