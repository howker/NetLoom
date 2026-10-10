using System;
using System.Linq;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.Rings;
using NetLoom.Wpf.Localization;

namespace NetLoom.Wpf.MapInteraction
{
    // Тон состояния защиты кольца: норма и нехватка данных нейтральны (ADR-081/083).
    public enum RingStatusTone
    {
        Neutral,
        Warning,
        Critical
    }

    public sealed class RingStatusPresentation
    {
        public RingStatusPresentation(string statusKey, string explanationText, RingStatusTone tone)
        {
            StatusKey = statusKey;
            ExplanationText = explanationText;
            Tone = tone;
        }

        // Ключ ресурса короткого состояния.
        public string StatusKey { get; }

        // Готовое пояснение для оператора; пустая строка, если пояснять нечего.
        public string ExplanationText { get; }

        public RingStatusTone Tone { get; }
    }

    // Состояние защиты кольца словами оператора.
    public static class RingPresentation
    {
        public static RingStatusPresentation Describe(RingDiagnostic ring)
        {
            if (ring == null)
            {
                throw new ArgumentNullException(nameof(ring));
            }

            switch (ring.Status)
            {
                case RingProtectionStatus.Protected:
                    return new RingStatusPresentation("RingStatusProtected",
                        UiText.Get("RingStatusProtectedHint"), RingStatusTone.Neutral);
                case RingProtectionStatus.Unprotected:
                    return new RingStatusPresentation("RingStatusUnprotected",
                        UiText.Get("RingStatusUnprotectedHint"), RingStatusTone.Critical);
                case RingProtectionStatus.Degraded:
                    return new RingStatusPresentation("RingStatusDegraded",
                        UiText.Get(ring.DisabledPhysicalLinkIds.Count > 0
                            ? "RingStatusDegradedDisabledHint"
                            : "RingStatusDegradedBlockingHint"),
                        RingStatusTone.Warning);
                case RingProtectionStatus.Unresolved:
                    return new RingStatusPresentation("RingStatusUnresolved",
                        ring.DevicesWithoutStpIds.Count > 0
                            ? UiText.Format("RingStatusUnresolvedNoStpHint",
                                UiText.FormatCount("DiagnosticDeviceCount", ring.DevicesWithoutStpIds.Count))
                            : UiText.Get("RingStatusUnresolvedPortsHint"),
                        RingStatusTone.Neutral);
                default:
                    return new RingStatusPresentation("RingStatusNotApplicable",
                        string.Empty, RingStatusTone.Neutral);
            }
        }

        public static string KindText(RingDiagnostic ring)
        {
            if (ring == null)
            {
                throw new ArgumentNullException(nameof(ring));
            }

            return UiText.Get(ring.Kind == PhysicalRedundancyRegionKind.CorePairRing
                ? "RingKindCorePair"
                : "RingKindSimple");
        }

        // Заголовок по размещению, где больше всего участников кольца.
        public static string Title(RingDiagnostic ring, Func<Guid, string> locationNameOfDevice)
        {
            if (ring == null)
            {
                throw new ArgumentNullException(nameof(ring));
            }

            if (locationNameOfDevice == null)
            {
                throw new ArgumentNullException(nameof(locationNameOfDevice));
            }

            var best = ring.DeviceIds
                .Select(id => locationNameOfDevice(id))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .GroupBy(name => name.Trim(), StringComparer.CurrentCultureIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
                .FirstOrDefault();

            return best != null
                ? UiText.Format("RingTitleWithLocation", best.Key)
                : UiText.Format("RingTitleWithCount",
                    UiText.FormatCount("DiagnosticDeviceCount", ring.DeviceIds.Count));
        }
    }
}
