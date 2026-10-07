using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.DiscoveryInbox;
using NetLoom.Wpf.Discovery;
using NetLoom.Wpf.Localization;

namespace NetLoom.Tests.Unit
{
    public sealed partial class Sprint42WpfDiscoveryPanelTests
    {
        [TestMethod]
        public void InboxAcceptUsesSelectedAddressesClearsSelectionAndRestoresFocus()
        {
            RunOnSta(() =>
            {
                // Обработчик действия делает await; тест нажимает кнопки из своего потока, поэтому ставит контекст диспетчера.
                System.Threading.SynchronizationContext.SetSynchronizationContext(
                    new System.Windows.Threading.DispatcherSynchronizationContext());
                var data = new Sprint48DiscoveryInboxFixture();
                var repository = Sprint48InboxActionFixture.Repository(data);
                var actions = new Sprint48RecordingInboxActions(repository);
                var window = InboxWindow(data, repository, new RecordingDiscoveryControl());
                window.DiscoveryInboxActions = actions;
                try
                {
                    window.Show();
                    Click((Button)window.FindName("ShellDiscoveryButton"));
                    PumpDispatcher();
                    var rows = InboxRows(window).Where(row => row.Group == DiscoveryResultGroup.New).Take(2).ToArray();
                    var list = (ItemsControl)window.FindName("DiscoveryInboxGroupsList");
                    foreach (var row in rows)
                    {
                        var check = InboxVisuals(list).OfType<CheckBox>().Single(item => item.DataContext == row);
                        Assert.AreEqual(row.SelectAutomationName, AutomationProperties.GetName(check));
                        check.IsChecked = true;
                    }
                    var accept = (Button)window.FindName("DiscoveryInboxAcceptButton");
                    Assert.IsTrue(accept.IsEnabled);
                    Assert.AreEqual(UiText.Format("DiscoveryInboxSelectedCount", 2),
                        ((TextBlock)window.FindName("DiscoveryInboxSelectedText")).Text);
                    accept.BringIntoView();
                    accept.Focus();
                    Click(accept);
                    WaitForCondition(() => ((TextBlock)window.FindName("DiscoveryMessageText")).Text ==
                        UiText.Format("DiscoveryInboxAppliedAccept", 2));
                    Assert.AreEqual(DiscoveryInboxAction.Accept, actions.LastAction);
                    Assert.AreEqual(data.RunId, actions.RunId);
                    Assert.AreEqual(data.Now, actions.NowUtc);
                    CollectionAssert.AreEquivalent(rows.Select(row => row.Address).ToArray(), actions.Addresses);
                    Assert.IsFalse(InboxRows(window).Any(row => row.IsSelected));
                    Assert.AreSame(accept, Keyboard.FocusedElement);
                    var select = InboxVisuals(list).OfType<CheckBox>().First(item =>
                        item.DataContext is DiscoveryInboxRow && item.IsVisible);
                    select.Focus();
                    PumpDispatcher();
                    Assert.IsFalse(accept.IsEnabled);
                }
                finally { window.Close(); }
            });
        }

        [TestMethod]
        public void InboxSelectAllOnlyChecksSelectableRowsAndActionsAreHiddenWithoutService()
        {
            RunOnSta(() =>
            {
                var data = new Sprint48DiscoveryInboxFixture();
                var repository = Sprint48InboxActionFixture.Repository(data);
                repository.SetResolution(data.RunId, "10.48.228.14", DiscoveryResultResolution.Accepted, data.Now);
                var window = InboxWindow(data, repository, new RecordingDiscoveryControl());
                try
                {
                    window.Show();
                    Click((Button)window.FindName("ShellDiscoveryButton"));
                    PumpDispatcher();
                    Assert.AreEqual(Visibility.Collapsed,
                        ((StackPanel)window.FindName("DiscoveryInboxActionsBar")).Visibility);
                    window.DiscoveryInboxActions = new Sprint48RecordingInboxActions(repository);
                    window.UpdateLayout();
                    Assert.AreEqual(Visibility.Visible,
                        ((StackPanel)window.FindName("DiscoveryInboxActionsBar")).Visibility);
                    var list = (ItemsControl)window.FindName("DiscoveryInboxGroupsList");
                    var group = list.Items.Cast<DiscoveryInboxGroup>().Single(item => item.Group == DiscoveryResultGroup.New);
                    var check = InboxVisuals(list).OfType<CheckBox>().Single(item => item.DataContext == group);
                    Assert.AreEqual(group.SelectAllAutomationName, AutomationProperties.GetName(check));
                    check.IsChecked = true;
                    Assert.IsTrue(group.Rows.Where(row => row.CanSelect).All(row => row.IsSelected));
                    Assert.IsTrue(group.Rows.Where(row => !row.CanSelect).All(row => !row.IsSelected));
                    Assert.AreEqual(3, group.Rows.Count(row => row.IsSelected));
                    check.IsChecked = false;
                    Assert.IsTrue(group.Rows.All(row => !row.IsSelected));
                    Assert.IsFalse(((Button)window.FindName("DiscoveryInboxAcceptButton")).IsEnabled);
                    window.DiscoveryInboxActions = null;
                    Assert.AreEqual(Visibility.Collapsed,
                        ((StackPanel)window.FindName("DiscoveryInboxActionsBar")).Visibility);
                }
                finally { window.Close(); }
            });
        }
    }
}
