using NetLoom.Domain.Observations.Arp;

namespace NetLoom.Application.Observations.Arp
{
    public interface IArpObservationParser
    {
        ArpObservation Parse(
            SnmpObservation observation);
    }
}
