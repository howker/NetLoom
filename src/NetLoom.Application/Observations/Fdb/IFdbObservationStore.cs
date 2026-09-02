using System;
using NetLoom.Domain.Observations.Fdb;

namespace NetLoom.Application.Observations.Fdb
{
    public interface IFdbObservationStore
    {
        void Save(FdbObservation observation);

        FdbObservation Get(Guid observationId);
    }
}
