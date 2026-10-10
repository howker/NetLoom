using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Discovery;
using NetLoom.Application.Snmp;
using NetLoom.Wpf;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.Shell;

namespace NetLoom.Tests.Unit
{
    public sealed partial class Sprint42WpfDiscoveryPanelTests
    {
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ProfileCheckDisplaysSixRowsWithoutSavingSelectingOrShowingCommunity(bool editing)
        {
            RunOnSta(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var data = new Sprint48DiscoveryInboxFixture();
                var window = InboxWindow(data, data.Repository(), new RecordingDiscoveryControl());
                var completion = new TaskCompletionSource<SnmpProfileCheckReport>();
                MainWindow.DiscoveryProfileCheckRequestedEventArgs request = null;
                var saveCalls = 0;
                window.DiscoveryProfileCreateRequested += (sender, args) => saveCalls++;
                window.DiscoveryProfileUpdateRequested += (sender, args) => saveCalls++;
                window.DiscoveryProfileCheckRequested += (sender, args) => { request = args; args.Result = completion.Task; };
                try
                {
                    window.Show();
                    PumpDispatcher();
                    ((TextBox)window.FindName("DiscoveryStartAddressTextBox")).Text = "192.0.2.1";
                    var selector = (ComboBox)window.FindName("DiscoveryProfileComboBox");
                    var selected = selector.SelectedItem;
                    Sprint48ProfileCheckDialogFixture.Open(window, editing ? data.Profile : null, dialog =>
                    {
                        var address = Sprint48ProfileCheckDialogFixture.Named<TextBox>(dialog, "DiscoveryProfileCheckAddress");
                        Assert.AreEqual("192.0.2.1", address.Text);
                        Sprint48ProfileCheckDialogFixture.Visuals(dialog).OfType<PasswordBox>().Single(box => box.IsVisible).Password =
                            Sprint48ProfileCheckDialogFixture.Community;
                        var check = Sprint48ProfileCheckDialogFixture.Named<Button>(dialog, "DiscoveryProfileCheckButton");
                        Click(check);
                        Assert.IsFalse(check.IsEnabled);
                        Assert.IsNotNull(request);
                        Assert.AreEqual(editing ? data.Profile.Id : (Guid?)null, request.ProfileId);
                        Assert.AreEqual("192.0.2.1", request.Address.ToString());
                        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(Sprint48ProfileCheckDialogFixture.Community), request.CommunityUtf8);
                        var progress = Sprint48ProfileCheckDialogFixture.Visuals(dialog).OfType<TextBlock>()
                            .Single(text => text.Text == UiText.Get("DiscoveryProfileChecking"));
                        Assert.IsTrue(LiveRegion.GetIsPolite(progress));
                        completion.SetResult(Sprint48ProfileCheckDialogFixture.Report());
                        var rows = Sprint48ProfileCheckDialogFixture.Named<StackPanel>(dialog, "DiscoveryProfileCheckRows");
                        WaitForCondition(() => rows.Children.Count == 6);
                        CollectionAssert.AreEqual(new[]
                        {
                            UiText.Format("DiscoveryProfileCheckMilliseconds", 14), string.Empty,
                            UiText.FormatCount("DiscoveryProfileCheckInterfaces", 52),
                            UiText.FormatCount("DiscoveryProfileCheckNeighbors", 18), string.Empty,
                            UiText.Get("DiscoveryProfileCheckPartial")
                        }, rows.Children.Cast<Grid>().Select(row => row.Children.OfType<TextBlock>().Last().Text).ToArray());
                        Assert.IsTrue(Sprint48ProfileCheckDialogFixture.Visuals(dialog).OfType<TextBlock>()
                            .All(text => !text.Text.Contains(Sprint48ProfileCheckDialogFixture.Community)));
                        Assert.IsTrue(request.CommunityUtf8.All(value => value == 0));
                        Assert.IsTrue(check.IsEnabled);
                        Assert.AreEqual(0, saveCalls);
                        Assert.AreSame(selected, selector.SelectedItem);
                    });
                }
                finally { window.Close(); }
            });
        }

        [TestMethod]
        public void InvalidProfileCheckAddressDoesNotCallHandlerAndShowsInlineError()
        {
            RunOnSta(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var data = new Sprint48DiscoveryInboxFixture();
                var window = InboxWindow(data, data.Repository(), new RecordingDiscoveryControl());
                var calls = 0;
                window.DiscoveryProfileCreateRequested += (sender, args) => Assert.Fail("Checking must not save.");
                window.DiscoveryProfileCheckRequested += (sender, args) => calls++;
                try
                {
                    window.Show();
                    PumpDispatcher();
                    Sprint48ProfileCheckDialogFixture.Open(window, null, dialog =>
                    {
                        var address = Sprint48ProfileCheckDialogFixture.Named<TextBox>(dialog, "DiscoveryProfileCheckAddress");
                        address.Text = "invalid-address";
                        Click(Sprint48ProfileCheckDialogFixture.Named<Button>(dialog, "DiscoveryProfileCheckButton"));
                        Assert.AreEqual(0, calls);
                        Assert.IsTrue(Sprint48ProfileCheckDialogFixture.Visuals(dialog).OfType<TextBlock>()
                            .Any(text => text.IsVisible && text.Text == UiText.Get("DiscoveryProfileCheckAddressInvalid")));
                    });
                }
                finally { window.Close(); }
            });
        }

        [TestMethod]
        public void EditingProfileWithBlankCommunityPassesIdAndShowsAuthenticationFailure()
        {
            RunOnSta(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var data = new Sprint48DiscoveryInboxFixture();
                var window = InboxWindow(data, data.Repository(), new RecordingDiscoveryControl());
                window.DiscoveryProfileUpdateRequested += (sender, args) => Assert.Fail("Checking must not save.");
                window.DiscoveryProfileCheckRequested += (sender, args) =>
                {
                    Assert.IsNull(args.CommunityUtf8);
                    Assert.AreEqual(data.Profile.Id, args.ProfileId);
                    args.Result = Task.FromResult(Sprint48ProfileCheckDialogFixture.Report(true));
                };
                try
                {
                    window.Show();
                    PumpDispatcher();
                    ((TextBox)window.FindName("DiscoveryStartAddressTextBox")).Text = "192.0.2.1";
                    Sprint48ProfileCheckDialogFixture.Open(window, data.Profile, dialog =>
                    {
                        Click(Sprint48ProfileCheckDialogFixture.Named<Button>(dialog, "DiscoveryProfileCheckButton"));
                        var rows = Sprint48ProfileCheckDialogFixture.Named<StackPanel>(dialog, "DiscoveryProfileCheckRows");
                        Assert.AreEqual(6, rows.Children.Count);
                        var details = rows.Children.Cast<Grid>().Select(row => row.Children.OfType<TextBlock>().Last().Text).ToArray();
                        Assert.AreEqual(UiText.Get("DiscoveryErrorSnmpAuthentication"), details[1]);
                        Assert.IsTrue(details.Skip(2).All(text => text == UiText.Get("DiscoveryProfileCheckNotChecked")));
                    });
                }
                finally { window.Close(); }
            });
        }
    }
}
