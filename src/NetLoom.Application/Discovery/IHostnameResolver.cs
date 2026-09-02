using System.Collections.Generic;
using System.Net;

namespace NetLoom.Application.Discovery
{
    public interface IHostnameResolver
    {
        IReadOnlyList<IPAddress> Resolve(string hostname);
    }
}
