using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Wpf;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Tests.Unit
{
    public sealed partial class Sprint46ShellFoundationTests
    {
        [TestMethod]
        public void TopologyQualityLineGroupsObjectReasonsAndShowReturnsFocusToToggle()
        {
            WithTopologyQualityWindow(Sprint49TopologyQualityFixture.Snapshot(secondLink: true,
                secondStrength: DiagnosticLinkStrength.Inferred), window =>
            {
                var notice = (Border)window.FindName("MapQualityNotice");
                var summary = (TextBlock)window.FindName("MapQualitySummaryText");
                var toggle = (ToggleButton)window.FindName("MapQualityToggle");
                var details = (Border)window.FindName("MapQualityDetails");
                Assert.IsTrue(notice.IsVisible);
                Assert.AreEqual("Топология неполная: 3", summary.Text);
                Assert.AreEqual(summary.Text, AutomationProperties.GetName(toggle));
                Assert.AreEqual(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(summary));
                Assert.AreEqual(false, toggle.IsChecked);
                Assert.AreEqual(Visibility.Collapsed, details.Visibility);

                var peer = UIElementAutomationPeer.CreatePeerForElement(toggle);
                var pattern = peer.GetPattern(PatternInterface.Toggle) as IToggleProvider;
                Assert.IsNotNull(pattern);
                Assert.AreEqual(ToggleState.Off, pattern.ToggleState);
                pattern.Toggle();
                PumpDispatcher();
                window.UpdateLayout();
                Assert.AreEqual(ToggleState.On, pattern.ToggleState);
                Assert.IsTrue(details.IsVisible);
                var items = (ItemsControl)window.FindName("MapQualityItems");
                Assert.AreEqual(3, items.Items.Count);
                var report = (TopologyQualityReport)TopologyQualityField(window, "_topologyQualityReport");
                Assert.AreEqual(3, report.Count);
                Assert.AreEqual(4, report.Items.Sum(item => item.Reasons.Count));
                var texts = ConfirmationTextBlocks(items).Select(text => text.Text).ToArray();
                foreach (var item in report.Items)
                {
                    CollectionAssert.Contains(texts, item.Subject);
                    foreach (var reason in item.Reasons)
                        Assert.IsTrue(texts.Any(text => text.Contains(reason.Text)), reason.Text);
                }

                var buttons = TopologyQualityVisualButtons(items).ToArray();
                Assert.AreEqual(3, buttons.Length);
                var linkButton = buttons.Single(button => ((TopologyQualityItem)button.Tag).PhysicalLinkId ==
                    Sprint49TopologyQualityFixture.LinkAB);
                var linkItem = (TopologyQualityItem)linkButton.Tag;
                CollectionAssert.AreEquivalent(new[] { TopologyQualityGapKind.ObservedLink, TopologyQualityGapKind.OneSidedLldp },
                    linkItem.Reasons.Select(reason => reason.Kind).ToArray());
                linkButton.Focus();
                Click(linkButton);
                Assert.AreEqual(Sprint49TopologyQualityFixture.LinkAB,
                    TopologyQualityField(window, "_selectedPhysicalLinkId"));
                Assert.IsNull(TopologyQualityField(window, "_selectedDeviceId"));
                Assert.AreSame(toggle, Keyboard.FocusedElement);
                Assert.AreEqual(false, toggle.IsChecked);
                Assert.AreEqual(Visibility.Collapsed, details.Visibility);

                pattern.Toggle();
                PumpDispatcher();
                window.UpdateLayout();
                var deviceButton = TopologyQualityVisualButtons(items).Single(button =>
                    ((TopologyQualityItem)button.Tag).DeviceId == Sprint49TopologyQualityFixture.DeviceA);
                deviceButton.Focus();
                // Неизменившийся опрос сохраняет кнопку и фокус внутри раскрытой панели.
                typeof(MainWindow).GetMethod("ApplyTopologyRefresh", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(window, new object[] { Sprint49TopologyQualityFixture.Snapshot(secondLink: true,
                        secondStrength: DiagnosticLinkStrength.Inferred) });
                PumpDispatcher();
                Assert.AreSame(deviceButton, Keyboard.FocusedElement);
                Assert.AreEqual(true, toggle.IsChecked);

                Click(deviceButton);
                Assert.AreEqual(Sprint49TopologyQualityFixture.DeviceA,
                    TopologyQualityField(window, "_selectedDeviceId"));
                Assert.IsNull(TopologyQualityField(window, "_selectedPhysicalLinkId"));
                Assert.AreSame(toggle, Keyboard.FocusedElement);
                Assert.AreEqual(false, toggle.IsChecked);
                Assert.AreEqual(Visibility.Collapsed, details.Visibility);
            });
        }

        [TestMethod]
        [DataRow(1100)]
        [DataRow(1440)]
        public void TopologyQualityPanelOverlaysMapWithoutMovingOrShrinkingIt(int width)
        {
            WithTopologyQualityWindow(Sprint49TopologyQualityFixture.Snapshot(), window =>
            {
                window.Width = width;
                window.Height = 900;
                window.UpdateLayout();
                var map = (ScrollViewer)window.FindName("MapScrollViewer");
                var before = map.TransformToAncestor(window).TransformBounds(new Rect(map.RenderSize));
                var toggle = (ToggleButton)window.FindName("MapQualityToggle");
                var details = (Border)window.FindName("MapQualityDetails");
                toggle.IsChecked = true;
                PumpDispatcher();
                window.UpdateLayout();

                Assert.IsTrue(details.IsVisible);
                Assert.AreEqual(before, map.TransformToAncestor(window).TransformBounds(new Rect(map.RenderSize)));
                Assert.AreSame(System.Windows.Media.VisualTreeHelper.GetParent(map),
                    System.Windows.Media.VisualTreeHelper.GetParent(details));
                Assert.AreEqual(2, Grid.GetRow(details));
                Assert.AreEqual(Grid.GetRow(map), Grid.GetRow(details));
                Assert.IsTrue(Panel.GetZIndex(details) > Panel.GetZIndex(map));
                Assert.AreEqual(HorizontalAlignment.Left, details.HorizontalAlignment);
                Assert.AreEqual(VerticalAlignment.Top, details.VerticalAlignment);
                Assert.AreEqual((double)window.FindResource("NetLoom.Map.QualityPanelWidth"), details.Width);
                Assert.IsTrue(details.ActualWidth <= map.ActualWidth - details.Margin.Left - details.Margin.Right + 0.001);
                Assert.IsTrue(details.ActualHeight <= (double)window.FindResource("NetLoom.Map.QualityListMaxHeight") + 0.001);
                Assert.AreEqual((Thickness)window.FindResource("NetLoom.Thickness.BorderThin"), details.BorderThickness);
                Assert.AreEqual(((System.Windows.Media.SolidColorBrush)window.FindResource("NetLoom.Brush.Border")).Color,
                    ((System.Windows.Media.SolidColorBrush)details.BorderBrush).Color);
                var surface = details.Background as System.Windows.Media.SolidColorBrush;
                Assert.IsNotNull(surface);
                Assert.AreEqual((byte)255, surface.Color.A);
                Assert.AreEqual(1.0, surface.Opacity);
                Assert.AreEqual(1.0, details.Opacity);
                Assert.AreEqual(((System.Windows.Media.SolidColorBrush)window.FindResource("NetLoom.Brush.Surface")).Color,
                    surface.Color);
                var shadow = details.Effect as System.Windows.Media.Effects.DropShadowEffect;
                Assert.IsNotNull(shadow);
                Assert.AreEqual((double)window.FindResource("NetLoom.Map.OverlayShadowBlur"), shadow.BlurRadius);
                Assert.AreEqual((double)window.FindResource("NetLoom.Map.OverlayShadowDepth"), shadow.ShadowDepth);
                Assert.AreEqual((double)window.FindResource("NetLoom.Map.OverlayShadowOpacity"), shadow.Opacity);
                Assert.AreEqual((System.Windows.Media.Color)window.FindResource("NetLoom.Color.OverlayShadow"), shadow.Color);

                toggle.IsChecked = false;
                window.UpdateLayout();
                Assert.AreEqual(Visibility.Collapsed, details.Visibility);
                Assert.AreEqual(before, map.TransformToAncestor(window).TransformBounds(new Rect(map.RenderSize)));
            });
        }

        [TestMethod]
        public void TopologyQualityEscapeClosesPanelReturnsFocusAndPreservesEditMode()
        {
            WithTopologyQualityWindow(Sprint49TopologyQualityFixture.Snapshot(), window =>
            {
                Click((Button)window.FindName("MapEditModeButton"));
                Assert.AreEqual("Edit", TopologyQualityField(window, "_mapInteractionMode").ToString());
                var toggle = (ToggleButton)window.FindName("MapQualityToggle");
                var details = (Border)window.FindName("MapQualityDetails");
                toggle.IsChecked = true;
                PumpDispatcher();
                window.UpdateLayout();
                var button = TopologyQualityVisualButtons((ItemsControl)window.FindName("MapQualityItems")).First();
                button.Focus();
                Assert.IsTrue(details.IsKeyboardFocusWithin);
                var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window),
                    Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                button.RaiseEvent(key);
                PumpDispatcher();
                window.UpdateLayout();

                Assert.IsTrue(key.Handled);
                Assert.AreEqual(false, toggle.IsChecked);
                Assert.AreEqual(Visibility.Collapsed, details.Visibility);
                Assert.AreSame(toggle, Keyboard.FocusedElement);
                Assert.AreEqual("Edit", TopologyQualityField(window, "_mapInteractionMode").ToString());
            });
        }

        [TestMethod]
        public void TopologyQualityPanelClosesOnlyForMapClickAndLeavesClickUnhandled()
        {
            WithTopologyQualityWindow(Sprint49TopologyQualityFixture.Snapshot(), window =>
            {
                var toggle = (ToggleButton)window.FindName("MapQualityToggle");
                var details = (Border)window.FindName("MapQualityDetails");
                toggle.IsChecked = true;
                PumpDispatcher();
                window.UpdateLayout();

                var inside = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                { RoutedEvent = Mouse.PreviewMouseDownEvent };
                details.RaiseEvent(inside);
                Assert.AreEqual(true, toggle.IsChecked);
                Assert.IsTrue(details.IsVisible);

                var map = (ScrollViewer)window.FindName("MapScrollViewer");
                var outside = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                { RoutedEvent = Mouse.PreviewMouseDownEvent };
                map.RaiseEvent(outside);
                PumpDispatcher();
                window.UpdateLayout();
                Assert.AreEqual(false, toggle.IsChecked);
                Assert.AreEqual(Visibility.Collapsed, details.Visibility);
                Assert.IsFalse(outside.Handled);
            });
        }

        [TestMethod]
        public void TopologyQualityLineIsHiddenForCompleteTopology()
        {
            WithTopologyQualityWindow(Sprint49TopologyQualityFixture.Snapshot(
                DiagnosticLinkStrength.Confirmed, DiagnosticLldpReporting.BothSides, synthetic: false), window =>
            {
                Assert.AreEqual(Visibility.Collapsed, ((Border)window.FindName("MapQualityNotice")).Visibility);
                Assert.AreEqual(0, ((ItemsControl)window.FindName("MapQualityItems")).Items.Count);
            });
        }

        [TestMethod]
        public void TopologyQualityLineIsHiddenOutsideMapAndReturnsCollapsed()
        {
            WithTopologyQualityWindow(Sprint49TopologyQualityFixture.Snapshot(), window =>
            {
                var notice = (Border)window.FindName("MapQualityNotice");
                var toggle = (ToggleButton)window.FindName("MapQualityToggle");
                toggle.IsChecked = true;
                Click((Button)window.FindName("ShellAlertsButton"));
                Assert.AreEqual(Visibility.Visible, ((Grid)window.FindName("ShellMapSurface")).Visibility);
                Assert.AreEqual(Visibility.Collapsed, notice.Visibility);
                Assert.AreEqual(false, toggle.IsChecked);
                Click((Button)window.FindName("ShellEquipmentButton"));
                Assert.AreEqual(Visibility.Collapsed, notice.Visibility);
                Click((Button)window.FindName("ShellMapButton"));
                Assert.IsTrue(notice.IsVisible);
                Assert.AreEqual(false, toggle.IsChecked);
            });
        }

        [TestMethod]
        public void TopologyQualityLineRecalculatesOnRefreshWithoutReappearingInAlerts()
        {
            WithTopologyQualityWindow(Sprint49TopologyQualityFixture.Snapshot(), window =>
            {
                var notice = (Border)window.FindName("MapQualityNotice");
                var method = typeof(MainWindow).GetMethod("ApplyTopologyRefresh",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(method);
                var complete = Sprint49TopologyQualityFixture.Snapshot(DiagnosticLinkStrength.Confirmed,
                    DiagnosticLldpReporting.BothSides, synthetic: false);
                method.Invoke(window, new object[] { complete });
                PumpDispatcher();
                Assert.AreEqual(Visibility.Collapsed, notice.Visibility);
                Click((Button)window.FindName("ShellAlertsButton"));
                method.Invoke(window, new object[] { Sprint49TopologyQualityFixture.Snapshot() });
                PumpDispatcher();
                Assert.AreEqual(Visibility.Collapsed, notice.Visibility);
                Click((Button)window.FindName("ShellMapButton"));
                Assert.IsTrue(notice.IsVisible);
                Assert.AreEqual("Топология неполная: 2", ((TextBlock)window.FindName("MapQualitySummaryText")).Text);
            });
        }

        private static void WithTopologyQualityWindow(TopologyRefreshSnapshot snapshot, Action<MainWindow> action)
        {
            RunOnSta(() =>
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var window = new MainWindow(new FixedRefreshProvider(snapshot), new EmptyLookupReader());
                try
                {
                    window.Show();
                    WaitForCondition(() => DeviceBorder(window, Sprint49TopologyQualityFixture.DeviceA) != null &&
                        ((TextBlock)window.FindName("MapQualitySummaryText")).Text ==
                        UiText.Format("MapQualitySummary", TopologyQualityProjection.Build(
                            snapshot.DiagnosticSnapshot, snapshot.MapSnapshot).Count));
                    window.UpdateLayout();
                    action(window);
                }
                finally
                {
                    window.Close();
                    PumpDispatcher();
                }
            });
        }

        private static object TopologyQualityField(MainWindow window, string name)
        {
            return typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
        }

        private static System.Collections.Generic.IEnumerable<Button> TopologyQualityVisualButtons(DependencyObject root)
        {
            if (root is Button button) yield return button;
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var child in TopologyQualityVisualButtons(System.Windows.Media.VisualTreeHelper.GetChild(root, i)))
                    yield return child;
        }
    }
}
