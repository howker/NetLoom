using NetLoom.Domain.Observations.Lldp;

namespace NetLoom.Application.Observations.Lldp
{
    public interface ILldpObservationParser
    {
        LldpObservation Parse(
            SnmpObservation observation);
    }
}
