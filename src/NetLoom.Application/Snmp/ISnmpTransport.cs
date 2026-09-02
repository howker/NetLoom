using System.Collections.Generic;

namespace NetLoom.Application.Snmp
{
    public interface ISnmpTransport
    {
        IReadOnlyList<SnmpVariable> Get(SnmpGetRequest request);

        IReadOnlyList<SnmpVariable> Walk(SnmpWalkRequest request);
    }
}
