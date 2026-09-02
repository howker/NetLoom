using System;
using NetLoom.Domain.Observations.Cdp;

namespace NetLoom.Application.Observations.Cdp
{
    public interface ICdpObservationStore
    {
        void Save(CdpObservation observation);

        CdpObservation Get(Guid observationId);
    }
}
