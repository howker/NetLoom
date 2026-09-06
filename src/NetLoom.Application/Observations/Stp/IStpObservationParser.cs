using NetLoom.Domain.Observations.Stp;

namespace NetLoom.Application.Observations.Stp
{
    public interface IStpObservationParser
    {
        StpObservation Parse(
            SnmpObservation observation);
    }
}
