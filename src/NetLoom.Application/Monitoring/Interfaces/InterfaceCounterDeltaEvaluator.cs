using System;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public sealed class InterfaceCounterDeltaEvaluator
    {
        public InterfaceCounterDelta Evaluate(
            InterfaceMonitoringSnapshot previous,
            InterfaceMonitoringSnapshot current)
        {
            if (current == null)
            {
                throw new ArgumentNullException(
                    nameof(current));
            }

            if (previous == null)
            {
                return NoBaseline();
            }

            RequireSameInterface(
                previous,
                current);

            if (current.CapturedUtc <=
                previous.CapturedUtc)
            {
                throw new ArgumentException(
                    "Current interface sample must be newer than previous sample.",
                    nameof(current));
            }

            if (!previous.CounterDiscontinuityTimeTicks.HasValue ||
                !current.CounterDiscontinuityTimeTicks.HasValue)
            {
                return NoBaseline();
            }

            if (previous.CounterDiscontinuityTimeTicks.Value !=
                current.CounterDiscontinuityTimeTicks.Value)
            {
                return new InterfaceCounterDelta(
                    InterfaceCounterDeltaStatus.Discontinuity,
                    null,
                    null,
                    null,
                    null,
                    null);
            }

            return new InterfaceCounterDelta(
                InterfaceCounterDeltaStatus.Valid,
                current.CapturedUtc -
                    previous.CapturedUtc,
                Delta32(
                    previous.InErrors,
                    current.InErrors),
                Delta32(
                    previous.OutErrors,
                    current.OutErrors),
                Delta32(
                    previous.InDiscards,
                    current.InDiscards),
                Delta32(
                    previous.OutDiscards,
                    current.OutDiscards));
        }

        private static InterfaceCounterDelta NoBaseline()
        {
            return new InterfaceCounterDelta(
                InterfaceCounterDeltaStatus.NoBaseline,
                null,
                null,
                null,
                null,
                null);
        }

        private static void RequireSameInterface(
            InterfaceMonitoringSnapshot previous,
            InterfaceMonitoringSnapshot current)
        {
            if (previous.IfIndex != current.IfIndex ||
                previous.DeviceId != current.DeviceId)
            {
                throw new ArgumentException(
                    "Interface counter samples must use the same stable identity.",
                    nameof(current));
            }
        }

        private static ulong? Delta32(
            uint? previous,
            uint? current)
        {
            if (!previous.HasValue ||
                !current.HasValue)
            {
                return null;
            }

            if (current.Value >= previous.Value)
            {
                return
                    (ulong)current.Value -
                    previous.Value;
            }

            return
                ((ulong)uint.MaxValue -
                    previous.Value) +
                1UL +
                current.Value;
        }
    }
}
