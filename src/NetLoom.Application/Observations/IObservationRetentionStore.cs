using System;

namespace NetLoom.Application.Observations
{
    public interface IObservationRetentionStore
    {
        int DeleteOlderThan(
            DateTime cutoffUtc,
            int maxObservations);
    }
}
