using System;
using System.Globalization;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Domain.Access;
using NetLoom.Wpf;

namespace NetLoom.Tests.Unit
{
    public sealed partial class Sprint42WpfDiscoveryPanelTests
    {
        [TestMethod]
        public void RunSummaryAppearsAfterTheRunWithCheckedFoundAndSnmpCounts()
        {
            RunOnSta(
                () =>
                {
                    var provider =
                        new MutableRefreshProvider(
                            EmptySnapshot());

                    var materializer =
                        new RecordingCandidateMaterializer();

                    var control =
                        new RecordingDiscoveryControl();

                    var profile =
                        new AccessProfile(
                            Guid.NewGuid(),
                            "Discovery",
                            true,
                            SnmpVersion.V2C,
                            null);

                    var window =
                        new MainWindow(
                            provider,
                            new EmptyLookupReader(),
                            new NoopMonitoringControl(),
                            control,
                            new[] { profile },
                            materializer);

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                provider.ReadCount >= 1);

                        var panel =
                            (StackPanel)window.FindName(
                                "DiscoveryRunSummaryPanel");

                        Assert.AreEqual(
                            Visibility.Collapsed,
                            panel.Visibility);

                        control.PublishState(
                            DiscoveryControlState.Running,
                            1,
                            4,
                            0);

                        Assert.AreEqual(
                            Visibility.Collapsed,
                            panel.Visibility);

                        control.EmitCandidate(
                            new DiscoveryCandidateSnapshot(
                                IPAddress.Parse(
                                    "192.0.2.44"),
                                profile.Id,
                                true,
                                true,
                                new[] { 22, 443 },
                                "Found switch",
                                "Synthetic device",
                                "1.3.6.1.4.1.99999",
                                null,
                                12));

                        control.EmitCandidate(
                            new DiscoveryCandidateSnapshot(
                                IPAddress.Parse(
                                    "192.0.2.45"),
                                profile.Id,
                                true,
                                false,
                                new[] { 22, 443 },
                                "Found host",
                                "Synthetic device",
                                null,
                                null,
                                12));

                        WaitForCondition(
                            () =>
                                ((ListBox)window.FindName(
                                    "DiscoveryCandidatesList"))
                                .Items.Count == 2);

                        control.PublishState(
                            DiscoveryControlState.Running,
                            3,
                            4,
                            2);

                        Assert.AreEqual(
                            Visibility.Collapsed,
                            panel.Visibility);

                        control.PublishState(
                            DiscoveryControlState.Completed,
                            4,
                            4,
                            2);

                        WaitForCondition(
                            () =>
                                panel.Visibility ==
                                Visibility.Visible);

                        var isRussian =
                            CultureInfo.CurrentUICulture
                                .TwoLetterISOLanguageName == "ru";

                        Assert.AreEqual(
                            isRussian
                                ? "4 из 4 адресов"
                                : "4 of 4 addresses",
                            ((TextBlock)window.FindName(
                                "DiscoveryRunCheckedValueText"))
                            .Text);

                        Assert.AreEqual(
                            isRussian
                                ? "2, SNMP ответили: 1"
                                : "2, SNMP answered: 1",
                            ((TextBlock)window.FindName(
                                "DiscoveryRunFoundValueText"))
                            .Text);

                        Assert.IsFalse(
                            string.IsNullOrWhiteSpace(
                                ((TextBlock)window.FindName(
                                    "DiscoveryRunStartedValueText"))
                                .Text));

                        Assert.IsFalse(
                            string.IsNullOrWhiteSpace(
                                ((TextBlock)window.FindName(
                                    "DiscoveryRunDurationValueText"))
                                .Text));
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }
    }
}
