using System;
using System.Collections.Generic;
using NetLoom.Domain.Observations.Stp;

namespace NetLoom.Application.Observations.Stp
{
    public interface ILatestStpObservationReader
    {
        IReadOnlyList<BoundStpObservation> GetLatest(
            string instanceId);
    }

    public sealed class BoundStpObservation
    {
        public BoundStpObservation(
            Guid deviceId,
            StpObservation observation)
        {
            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Device id is required.",
                    nameof(deviceId));
            }

            DeviceId = deviceId;

            Observation =
                observation ??
                throw new ArgumentNullException(
                    nameof(observation));
        }

        public Guid DeviceId { get; }

        public StpObservation Observation { get; }
    }
}
