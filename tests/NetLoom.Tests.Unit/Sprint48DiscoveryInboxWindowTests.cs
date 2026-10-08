using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Application.DiscoveryInbox;
using NetLoom.Domain.Access;
using NetLoom.Wpf;
using NetLoom.Wpf.Discovery;
using NetLoom.Wpf.Localization;

namespace NetLoom.Tests.Unit
{
    public sealed partial class Sprint42WpfDiscoveryPanelTests
    {
        [TestMethod]
        public void SavedInboxShowsAllGroupsAndRetryButtons()
        {
            RunOnSta(() =>
            {
                var data = new Sprint48DiscoveryInboxFixture();
                var window = InboxWindow(data, data.Repository(), new RecordingDiscoveryControl());
                try
                {
                    window.Show();
                    Click((Button)window.FindName("ShellDiscoveryButton"));
                    PumpDispatcher();
                    window.UpdateLayout();
                    var list = (ItemsControl)window.FindName("DiscoveryInboxGroupsList");
                    CollectionAssert.AreEqual(Sprint48DiscoveryInboxFixture.Order,
                        list.Items.Cast<DiscoveryInboxGroup>().Select(group => group.Group).ToArray());
                    var rows = InboxRows(window);
                    Assert.AreEqual(14, rows.Length);
                    var error = rows.Single(row => row.Address == "10.48.228.50");
                    Assert.AreEqual(UiText.Format("DiscoveryCandidateErrorSummary", error.Name,
                        UiText.Get("DiscoveryErrorSnmpAuthentication"), data.Profile.Name,
                        data.Observed.ToLocalTime().ToString("t", CultureInfo.CurrentCulture)), error.Reason);
                    var buttons = InboxVisuals(list).OfType<Button>()
                        .Where(button => button.DataContext is DiscoveryInboxRow).ToArray();
                    Assert.AreEqual(rows.Length, buttons.Length);
                    foreach (var button in buttons)
                    {
                        var row = (DiscoveryInboxRow)button.DataContext;
                        Assert.AreEqual(row.CanRetry ? Visibility.Visible : Visibility.Collapsed, button.Visibility);
                        Assert.AreEqual(row.RetryAutomationName, AutomationProperties.GetName(button));
                        Assert.IsTrue(button.IsEnabled);
                    }
                    StringAssert.Contains(((TextBlock)window.FindName("DiscoveryInboxSummaryText")).Text,
                        UiText.Format("DiscoveryInboxSummaryUnchanged", 19));
                }
                finally { window.Close(); }
            });
        }

        [TestMethod]
        public void RetryUsesSingleAddressAndUpdatesSameRunWithoutChangingRange()
        {
            RunOnSta(() =>
            {
                var data = new Sprint48DiscoveryInboxFixture();
                var repository = data.Repository();
                var control = new RecordingDiscoveryControl();
                var window = InboxWindow(data, repository, control);
                try
                {
                    window.Show();
                    Click((Button)window.FindName("ShellDiscoveryButton"));
                    PumpDispatcher();
                    ((TextBox)window.FindName("DiscoveryStartAddressTextBox")).Text = "10.48.228.1";
                    ((TextBox)window.FindName("DiscoveryEndAddressTextBox")).Text = "10.48.228.254";
                    var mask = ((TextBox)window.FindName("DiscoverySubnetMaskTextBox")).Text;
                    var started = ((TextBlock)window.FindName("DiscoveryRunStartedValueText")).Text;
                    var checkedText = ((TextBlock)window.FindName("DiscoveryRunCheckedValueText")).Text;
                    var original = repository.GetRun(data.RunId);
                    var groupsList = (ItemsControl)window.FindName("DiscoveryInboxGroupsList");
                    InboxVisuals(groupsList).OfType<Expander>().Single(expander =>
                        (expander.DataContext as DiscoveryInboxGroup)?.Group == DiscoveryResultGroup.New)
                        .IsExpanded = false;
                    var button = InboxRetryButton(window, "10.48.228.50");
                    Assert.IsNotNull(button);
                    button.BringIntoView();
                    Click(button);
                    Assert.AreEqual("10.48.228.50", control.StartRequest.StartAddress);
                    Assert.AreEqual(control.StartRequest.StartAddress, control.StartRequest.EndAddress);
                    Assert.AreEqual("255.255.255.255", control.StartRequest.SubnetMask);
                    Assert.AreEqual(data.Profile.Id, control.StartRequest.AccessProfileId);
                    Assert.AreEqual(data.Profile.SnmpVersion, control.StartRequest.Version);
                    Assert.IsFalse(window.DiscoveryInboxCanRetry);
                    PumpDispatcher();
                    Assert.IsTrue(InboxVisuals((ItemsControl)window.FindName("DiscoveryInboxGroupsList"))
                        .OfType<Button>().Where(item => item.DataContext is DiscoveryInboxRow)
                        .All(item => !item.IsEnabled));
                    control.EmitCandidate(new DiscoveryCandidateSnapshot(IPAddress.Parse("10.48.228.50"),
                        data.Profile.Id, true, true, new[] { 22, 443 }, "kb-sw-50", "Cisco IOS 15.4",
                        "1.3.6.1.4.1.9", null, 24));
                    PumpDispatcher();
                    Assert.AreEqual(DiscoveryResultGroup.New,
                        InboxRows(window).Single(row => row.Address == "10.48.228.50").Group);
                    Assert.IsFalse(InboxVisuals(groupsList).OfType<Expander>().Single(expander =>
                        (expander.DataContext as DiscoveryInboxGroup)?.Group == DiscoveryResultGroup.New).IsExpanded);
                    control.PublishState(DiscoveryControlState.Completed, 1, 1, 1);
                    PumpDispatcher();
                    Assert.IsTrue(window.DiscoveryInboxCanRetry);
                    var updated = repository.GetRun(data.RunId);
                    Assert.AreEqual(original.Id, updated.Id);
                    Assert.AreEqual(original.StartedUtc, updated.StartedUtc);
                    Assert.AreEqual(original.FinishedUtc, updated.FinishedUtc);
                    Assert.AreEqual(original.TotalAddresses, updated.TotalAddresses);
                    Assert.AreEqual(original.ProcessedAddresses, updated.ProcessedAddresses);
                    Assert.AreEqual(2, updated.ErrorCount);
                    Assert.AreEqual(started, ((TextBlock)window.FindName("DiscoveryRunStartedValueText")).Text);
                    Assert.AreEqual(checkedText, ((TextBlock)window.FindName("DiscoveryRunCheckedValueText")).Text);
                    Assert.AreEqual("10.48.228.1", ((TextBox)window.FindName("DiscoveryStartAddressTextBox")).Text);
                    Assert.AreEqual("10.48.228.254", ((TextBox)window.FindName("DiscoveryEndAddressTextBox")).Text);
                    Assert.AreEqual(mask, ((TextBox)window.FindName("DiscoverySubnetMaskTextBox")).Text);
                    Assert.AreSame(InboxRetryButton(window, "10.48.228.50"), Keyboard.FocusedElement);
                }
                finally { window.Close(); }
            });
        }

