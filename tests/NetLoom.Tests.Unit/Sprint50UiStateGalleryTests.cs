using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.Rings;
using NetLoom.Wpf;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Tests.Unit
{
    public sealed partial class Sprint46UiStateGalleryTests
    {
        [TestMethod]
        public void FailurePredictionGallery()
        {
            var source = System.IO.Path.Combine(FindParallelLinksRepositoryRoot(),
                "artifacts", "realistic-stand", "field-s46.db");
            if (!File.Exists(source))
                Assert.Inconclusive("Field stand is not built: run TestCategory=StandBuilder first.");
            RunOnSta(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var output = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(ResolveOutputDirectory()), "sprint50-impact");
                Directory.CreateDirectory(output);
                foreach (var file in Directory.GetFiles(output, "*.png")) File.Delete(file);
                var findings = new List<string>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                foreach (var scenario in new[] { "75-impact-bypass", "76-impact-single-path",
                    "77-impact-not-connected" })
                foreach (var dark in new[] { false, true })
                foreach (var width in new[] { 1100, 1440 })
                {
                    var database = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                        "netloom-s50-impact-" + Guid.NewGuid().ToString("N") + ".db");
                    MainWindow window = null;
                    File.Copy(source, database);
                    try
                    {
                        window = CreateParallelLinksFieldWindow(database, dark, withoutSavedView: true);
                        PrepareWindow(window, width, GalleryHeight);
                        var current = window;
                        WaitForCondition(() =>
                        {
                            var snapshot = (NetworkDiagnosticSnapshot)typeof(MainWindow)
                                .GetField("_lastDiagnosticSnapshot", flags).GetValue(current);
                            return snapshot != null && snapshot.Devices.Count > 0 && current.PollingPoint != null;
                        });
                        var diagnostics = (NetworkDiagnosticSnapshot)typeof(MainWindow)
                            .GetField("_lastDiagnosticSnapshot", flags).GetValue(window);
                        var polling = diagnostics.Devices.FirstOrDefault(device =>
                            device.DisplayName.StartsWith("ps1-sw-01", StringComparison.OrdinalIgnoreCase));
                        var deviceName = scenario == "75-impact-bypass" ? "ps1-sw-03" :
                            scenario == "76-impact-single-path" ? "core-sw-01" : "kb-sw-02";
                        var selected = diagnostics.Devices.FirstOrDefault(device =>
                            device.DisplayName.StartsWith(deviceName, StringComparison.OrdinalIgnoreCase));
                        if (polling == null || selected == null)
                        {
                            findings.Add(scenario + " — Устройство не найдено на полевом стенде.");
                            continue;
                        }
                        typeof(MainWindow).GetField("_pollingPoint", flags).SetValue(window,
                            new EnginePollingPointResult(EnginePollingPointStatus.Determined, polling.DeviceId));
                        SelectDevice(window, selected.DeviceId);
                        PumpDispatcher();
                        window.UpdateLayout();
                        var inspector = (FrameworkElement)window.FindName("ShellInspectorPanel");
                        SaveRingPng(CaptureScaledRing(inspector, 2.0), System.IO.Path.Combine(output,
                            scenario + "-inspector-" + width + "-" + (dark ? "dark" : "light") + ".png"));
                        var show = (Button)window.FindName("InspectorFailurePredictionShowButton");
                        if (scenario == "76-impact-single-path" && show.Visibility != Visibility.Visible)
                            findings.Add(scenario + " — На стенде нет направленного прогноза для " + deviceName);
                        if (scenario != "77-impact-not-connected" && show.Visibility == Visibility.Visible) Click(show);
                        PumpDispatcher();
                        window.UpdateLayout();
                        CollectTextClipping(window.Content as DependencyObject, scenario, findings);
                        SaveRingPng(Capture(window.Content as FrameworkElement), System.IO.Path.Combine(output,
                            scenario + "-map-" + width + "-" + (dark ? "dark" : "light") + ".png"));
                    }
                    finally
                    {
                        if (window != null) window.Close();
                        PumpDispatcher();
                        DeleteParallelLinksFieldCopy(database);
                    }
                }
                File.WriteAllLines(System.IO.Path.Combine(output, "findings.txt"),
                    findings.Count == 0 ? new[] { "Находок нет." } : findings.ToArray(), new UTF8Encoding(false));
                Assert.AreEqual(0, findings.Count, string.Join(Environment.NewLine, findings));
                Assert.AreEqual(24, Directory.GetFiles(output, "*.png").Length);
            });
        }

        // Sprint 50: вид кольца на полевом стенде. Кадры: простое кольцо ПС-2 (после обрыва участка),
        // Кольцо ПС-1 через пару ядер и инспектор кольца крупно; две темы, ширины 1100 и 1440.
        [TestMethod]
        public void RingViewGallery()
        {
            var source = System.IO.Path.Combine(FindParallelLinksRepositoryRoot(),
                "artifacts", "realistic-stand", "field-s46.db");
            if (!File.Exists(source))
                Assert.Inconclusive("Field stand is not built: run TestCategory=StandBuilder first.");

            RunOnSta(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var output = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(ResolveOutputDirectory()), "sprint50-rings");
                Directory.CreateDirectory(output);
                foreach (var file in Directory.GetFiles(output, "*.png")) File.Delete(file);
                var findings = new List<string>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var scenarios = new[] { "70-ring-simple", "71-ring-core-pair", "72-ring-inspector" };

                try
                {
                    foreach (var scenario in scenarios)
                    foreach (var dark in new[] { false, true })
                    foreach (var width in new[] { 1100, 1440 })
                    {
                        var theme = dark ? "dark" : "light";
                        var context = scenario + "/" + theme + "/" + width;
                        var database = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                            "netloom-s50-rings-" + Guid.NewGuid().ToString("N") + ".db");
                        MainWindow window = null;
                        File.Copy(source, database);
                        try
                        {
                            window = CreateParallelLinksFieldWindow(database, dark, withoutSavedView: true);
                            PrepareWindow(window, width, GalleryHeight);
                            var current = window;
                            WaitForCondition(() =>
                            {
                                var diagnostics = (NetworkDiagnosticSnapshot)typeof(MainWindow)
                                    .GetField("_lastDiagnosticSnapshot", flags).GetValue(current);
                                return diagnostics != null && diagnostics.Rings.Count > 0;
                            });
                            PumpDispatcher();

                            var wantCore = scenario == "71-ring-core-pair";
                            var ring = FindFieldRing(window, wantCore);
                            if (ring == null)
                            {
                                findings.Add(context + " — На стенде нет " +
                                    (wantCore ? "кольца ПС-1 через пару ядер" : "простого кольца ПС-2"));
                                continue;
                            }
                            var expectedStatus = wantCore ? RingProtectionStatus.Protected : RingProtectionStatus.Degraded;
                            if (ring.Status != expectedStatus)
                                findings.Add(context + " — Состояние кольца " + ring.Status + ", ожидалось " + expectedStatus);

                            var label = (string)typeof(MainWindow).GetMethod("RingLabelText", flags)
                                .Invoke(window, new object[] { ring });
                            var show = (Button)window.FindName("MapOperationalFocusButton");
                            show.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            var rings = show.ContextMenu.Items.OfType<MenuItem>()
                                .Single(item => AutomationProperties.GetName(item) == UiText.Get("RingMenu"));
                            show.ContextMenu.IsOpen = false;
                            rings.Items.OfType<MenuItem>().Single(item => Equals(item.Header, label))
                                .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                            PumpDispatcher();
                            window.UpdateLayout();
                            Assert.AreEqual(ring.RingKey, typeof(MainWindow).GetField("_selectedRingKey", flags).GetValue(window));
                            PumpDispatcher();
                            window.UpdateLayout();

                            CollectTextClipping(window.Content as DependencyObject, context, findings);
                            var bitmap = scenario == "72-ring-inspector"
                                ? CaptureScaledRing((FrameworkElement)window.FindName("ShellInspectorPanel"), 2.0)
                                : Capture(window.Content as FrameworkElement);
                            SaveRingPng(bitmap, System.IO.Path.Combine(output,
                                scenario + "-" + width + "-" + theme + ".png"));
                        }
                        finally
                        {
                            if (window != null) window.Close();
                            PumpDispatcher();
                            DeleteParallelLinksFieldCopy(database);
                        }
                    }
                }
                finally
                {
                    File.WriteAllLines(System.IO.Path.Combine(output, "findings.txt"),
                        findings.Count == 0 ? new[] { "Находок нет." } : findings.ToArray(), new UTF8Encoding(false));
                }
                Assert.AreEqual(0, findings.Count, string.Join(Environment.NewLine, findings));
                Assert.AreEqual(12, Directory.GetFiles(output, "*.png").Length);
            });
        }

        // Кольцо ПС-1 (через пару ядер) или ПС-2 (простое) по составу: коммутаторы ps1-sw и ps2-sw полевого стенда.
        private static RingDiagnostic FindFieldRing(MainWindow window, bool corePair)
        {
            var diagnostics = (NetworkDiagnosticSnapshot)typeof(MainWindow)
                .GetField("_lastDiagnosticSnapshot", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
            var names = diagnostics.Devices.ToDictionary(device => device.DeviceId, device => device.DisplayName ?? string.Empty);
            return diagnostics.Rings.FirstOrDefault(ring =>
                ring.Kind == (corePair ? PhysicalRedundancyRegionKind.CorePairRing : PhysicalRedundancyRegionKind.SimpleRing) &&
                ring.DeviceIds.Any(id => names.ContainsKey(id) &&
                    names[id].StartsWith(corePair ? "ps1-sw" : "ps2-sw", StringComparison.Ordinal)));
        }

        // Снимок элемента в увеличенном масштабе: инспектор кольца крупно.
        private static BitmapSource CaptureScaledRing(FrameworkElement element, double scale)
        {
            Assert.IsNotNull(element, "Gallery visual is unavailable.");
            element.UpdateLayout();
            // Элемент рисуется через кисть: Render(element) сохраняет его смещение в окне,
            // И инспектор у правого края уходил за пределы кадра (пустой снимок).
            var bounds = new Rect(0, 0, element.ActualWidth, element.ActualHeight);
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                context.PushTransform(new ScaleTransform(scale, scale));
                context.DrawRectangle(new VisualBrush(element) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null, bounds);
            }
            var bitmap = new RenderTargetBitmap(
                Math.Max(1, (int)Math.Ceiling(element.ActualWidth * scale)),
                Math.Max(1, (int)Math.Ceiling(element.ActualHeight * scale)),
                96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        private static void SaveRingPng(BitmapSource bitmap, string path)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path))
            {
                encoder.Save(stream);
            }
        }
    }
}
