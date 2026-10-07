using System;
using System.Globalization;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Discovery;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using NetLoom.Wpf;
using NetLoom.Wpf.Localization;

namespace NetLoom.Tests.Unit
{
    public sealed partial class Sprint42WpfDiscoveryPanelTests
    {
        [TestMethod]
        public void DiscoveryPanelShowsCurrentPhaseAndClearsItAfterAddress()
        {
            RunOnSta(
                () =>
                {
                    var provider = new MutableRefreshProvider(
                        EmptySnapshot());
                    var control = new RecordingDiscoveryControl();
                    var profile = new AccessProfile(
                        Guid.NewGuid(),
                        "Discovery",
                        true,
                        SnmpVersion.V2C,
                        null);
                    var window = new MainWindow(
                        provider,
                        new EmptyLookupReader(),
                        new NoopMonitoringControl(),
                        control,
                        new[] { profile },
                        new RecordingCandidateMaterializer());

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () => provider.ReadCount >= 1);

                        var phaseText =
                            (TextBlock)window.FindName(
                                "DiscoveryPhaseValueText");
                        var address = IPAddress.Parse("192.0.2.1");

                        control.PublishState(
                            DiscoveryControlState.Running,
                            0,
                            4,
                            0,
                            address,
                            DiscoveryPhase.Icmp,
                            1,
                            2);

                        PumpDispatcher();

                        Assert.AreEqual(
                            UiText.Format(
                                "DiscoveryPhaseValue",
                                "ICMP",
                                1,
                                2),
                            phaseText.Text);

                        control.PublishState(
                            DiscoveryControlState.Running,
                            1,
                            4,
                            0,
                            address);

                        PumpDispatcher();

                        Assert.AreEqual(
                            UiText.Get("DiagnosticNotAvailable"),
                            phaseText.Text);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void ErrorCandidateShowsReasonAndCountsInRunSummary()
        {
            RunOnSta(
                () =>
                {
                    var provider = new MutableRefreshProvider(EmptySnapshot());
                    var control = new RecordingDiscoveryControl();
                    var profile = new AccessProfile(
                        Guid.NewGuid(), "Площадка А", true, SnmpVersion.V2C, null);
                    var window = new MainWindow(
                        provider,
                        new EmptyLookupReader(),
                        new NoopMonitoringControl(),
                        control,
                        new[] { profile },
                        new RecordingCandidateMaterializer());

                    try
                    {
                        window.Show();
                        WaitForCondition(() => provider.ReadCount >= 1);

                        var runClock = new DateTime(
                            2026, 10, 7, 9, 12, 5, DateTimeKind.Utc);
                        window.DiscoveryRunClock = () => runClock;
                        control.StartAsync(
                            new DiscoveryControlRequest(
                                "10.0.0.0/24", profile.Id, SnmpVersion.V2C),
                            CancellationToken.None).GetAwaiter().GetResult();

                        control.EmitCandidate(new DiscoveryCandidateSnapshot(
                            IPAddress.Parse("10.0.0.7"),
                            null,
                            true,
                            false,
                            new int[0],
                            null,
                            null,
                            null,
                            null,
                            0,
                            SnmpTransportFailure.Authentication));

                        var list = (ListBox)window.FindName("DiscoveryCandidatesList");
                        WaitForCondition(() => list.Items.Count == 1);
                        var expectedSummary = UiText.Format(
                            "DiscoveryCandidateErrorSummary",
                            UiText.Get("DiscoveryUnnamedCandidate"),
                            UiText.Get("DiscoveryErrorSnmpAuthentication"),
                            profile.Name,
                            runClock.ToLocalTime().ToString("t", CultureInfo.CurrentCulture));

                        runClock = runClock.AddMinutes(1);
                        control.PublishState(DiscoveryControlState.Completed, 4, 4, 1);
                        PumpDispatcher();

                        Assert.AreEqual(
                            expectedSummary,
                            list.Items[0].GetType().GetProperty("Summary")
                                .GetValue(list.Items[0]));
                        Assert.AreEqual(
                            "1",
                            ((TextBlock)window.FindName("DiscoveryRunErrorsValueText")).Text);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

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