        [TestMethod]
        public void RetryWithoutCandidateReturnsFocusToStartWhenRowDisappears()
        {
            RunOnSta(() =>
            {
                var data = new Sprint48DiscoveryInboxFixture();
                var repository = new InMemoryDiscoveryRunRepository();
                repository.SaveRun(data.Run);
                repository.SaveResult(data.Result(DiscoveryResultGroup.Error, "10.48.228.50",
                    snmp: false, error: NetLoom.Application.Snmp.SnmpTransportFailure.Timeout));
                var control = new RecordingDiscoveryControl();
                var window = InboxWindow(data, repository, control);
                try
                {
                    window.Show();
                    Click((Button)window.FindName("ShellDiscoveryButton"));
                    PumpDispatcher();
                    Click(InboxRetryButton(window, "10.48.228.50"));
                    control.PublishState(DiscoveryControlState.Completed, 1, 1, 0);
                    PumpDispatcher();
                    Assert.AreEqual(0, InboxRows(window).Length);
                    Assert.AreEqual(Visibility.Visible,
                        ((TextBlock)window.FindName("DiscoveryInboxEmptyText")).Visibility);
                    Assert.AreSame(window.FindName("DiscoveryStartButton"), Keyboard.FocusedElement);
                }
                finally { window.Close(); }
            });
        }

        private static MainWindow InboxWindow(Sprint48DiscoveryInboxFixture data,
            InMemoryDiscoveryRunRepository repository, RecordingDiscoveryControl control)
        {
            var journal = new DiscoveryRunJournal(repository, new EmptyDiscoveryTopologyReader(),
                new RecordingCandidateMaterializer(), new EmptyDiscoveryExclusionSource());
            var window = new MainWindow(new MutableRefreshProvider(EmptySnapshot()),
                new EmptyLookupReader(), new NoopMonitoringControl(), control, new[] { data.Profile }, journal);
            window.DiscoveryRunClock = () => data.Now;
            return window;
        }

        private static DiscoveryInboxRow[] InboxRows(MainWindow window)
        {
            return ((ItemsControl)window.FindName("DiscoveryInboxGroupsList")).Items
                .Cast<DiscoveryInboxGroup>().SelectMany(group => group.Rows).ToArray();
        }

        private static Button InboxRetryButton(MainWindow window, string address)
        {
            window.UpdateLayout();
            return InboxVisuals((ItemsControl)window.FindName("DiscoveryInboxGroupsList"))
                .OfType<Button>().SingleOrDefault(button =>
                    (button.DataContext as DiscoveryInboxRow)?.Address == address);
        }

        private static IEnumerable<DependencyObject> InboxVisuals(DependencyObject root)
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                yield return child;
                foreach (var descendant in InboxVisuals(child)) yield return descendant;
            }
        }
    }
}
