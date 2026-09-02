using NetLoom.Domain.Observations.Fdb;

namespace NetLoom.Application.Observations.Fdb
{
    public interface IFdbObservationParser
    {
        FdbObservation Parse(
            SnmpObservation observation);
    }
}
