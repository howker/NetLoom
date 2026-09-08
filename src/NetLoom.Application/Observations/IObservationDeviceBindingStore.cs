using System;

namespace NetLoom.Application.Observations
{
    public interface IObservationDeviceBindingStore
    {
        void Bind(
            Guid observationId,
            Guid deviceId);

        Guid? GetDeviceId(
            Guid observationId);
    }
}