using System;
using NetLoom.Domain.Observations.Stp;

namespace NetLoom.Application.Observations.Stp
{
    public interface IStpObservationStore
    {
        void Save(StpObservation observation);

        StpObservation Get(Guid observationId);
    }
}
