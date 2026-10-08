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
        public void TopologyQualityLineExpandsAndShowsLinkAndDeviceWithoutLeavingList()
        {
            WithTopologyQualityWindow(Sprint49TopologyQualityFixture.Snapshot(secondLink: true,
                secondStrength: DiagnosticLinkStrength.Inferred), window =>
            {
                var notice = (Border)window.FindName("MapQualityNotice");
                var summary = (TextBlock)window.FindName("MapQualitySummaryText");
                var toggle = (ToggleButton)window.FindName("MapQualityToggle");
                var details = (ScrollViewer)window.FindName("MapQualityDetails");
                Assert.IsTrue(notice.IsVisible);
                Assert.AreEqual("Топология неполная: 4", summary.Text);
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
                Assert.AreEqual((double)window.FindResource("NetLoom.Map.QualityListMaxHeight"), details.MaxHeight);
                var groups = (ItemsControl)window.FindName("MapQualityGroups");
                Assert.AreEqual(4, groups.Items.Count);
                var headings = ConfirmationTextBlocks(groups).Select(text => text.Text).ToArray();
                CollectionAssert.Contains(headings, UiText.Format("MapQualityObservedGroup", 1));
                CollectionAssert.Contains(headings, UiText.Format("MapQualityInferredGroup", 1));
                CollectionAssert.Contains(headings, UiText.Format("MapQualityOneSidedGroup", 1));
                CollectionAssert.Contains(headings, UiText.Format("MapQualitySyntheticGroup", 1));

                var buttons = TopologyQualityVisualButtons(groups).ToArray();
                Assert.AreEqual(4, buttons.Length);
                var linkButton = buttons.First(button => ((TopologyQualityGap)button.Tag).Kind ==
                    TopologyQualityGapKind.ObservedLink);
                linkButton.Focus();
                Click(linkButton);
                Assert.AreEqual(Sprint49TopologyQualityFixture.LinkAB,
                    TopologyQualityField(window, "_selectedPhysicalLinkId"));
                Assert.IsNull(TopologyQualityField(window, "_selectedDeviceId"));
                Assert.AreSame(linkButton, Keyboard.FocusedElement);
                Assert.AreEqual(true, toggle.IsChecked);

                var deviceButton = buttons.Single(button => ((TopologyQualityGap)button.Tag).Kind ==
                    TopologyQualityGapKind.SyntheticInterface);
                deviceButton.Focus();
                Click(deviceButton);
                Assert.AreEqual(Sprint49TopologyQualityFixture.DeviceA,
                    TopologyQualityField(window, "_selectedDeviceId"));
                Assert.IsNull(TopologyQualityField(window, "_selectedPhysicalLinkId"));
                Assert.AreSame(deviceButton, Keyboard.FocusedElement);

                typeof(MainWindow).GetMethod("ApplyTopologyRefresh", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(window, new object[] { Sprint49TopologyQualityFixture.Snapshot(secondLink: true,
                        secondStrength: DiagnosticLinkStrength.Inferred) });
                PumpDispatcher();
                Assert.AreSame(deviceButton, Keyboard.FocusedElement);
                Assert.AreEqual(true, toggle.IsChecked);
            });
        }

        [TestMethod]
        public void TopologyQualityLineIsHiddenForCompleteTopology()
        {
            WithTopologyQualityWindow(Sprint49TopologyQualityFixture.Snapshot(
                DiagnosticLinkStrength.Confirmed, DiagnosticLldpReporting.BothSides, synthetic: false), window =>
            {
                Assert.AreEqual(Visibility.Collapsed, ((Border)window.FindName("MapQualityNotice")).Visibility);
                Assert.AreEqual(0, ((ItemsControl)window.FindName("MapQualityGroups")).Items.Count);
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
                Assert.AreEqual("Топология неполная: 3", ((TextBlock)window.FindName("MapQualitySummaryText")).Text);
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
