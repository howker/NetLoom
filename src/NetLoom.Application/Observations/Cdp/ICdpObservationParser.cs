using NetLoom.Domain.Observations.Cdp;

namespace NetLoom.Application.Observations.Cdp
{
    public interface ICdpObservationParser
    {
        CdpObservation Parse(
            SnmpObservation observation);
    }
}
