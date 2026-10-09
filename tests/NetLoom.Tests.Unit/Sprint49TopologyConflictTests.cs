using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.MapLayout;
using NetLoom.Application.Topology;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Wpf;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Tests.Unit
{
    // Сценарии используют настоящую оболочку и её существующие STA-хелперы.
    public sealed partial class Sprint46ShellFoundationTests
    {
        [TestMethod]
        public void TopologyConflictLabelsRemainVisibleAtMinimumZoomAndInspectorShowsBothVersions()
        {
            WithTopologyConflictWindow(new ConflictAcknowledgementStore(), (window, snapshot) =>
            {
                var canvas = (Canvas)window.FindName("MapCanvas");
                var labels = canvas.Children.OfType<TextBlock>()
                    .Where(label => label.Tag is Guid).ToArray();
                Assert.AreEqual(2, labels.Length);
                foreach (var label in labels)
                {
                    Assert.IsTrue(label.Text.StartsWith("⚠ ", StringComparison.Ordinal));
                    Assert.AreEqual(((SolidColorBrush)window.FindResource("NetLoom.Brush.Warning")).Color,
                        ((SolidColorBrush)label.Foreground).Color);
                }
                ConflictSelect(window, Sprint49TopologyConflictFixture.ManualId);
                var blocks = (ItemsControl)window.FindName("InspectorTopologyConflicts");
                Assert.AreEqual(1, blocks.Items.Count);
                Assert.IsTrue(blocks.IsVisible);
                var text = ConfirmationTextBlocks(blocks).Select(item => item.Text)
                    .Concat(ConflictVisualTextBoxes(blocks).Select(item => item.Text)).ToArray();
                // Заголовок группы инспектора выводится заглавными (стиль SidebarSectionLabel, TextCase.Upper).
                Assert.IsTrue(text.Any(value => string.Equals(value, UiText.Get("TopologyConflictHeading"),
                    StringComparison.CurrentCultureIgnoreCase)));
                Assert.IsTrue(text.Any(value => value.StartsWith("Ручная:", StringComparison.Ordinal) &&
                    value.Contains("conflict-sw-b") && value.Contains("Gi0/2")));
                Assert.IsTrue(text.Any(value => value.StartsWith("Наблюдается:", StringComparison.Ordinal) &&
                    value.Contains("conflict-sw-c") && value.Contains("Gi0/5") && value.Contains("LLDP")));
                Assert.AreEqual(3, TopologyQualityVisualButtons(blocks).Count(button => button.IsVisible));

                typeof(MainWindow).GetField("_zoom", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(window, (double)window.FindResource("NetLoom.Map.ZoomMin"));
                typeof(MainWindow).GetMethod("UpdateSemanticMapVisibility", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(window, null);
                Assert.IsTrue(labels.All(label => label.Visibility == Visibility.Visible));
                Assert.IsTrue(labels.All(label => !label.Text.StartsWith("⚠ ⚠", StringComparison.Ordinal)));
                // На уровне «Издалека» подпись обратно масштабирована до экранного размера.
                var farZoom = (double)window.FindResource("NetLoom.Map.ZoomMin");
                foreach (var label in labels)
                {
                    var inverse = (ScaleTransform)label.RenderTransform;
                    Assert.AreEqual(1 / farZoom, inverse.ScaleX, 0.001);
                    Assert.IsTrue(label.Text.Contains(" ↔ "));
                }

                ConflictSelect(window, Sprint49TopologyConflictFixture.ObservedId);
                var toggle = (ToggleButton)window.FindName("MapQualityToggle");
                toggle.IsChecked = true;
                window.UpdateLayout();
                var items = (ItemsControl)window.FindName("MapQualityItems");
                var show = TopologyQualityVisualButtons(items).Single(button =>
                    ((TopologyQualityItem)button.Tag).Reasons.Any(reason =>
                        reason.Kind == TopologyQualityGapKind.ManualObservedConflict));
                var qualityItem = (TopologyQualityItem)show.Tag;
                Assert.AreEqual(Sprint49TopologyConflictFixture.ManualId, qualityItem.PhysicalLinkId);
                var conflictReason = qualityItem.Reasons.Single(value => value.Kind == TopologyQualityGapKind.ManualObservedConflict);
                var qualityTexts = ConfirmationTextBlocks(items).Select(value => value.Text).ToArray();
                CollectionAssert.Contains(qualityTexts, qualityItem.Subject);
                Assert.IsTrue(qualityTexts.Any(value => value.Contains(conflictReason.Text)));
                StringAssert.Contains(conflictReason.Text, "conflict-sw-b");
                StringAssert.Contains(conflictReason.Text, "Gi0/2");
                StringAssert.Contains(conflictReason.Text, "conflict-sw-c");
                StringAssert.Contains(conflictReason.Text, "Gi0/5");
                show.Focus();
                Click(show);
                Assert.AreEqual(Sprint49TopologyConflictFixture.ManualId, TopologyQualityField(window, "_selectedPhysicalLinkId"));
                Assert.AreEqual(false, toggle.IsChecked);
                Assert.AreEqual(Visibility.Collapsed, ((Border)window.FindName("MapQualityDetails")).Visibility);
                Assert.AreSame(toggle, Keyboard.FocusedElement);
            });
        }

        [TestMethod]
        public void TopologyConflictKeepManualStoresPairRemovesWarningsAndKeepsFocusInsideInspector()
        {
            var store = new ConflictAcknowledgementStore();
            WithTopologyConflictWindow(store, (window, snapshot) =>
            {
                ConflictSelect(window, Sprint49TopologyConflictFixture.ManualId);
                var blocks = (ItemsControl)window.FindName("InspectorTopologyConflicts");
                var button = TopologyQualityVisualButtons(blocks).Single(item =>
                    Equals(item.Content, UiText.Get("TopologyConflictKeepManual")));
                button.Focus();
                // Неизменившийся опрос сохраняет экземпляр кнопки и текущий фокус.
                typeof(MainWindow).GetMethod("ApplyTopologyRefresh", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(window, new object[] { snapshot });
                PumpDispatcher();
                Assert.AreSame(button, Keyboard.FocusedElement);
                Click(button);
                Assert.AreEqual(1, store.CallCount);
                CollectionAssert.AreEqual(new[] { new TopologyConflictKey(
                    Sprint49TopologyConflictFixture.ManualId, Sprint49TopologyConflictFixture.ObservedId) }, store.List().ToArray());
                Assert.AreEqual(DateTimeKind.Utc, store.AcknowledgedUtc.Kind);
                Assert.AreEqual(Visibility.Collapsed, blocks.Visibility);
                Assert.AreEqual(0, blocks.Items.Count);
                Assert.IsNotNull(Keyboard.FocusedElement);
                Assert.AreNotSame(window, Keyboard.FocusedElement);
                Assert.IsTrue(((TabControl)window.FindName("InspectorTabControl")).IsKeyboardFocusWithin);
                Assert.IsTrue(((Canvas)window.FindName("MapCanvas")).Children.OfType<TextBlock>()
                    .Where(label => label.Tag is Guid).All(label => !label.Text.StartsWith("⚠", StringComparison.Ordinal)));
                typeof(MainWindow).GetMethod("ApplyTopologyRefresh", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(window, new object[] { snapshot });
                Assert.AreEqual(Visibility.Collapsed, blocks.Visibility);
            });
        }

        [TestMethod]
        public void TopologyConflictOpenEvidenceSelectsObservedLinkAndEvidenceTab()
        {
            WithTopologyConflictWindow(new ConflictAcknowledgementStore(), (window, snapshot) =>
            {
                ConflictSelect(window, Sprint49TopologyConflictFixture.ManualId);
                Click(TopologyQualityVisualButtons((ItemsControl)window.FindName("InspectorTopologyConflicts"))
                    .Single(button => Equals(button.Content, UiText.Get("TopologyConflictOpenEvidence"))));
                Assert.AreEqual(Sprint49TopologyConflictFixture.ObservedId, TopologyQualityField(window, "_selectedPhysicalLinkId"));
                Assert.AreSame(window.FindName("InspectorEvidenceTab"),
                    ((TabControl)window.FindName("InspectorTabControl")).SelectedItem);
            });
        }

        [TestMethod]
        public void TopologyConflictChangeManualEntersEditModeAndOpensSelectedManualLink()
        {
            WithTopologyConflictWindow(new ConflictAcknowledgementStore(), (window, snapshot) =>
            {
                ConflictSelect(window, Sprint49TopologyConflictFixture.ObservedId);
                Click(TopologyQualityVisualButtons((ItemsControl)window.FindName("InspectorTopologyConflicts"))
                    .Single(button => Equals(button.Content, UiText.Get("TopologyConflictChangeManual"))));
                Assert.AreEqual("Edit", TopologyQualityField(window, "_mapInteractionMode").ToString());
                var host = (ContentControl)window.FindName("ShellWorkspaceEditorHost");
                Assert.IsTrue(host.IsVisible);
                var control = host.Content;
                Assert.AreEqual("ManualTopologyEditorControl", control.GetType().Name);
                var editor = (Window)control.GetType().GetField("_editor", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(control);
                var links = (ListBox)editor.FindName("ManualLinksList");
                Assert.IsNotNull(links.SelectedItem);
                var item = links.SelectedItem.GetType().GetProperty("Item").GetValue(links.SelectedItem);
                Assert.AreEqual(Sprint49TopologyConflictFixture.ManualId,
                    item.GetType().GetProperty("PhysicalLinkId").GetValue(item));
            });
        }

        [TestMethod]
        public void TopologyConflictWithoutStoreStillShowsConflictAndHidesKeepManual()
        {
            WithTopologyConflictWindow(null, (window, snapshot) =>
            {
                ConflictSelect(window, Sprint49TopologyConflictFixture.ManualId);
                var blocks = (ItemsControl)window.FindName("InspectorTopologyConflicts");
                Assert.AreEqual(1, blocks.Items.Count);
                var buttons = TopologyQualityVisualButtons(blocks).Where(button => button.IsVisible).ToArray();
                Assert.AreEqual(2, buttons.Length);
                Assert.IsFalse(buttons.Any(button => Equals(button.Content, UiText.Get("TopologyConflictKeepManual"))));
            });
        }

        [TestMethod]
        public void TopologyConflictHasOneInspectorBlockPerPairAndClearsOnDeviceSelection()
        {
            WithTopologyConflictWindow(new ConflictAcknowledgementStore(), (window, snapshot) =>
            {
                ConflictSelect(window, Sprint49TopologyConflictFixture.ManualId);
                Assert.AreEqual(2, ((ItemsControl)window.FindName("InspectorTopologyConflicts")).Items.Count);
                SelectDevice(window, Sprint49TopologyConflictFixture.A);
                Assert.AreEqual(Visibility.Collapsed, ((ItemsControl)window.FindName("InspectorTopologyConflicts")).Visibility);
            }, secondConflict: true);
        }

        private static void WithTopologyConflictWindow(ITopologyConflictAcknowledgementStore store,
            Action<MainWindow, TopologyRefreshSnapshot> action, bool secondConflict = false)
        {
            RunOnSta(() =>
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var snapshot = Sprint49TopologyConflictFixture.Snapshot(secondConflict);
                var layouts = new FixedViewportLayoutStore(new MapLayoutSnapshot(MapLayoutScope.PhysicalTopologyMapId,
                    new MapViewportLayout(1, 0, 0), new MapDeviceLayout[0], new MapLocationLayout[0]));
                var window = new MainWindow(new FixedRefreshProvider(snapshot), new EmptyLookupReader(), layouts,
                    new ConflictManualTopologyService(snapshot)) { TopologyConflictAcknowledgements = store };
                try
                {
                    window.Show();
                    WaitForCondition(() => DeviceBorder(window, Sprint49TopologyConflictFixture.A) != null &&
                        ((Canvas)window.FindName("MapCanvas")).Children.OfType<Line>()
                            .Any(line => Equals(line.Tag, Sprint49TopologyConflictFixture.ManualId)));
                    window.UpdateLayout();
                    action(window, snapshot);
                }
                finally { window.Close(); PumpDispatcher(); }
            });
        }

        private static void ConflictSelect(MainWindow window, Guid id)
        {
            var line = ((Canvas)window.FindName("MapCanvas")).Children.OfType<Line>()
                .Single(item => Equals(item.Tag, id));
            line.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = UIElement.MouseLeftButtonDownEvent, Source = line });
            PumpDispatcher();
            window.UpdateLayout();
        }

        private static IEnumerable<TextBox> ConflictVisualTextBoxes(DependencyObject root)
        {
            if (root is TextBox text) yield return text;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var child in ConflictVisualTextBoxes(VisualTreeHelper.GetChild(root, i))) yield return child;
        }

        private sealed class ConflictAcknowledgementStore : ITopologyConflictAcknowledgementStore
        {
            private readonly HashSet<TopologyConflictKey> _pairs = new HashSet<TopologyConflictKey>();
            public int CallCount { get; private set; }
            public DateTime AcknowledgedUtc { get; private set; }
            public IReadOnlyCollection<TopologyConflictKey> List() => _pairs.ToArray();
            public void Acknowledge(Guid manualLinkId, Guid observedLinkId, DateTime acknowledgedUtc)
            {
                CallCount++;
                AcknowledgedUtc = acknowledgedUtc;
                _pairs.Add(new TopologyConflictKey(manualLinkId, observedLinkId));
            }
        }

        private sealed class ConflictManualTopologyService : IManualTopologyService
        {
            private readonly ManualTopologyEditorSnapshot _snapshot;
            public ConflictManualTopologyService(TopologyRefreshSnapshot snapshot)
            {
                _snapshot = new ManualTopologyEditorSnapshot(snapshot.DiagnosticSnapshot.Devices.Select(device =>
                    new ManualTopologyDeviceItem(device.DeviceId, device.DisplayName, ManualTopologyDeviceCategory.Unknown, null, false)).ToArray(),
                    snapshot.DiagnosticSnapshot.Devices.SelectMany(device => device.Interfaces).Select(port =>
                    new ManualTopologyPortItem(port.InterfaceId, port.DeviceId, port.DisplayName, "Ethernet", false)).ToArray(),
                    snapshot.DiagnosticSnapshot.Links.Select(link => new ManualTopologyLinkItem(link.PhysicalLinkId,
                        link.DeviceAId, link.InterfaceAId, link.DeviceBId, link.InterfaceBId, "Ethernet", null,
                        link.Strength == NetLoom.Contracts.Diagnostics.DiagnosticLinkStrength.Manual)).ToArray());
            }
            public ManualTopologyEditorSnapshot GetSnapshot() => _snapshot;
            public Guid CreateDevice(string name, ManualTopologyDeviceCategory category, string notes) => throw new NotSupportedException();
            public void UpdateDevice(Guid deviceId, string name, ManualTopologyDeviceCategory category, string notes) => throw new NotSupportedException();
            public void DeleteDevice(Guid deviceId) => throw new NotSupportedException();
            public Guid CreatePort(Guid deviceId, string name, string mediaType) => throw new NotSupportedException();
            public void UpdatePort(Guid interfaceId, string name, string mediaType) => throw new NotSupportedException();
            public void DeletePort(Guid interfaceId) => throw new NotSupportedException();
            public Guid CreateLink(Guid deviceAId, Guid? interfaceAId, Guid deviceBId, Guid? interfaceBId, string mediaType, string notes) => throw new NotSupportedException();
            public void UpdateLink(Guid physicalLinkId, string mediaType, string notes) => throw new NotSupportedException();
            public void DeleteLink(Guid physicalLinkId) => throw new NotSupportedException();
        }
    }
}
