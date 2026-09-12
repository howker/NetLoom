using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Wpf.Localization;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class UiLocalizationTests
    {
        [TestMethod]
        public void
            NeutralEnglishAndRussianSatelliteResolveByCurrentUiCulture()
        {
            using (new CultureScope(
                "en-US"))
            {
                Assert.AreEqual(
                    "Physical topology",
                    UiText.Get(
                        "MapTitle"));

                Assert.AreEqual(
                    "Unknown device",
                    UiText.Get(
                        "NodeUnknownLabel"));
            }

            using (new CultureScope(
                "ru-RU"))
            {
                Assert.AreEqual(
                    "Физическая топология",
                    UiText.Get(
                        "MapTitle"));

                Assert.AreEqual(
                    "Неизвестное устройство",
                    UiText.Get(
                        "NodeUnknownLabel"));
            }
        }

        [TestMethod]
        public void
            EnglishPluralizationUsesOneAndOther()
        {
            using (new CultureScope(
                "en-US"))
            {
                Assert.AreEqual(
                    "1 node",
                    UiText.FormatCount(
                        "MapNodeCount",
                        1));

                Assert.AreEqual(
                    "2 nodes",
                    UiText.FormatCount(
                        "MapNodeCount",
                        2));

                Assert.AreEqual(
                    "Found 1 candidate. Query: test",
                    UiText.FormatCount(
                        "LookupResultCount",
                        1,
                        "test"));

                Assert.AreEqual(
                    "Found 5 candidates. Query: test",
                    UiText.FormatCount(
                        "LookupResultCount",
                        5,
                        "test"));
            }
        }

        [TestMethod]
        public void
            RussianPluralizationHandlesOneFewAndMany()
        {
            using (new CultureScope(
                "ru-RU"))
            {
                Assert.AreEqual(
                    "1 узел",
                    UiText.FormatCount(
                        "MapNodeCount",
                        1));

                Assert.AreEqual(
                    "2 узла",
                    UiText.FormatCount(
                        "MapNodeCount",
                        2));

                Assert.AreEqual(
                    "5 узлов",
                    UiText.FormatCount(
                        "MapNodeCount",
                        5));

                Assert.AreEqual(
                    "11 узлов",
                    UiText.FormatCount(
                        "MapNodeCount",
                        11));

                Assert.AreEqual(
                    "21 узел",
                    UiText.FormatCount(
                        "MapNodeCount",
                        21));

                Assert.AreEqual(
                    "22 узла",
                    UiText.FormatCount(
                        "MapNodeCount",
                        22));

                Assert.AreEqual(
                    "25 узлов",
                    UiText.FormatCount(
                        "MapNodeCount",
                        25));
            }
        }

        private sealed class CultureScope :
            IDisposable
        {
            private readonly CultureInfo
                _originalCulture;

            private readonly CultureInfo
                _originalUiCulture;

            public CultureScope(
                string cultureName)
            {
                _originalCulture =
                    CultureInfo.CurrentCulture;

                _originalUiCulture =
                    CultureInfo.CurrentUICulture;

                var culture =
                    CultureInfo.GetCultureInfo(
                        cultureName);

                CultureInfo.CurrentCulture =
                    culture;

                CultureInfo.CurrentUICulture =
                    culture;
            }

            public void Dispose()
            {
                CultureInfo.CurrentCulture =
                    _originalCulture;

                CultureInfo.CurrentUICulture =
                    _originalUiCulture;
            }
        }
    }
}
