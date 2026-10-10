using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Wpf.Localization;

namespace NetLoom.Tests.Unit
{
    public sealed partial class Sprint42WpfDiscoveryPanelTests
    {
        [TestMethod]
        public void SecureV3TemplateFillsParametersWithoutSecretsAndRejectsShortPassword()
        {
            RunOnSta(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var data = new Sprint48DiscoveryInboxFixture();
                var window = InboxWindow(data, data.Repository(), new RecordingDiscoveryControl());
                var saveCalls = 0;
                window.DiscoveryProfileCreateRequested += (sender, args) => saveCalls++;
                try
                {
                    window.Show();
                    PumpDispatcher();
                    Sprint48ProfileCheckDialogFixture.Open(window, null, dialog =>
                    {
                        Sprint48ProfileCheckDialogFixture.Visuals(dialog).OfType<TextBox>()
                            .First(box => box.Name == string.Empty).Text = "Secured";
                        var template = Sprint48ProfileCheckDialogFixture.Named<ComboBox>(dialog,
                            "DiscoveryProfileTemplateComboBox");
                        template.SelectedIndex = 1;
                        var version = Sprint48ProfileCheckDialogFixture.Visuals(dialog).OfType<ComboBox>()
                            .Single(box => box.DisplayMemberPath == "DisplayName");
                        Assert.AreEqual("v3", version.Text);
                        Assert.AreEqual(2, Sprint48ProfileCheckDialogFixture.Named<ComboBox>(dialog,
                            "DiscoveryProfileV3SecurityLevel").SelectedIndex);
                        Assert.AreEqual("SHA-256", Sprint48ProfileCheckDialogFixture.Named<ComboBox>(dialog,
                            "DiscoveryProfileV3AuthProtocol").SelectedItem);
                        Assert.AreEqual("AES-128", Sprint48ProfileCheckDialogFixture.Named<ComboBox>(dialog,
                            "DiscoveryProfileV3PrivacyProtocol").SelectedItem);
                        Assert.AreEqual("3000", Sprint48ProfileCheckDialogFixture.Named<TextBox>(dialog,
                            "DiscoveryProfileV3Timeout").Text);
                        Assert.AreEqual("1", Sprint48ProfileCheckDialogFixture.Named<TextBox>(dialog,
                            "DiscoveryProfileV3Retries").Text);
                        var auth = Sprint48ProfileCheckDialogFixture.Named<PasswordBox>(dialog,
                            "DiscoveryProfileV3AuthPassword");
                        Assert.AreEqual(string.Empty, auth.Password);
                        Assert.AreEqual(string.Empty, Sprint48ProfileCheckDialogFixture.Named<PasswordBox>(dialog,
                            "DiscoveryProfileV3PrivacyPassword").Password);
                        Sprint48ProfileCheckDialogFixture.Named<TextBox>(dialog,
                            "DiscoveryProfileV3Username").Text = "operator";
                        auth.Password = "short";
                        Click(Sprint48ProfileCheckDialogFixture.Visuals(dialog).OfType<Button>()
                            .Single(button => Equals(button.Content, UiText.Get("DiscoveryProfileSaveAction"))));
                        Assert.AreEqual(0, saveCalls);
                        Assert.IsTrue(Sprint48ProfileCheckDialogFixture.Visuals(dialog).OfType<TextBlock>()
                            .Any(block => block.Visibility == Visibility.Visible &&
                                block.Text == UiText.Get("DiscoveryProfileV3PasswordShort")));
                    });
                }
                finally { window.Close(); }
            });
        }
    }
}
