using System;
using NetLoom.Domain.Observations.Lldp;

namespace NetLoom.Application.Observations.Lldp
{
    public interface ILldpObservationStore
    {
        void Save(LldpObservation observation);

        LldpObservation Get(Guid observationId);
    }
}
