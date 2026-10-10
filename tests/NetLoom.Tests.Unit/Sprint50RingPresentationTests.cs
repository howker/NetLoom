using System;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.Rings;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint50RingPresentationTests
    {
        private CultureInfo _savedUi;
        private CultureInfo _saved;

        [TestInitialize]
        public void SetRussianCulture()
        {
            _savedUi = CultureInfo.CurrentUICulture;
            _saved = CultureInfo.CurrentCulture;
            CultureInfo.CurrentUICulture = new CultureInfo("ru-RU");
            CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
        }

        [TestCleanup]
        public void RestoreCulture()
        {
            CultureInfo.CurrentUICulture = _savedUi;
            CultureInfo.CurrentCulture = _saved;
        }

        private static Guid Id(int n)
        {
            return Guid.Parse("50505050-0005-0000-0000-" + n.ToString("D12"));
        }

        private static RingDiagnostic Ring(RingProtectionStatus status, int devices = 3,
            int disabled = 0, int blocking = 0, int withoutStp = 0,
            PhysicalRedundancyRegionKind kind = PhysicalRedundancyRegionKind.SimpleRing)
        {
            return new RingDiagnostic("ring-1", kind, status,
                Enumerable.Range(1, devices).Select(Id), new Guid[0], new Guid[0],
                Enumerable.Range(100, blocking).Select(Id),
                Enumerable.Range(200, disabled).Select(Id),
                new Guid[0], new RingBlockedPort[0], null, null,
                Enumerable.Range(300, withoutStp).Select(Id), null);
        }

        [TestMethod]
        public void EachStatusHasOwnTextAndTone()
        {
            var protectedRing = RingPresentation.Describe(Ring(RingProtectionStatus.Protected, blocking: 1));
            Assert.AreEqual("RingStatusProtected", protectedRing.StatusKey);
            Assert.AreEqual("Одна связь кольца заблокирована STP и служит резервом.", protectedRing.ExplanationText);
            Assert.AreEqual(RingStatusTone.Neutral, protectedRing.Tone);

            var unprotected = RingPresentation.Describe(Ring(RingProtectionStatus.Unprotected));
            Assert.AreEqual("RingStatusUnprotected", unprotected.StatusKey);
            Assert.AreEqual("STP не блокирует ни одной связи кольца: все связи пересылают трафик.",
                unprotected.ExplanationText);
            Assert.AreEqual(RingStatusTone.Critical, unprotected.Tone);

            var degraded = RingPresentation.Describe(Ring(RingProtectionStatus.Degraded, disabled: 1));
            Assert.AreEqual("RingStatusDegraded", degraded.StatusKey);
            Assert.AreEqual(RingStatusTone.Warning, degraded.Tone);

            var unresolved = RingPresentation.Describe(Ring(RingProtectionStatus.Unresolved));
            Assert.AreEqual("RingStatusUnresolved", unresolved.StatusKey);
            Assert.AreEqual(RingStatusTone.Neutral, unresolved.Tone);

            foreach (var status in new[] { RingProtectionStatus.NotApplicable, RingProtectionStatus.Unknown })
            {
                var other = RingPresentation.Describe(Ring(status));
                Assert.AreEqual("RingStatusNotApplicable", other.StatusKey);
                Assert.AreEqual(string.Empty, other.ExplanationText);
                Assert.AreEqual(RingStatusTone.Neutral, other.Tone);
            }
        }

        [TestMethod]
        public void UnresolvedWithoutStpDataNamesDeviceCountAndIsNeutral()
        {
            var result = RingPresentation.Describe(Ring(RingProtectionStatus.Unresolved, withoutStp: 3));
            Assert.AreEqual("Нет данных STP: 3 устройства. Защиту определить нельзя.", result.ExplanationText);
            Assert.AreEqual(RingStatusTone.Neutral, result.Tone);
        }

        [TestMethod]
        public void UnresolvedWithoutStpDataFallsBackToPortsHint()
        {
            var result = RingPresentation.Describe(Ring(RingProtectionStatus.Unresolved));
            Assert.AreEqual("Состояние STP части портов кольца не определено. Защиту определить нельзя.",
                result.ExplanationText);
        }

        [TestMethod]
        public void UnresolvedNeverSaysNotProtected()
        {
            var notProtected = UiText.Get("RingStatusUnprotected");
            foreach (var withoutStp in new[] { 0, 3 })
            {
                var result = RingPresentation.Describe(Ring(RingProtectionStatus.Unresolved, withoutStp: withoutStp));
                Assert.AreNotEqual("RingStatusUnprotected", result.StatusKey);
                Assert.AreNotEqual(notProtected, UiText.Get(result.StatusKey));
                Assert.IsFalse(result.ExplanationText.Contains(notProtected));
            }
        }

        [TestMethod]
        public void DegradedDistinguishesDisabledLinkFromSeveralBlockings()
        {
            var disabled = RingPresentation.Describe(Ring(RingProtectionStatus.Degraded, disabled: 1));
            Assert.AreEqual("Связь кольца отключена или оборвана: резерва по кольцу нет.", disabled.ExplanationText);

            var blocking = RingPresentation.Describe(Ring(RingProtectionStatus.Degraded, blocking: 2));
            Assert.AreEqual("STP заблокировал несколько связей кольца: резерва по кольцу нет.",
                blocking.ExplanationText);
        }

        [TestMethod]
        public void TitleUsesLocationOfMajority()
        {
            var ring = Ring(RingProtectionStatus.Protected, devices: 3);
            var title = RingPresentation.Title(ring, id => id == Id(1) ? "Цех 2" : "Цех 1");
            Assert.AreEqual("Кольцо — Цех 1", title);
        }

        [TestMethod]
        public void TitleTieBreaksByNameAndIgnoresEmptyLocations()
        {
            var ring = Ring(RingProtectionStatus.Protected, devices: 4);
            var title = RingPresentation.Title(ring, id =>
                id == Id(1) || id == Id(2) ? "Цех 2" : id == Id(3) || id == Id(4) ? "Цех 1" : null);
            Assert.AreEqual("Кольцо — Цех 1", title);

            var withEmpty = RingPresentation.Title(ring, id => id == Id(1) ? "Склад" : "  ");
            Assert.AreEqual("Кольцо — Склад", withEmpty);
        }

        [TestMethod]
        public void TitleWithoutLocationsShowsDeviceCount()
        {
            var ring = Ring(RingProtectionStatus.Protected, devices: 10);
            Assert.AreEqual("Кольцо: 10 устройств", RingPresentation.Title(ring, id => null));
        }

        [TestMethod]
        public void KindTextNamesCorePairRing()
        {
            Assert.AreEqual("Кольцо через пару ядер", RingPresentation.KindText(
                Ring(RingProtectionStatus.Protected, kind: PhysicalRedundancyRegionKind.CorePairRing)));
            Assert.AreEqual("Простое кольцо", RingPresentation.KindText(Ring(RingProtectionStatus.Protected)));
        }
    }
}
