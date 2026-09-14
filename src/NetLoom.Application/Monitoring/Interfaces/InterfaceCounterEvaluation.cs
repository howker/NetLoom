using System;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public sealed class InterfaceCounterEvaluation
    {
        public InterfaceCounterEvaluation(
            InterfaceMonitoringSnapshot currentSnapshot,
            InterfaceCounterDelta delta)
        {
            CurrentSnapshot =
                currentSnapshot ??
                throw new ArgumentNullException(
                    nameof(currentSnapshot));

            if (!currentSnapshot.DeviceId.HasValue)
            {
                throw new ArgumentException(
                    "Interface counter evaluation requires a stable DeviceId.",
                    nameof(currentSnapshot));
            }

            Delta =
                delta ??
                throw new ArgumentNullException(
                    nameof(delta));
        }

        public Guid DeviceId
        {
            get { return CurrentSnapshot.DeviceId.Value; }
        }

        public int IfIndex
        {
            get { return CurrentSnapshot.IfIndex; }
        }

        public DateTime CapturedUtc
        {
            get { return CurrentSnapshot.CapturedUtc; }
        }

        public InterfaceMonitoringSnapshot CurrentSnapshot { get; }

        public InterfaceCounterDelta Delta { get; }
    }
}
