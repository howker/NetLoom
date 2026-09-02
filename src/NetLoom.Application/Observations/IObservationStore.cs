using System;

namespace NetLoom.Application.Observations
{
    public interface IObservationStore
    {
        void SaveSnmp(SnmpObservation observation);

        SnmpObservation GetSnmp(Guid observationId);
    }
}
