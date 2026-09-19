using System;
using System.Net;

namespace NetLoom.Application.MonitoringControl
{
    public sealed class MonitoringTarget
    {
        public MonitoringTarget(
            Guid deviceId,
            IPAddress targetAddress)
        {
            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "DEVICE_ID_REQUIRED",
                    nameof(deviceId));
            }

            DeviceId = deviceId;
            TargetAddress =
                targetAddress ??
                throw new ArgumentNullException(
                    nameof(targetAddress));
        }

        public Guid DeviceId { get; }

        public IPAddress TargetAddress { get; }
    }
}
