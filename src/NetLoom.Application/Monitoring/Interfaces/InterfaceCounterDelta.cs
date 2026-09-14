using System;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public sealed class InterfaceCounterDelta
    {
        internal InterfaceCounterDelta(
            InterfaceCounterDeltaStatus status,
            TimeSpan? interval,
            ulong? inErrors,
            ulong? outErrors,
            ulong? inDiscards,
            ulong? outDiscards)
        {
            Status = status;
            Interval = interval;
            InErrors = inErrors;
            OutErrors = outErrors;
            InDiscards = inDiscards;
            OutDiscards = outDiscards;
        }

        public InterfaceCounterDeltaStatus Status { get; }

        public TimeSpan? Interval { get; }

        public ulong? InErrors { get; }

        public ulong? OutErrors { get; }

        public ulong? InDiscards { get; }

        public ulong? OutDiscards { get; }
    }
}
