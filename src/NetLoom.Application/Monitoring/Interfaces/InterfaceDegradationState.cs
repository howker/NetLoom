using System;
using System.Globalization;
using System.Linq;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public sealed class InterfaceDegradationState
    {
        public InterfaceDegradationState(
            Guid deviceId,
            int ifIndex,
            DateTime capturedUtc,
            InterfaceDegradationStatus status,
            string evidenceFingerprint)
        {
            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Interface degradation state requires a stable DeviceId.",
                    nameof(deviceId));
            }

            if (ifIndex < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ifIndex));
            }

            if (capturedUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Interface degradation state timestamp must be UTC.",
                    nameof(capturedUtc));
            }

            var normalizedFingerprint =
                string.IsNullOrWhiteSpace(
                    evidenceFingerprint)
                    ? string.Empty
                    : evidenceFingerprint.Trim();

            if (status ==
                    InterfaceDegradationStatus.Indeterminate)
            {
                throw new ArgumentException(
                    "Indeterminate classification is not a durable interface degradation state.",
                    nameof(status));
            }

            if (status ==
                    InterfaceDegradationStatus.Healthy &&
                normalizedFingerprint.Length > 0)
            {
                throw new ArgumentException(
                    "Healthy interface degradation state cannot contain an evidence fingerprint.",
                    nameof(evidenceFingerprint));
            }

            if (status ==
                    InterfaceDegradationStatus.Degraded &&
                normalizedFingerprint.Length == 0)
            {
                throw new ArgumentException(
                    "Degraded interface degradation state requires an evidence fingerprint.",
                    nameof(evidenceFingerprint));
            }

            if (status !=
                    InterfaceDegradationStatus.Healthy &&
                status !=
                    InterfaceDegradationStatus.Degraded)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(status));
            }

            DeviceId = deviceId;
            IfIndex = ifIndex;
            CapturedUtc = capturedUtc;
            Status = status;
            EvidenceFingerprint =
                normalizedFingerprint;
        }

        public Guid DeviceId { get; }

        public int IfIndex { get; }

        public DateTime CapturedUtc { get; }

        public InterfaceDegradationStatus Status { get; }

        public string EvidenceFingerprint { get; }

        internal static InterfaceDegradationState
            FromClassification(
                InterfaceDegradationClassification classification)
        {
            if (classification == null)
            {
                throw new ArgumentNullException(
                    nameof(classification));
            }

            if (classification.Status ==
                InterfaceDegradationStatus.Indeterminate)
            {
                throw new ArgumentException(
                    "Indeterminate classification cannot become durable interface degradation state.",
                    nameof(classification));
            }

            var fingerprint =
                classification.Status ==
                    InterfaceDegradationStatus.Healthy
                    ? string.Empty
                    : string.Join(
                        ",",
                        classification.Reasons
                            .Select(
                                reason =>
                                    ((int)reason).ToString(
                                        CultureInfo.InvariantCulture)));

            return new InterfaceDegradationState(
                classification.DeviceId,
                classification.IfIndex,
                classification.CapturedUtc,
                classification.Status,
                fingerprint);
        }
    }
}
