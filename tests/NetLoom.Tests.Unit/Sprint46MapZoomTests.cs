using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Wpf;

namespace NetLoom.Tests.Unit
{
    // P5 (полевая проверка Sprint 46): два быстрых «+» без выбранного устройства уводили карту из кадра.
    // Второе нажатие брало центр из прокрутки, которую первое ещё не выполнило (она отложена до Loaded).
    // Правило ADR-083: масштаб кнопками сохраняет центр текущего вида.
    public sealed partial class Sprint46LiveUiAuditTests
    {
        [TestMethod]
        [TestCategory("LiveUiAudit")]
        public void RapidZoomClicksKeepViewportCenter()
        {
            var repositoryRoot =
                FindRepositoryRoot();

            var sourceDatabase =
                Path.Combine(
                    repositoryRoot,
                    "artifacts",
                    "realistic-stand",
                    "operator-s46-pass3-visual.db");

            if (!File.Exists(
                    sourceDatabase))
            {
                Assert.Inconclusive(
                    "Live UI audit database is not available: " +
                    sourceDatabase);
            }

            var workDatabase =
                Path.Combine(
                    Path.GetTempPath(),
                    "netloom-zoom-audit-" +
                    Guid.NewGuid().ToString("N") +
                    ".db");

            File.Copy(
                sourceDatabase,
                workDatabase);

            try
            {
                RunOnSta(
                    () =>
                    {
                        var window =
                            CreateLiveWindow(
                                workDatabase,
                                false);

                        try
                        {
                            PrepareWindow(
                                window,
                                1440,
                                900);

                            var before =
                                ViewportLogicalCenter(
                                    window);

                            var visibleBefore =
                                VisibleDeviceBorders(
                                    window).Count();

                            var zoomIn =
                                (ButtonBase)window.FindName(
                                    "MapZoomInButton");

                            // Без Settle между нажатиями — как двойной щелчок оператора.
                            RaiseClick(zoomIn);
                            RaiseClick(zoomIn);
                            Settle(800);

                            var after =
                                ViewportLogicalCenter(
                                    window);

                            Assert.IsTrue(
                                visibleBefore > 0,
                                "Before zooming the map must show devices.");

                            Assert.IsTrue(
                                VisibleDeviceBorders(
                                    window).Any(),
                                "Two rapid zoom-in clicks moved every device out of the viewport.");

                            // Допуск — 2 логических пикселя: округление прокрутки до целых экранных.
                            Assert.AreEqual(
                                before.X,
                                after.X,
                                2.0,
                                "Horizontal center drifted after rapid zoom.");

                            Assert.AreEqual(
                                before.Y,
                                after.Y,
                                2.0,
                                "Vertical center drifted after rapid zoom.");
                        }
                        finally
                        {
                            window.Close();
                            Settle();
                        }
                    });
            }
            finally
            {
                TryDelete(workDatabase);
            }
        }

        private static System.Windows.Point ViewportLogicalCenter(
            MainWindow window)
        {
            var scroll =
                (ScrollViewer)window.FindName(
                    "MapScrollViewer");

            var zoom =
                (double)typeof(MainWindow)
                    .GetField(
                        "_zoom",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic)
                    .GetValue(window);

            return new System.Windows.Point(
                (scroll.HorizontalOffset +
                 (scroll.ViewportWidth / 2.0)) / zoom,
                (scroll.VerticalOffset +
                 (scroll.ViewportHeight / 2.0)) / zoom);
        }
    }
}
