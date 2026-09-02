using System;
using NetLoom.Domain.Observations.Arp;

namespace NetLoom.Application.Observations.Arp
{
    public interface IArpObservationStore
    {
        void Save(ArpObservation observation);

        ArpObservation Get(Guid observationId);
    }
}
