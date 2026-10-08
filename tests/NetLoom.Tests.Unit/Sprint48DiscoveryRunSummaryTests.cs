using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Discovery;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Application.DiscoveryInbox;
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
                        Assert.AreEqual(Visibility.Visible, phaseText.Visibility);

                        // После завершения запуска строки «Текущий адрес» и «Этап» скрыты.
                        control.PublishState(
                            DiscoveryControlState.Completed,
                            4,
                            4,
                            0,
                            null);

                        PumpDispatcher();

                        foreach (var name in new[]
                        {
                            "DiscoveryCurrentAddressLabelText",
                            "DiscoveryCurrentAddressValueText",
                            "DiscoveryPhaseLabelText",
                            "DiscoveryPhaseValueText"
                        })
                        {
                            Assert.AreEqual(
                                Visibility.Collapsed,
                                ((TextBlock)window.FindName(name)).Visibility,
                                name);
                        }
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
                        StartDiscoveryRunThroughTheWindow(
                            window, "10.0.0.1", "10.0.0.10");

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

                        WaitForCondition(() => InboxRows(window).Length == 1);
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
                            InboxRows(window).Single().Reason);
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

                        StartDiscoveryRunThroughTheWindow(
                            window, "192.0.2.1", "192.0.2.254");
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
                                InboxRows(window).Length == 2);

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

        [TestMethod]
        public void RunSummaryAfterRestartComesFromTheJournal()
        {
            RunOnSta(
                () =>
                {
                    var started = new DateTime(
                        2026, 10, 7, 9, 12, 5, DateTimeKind.Utc);
                    var profile = new AccessProfile(
                        Guid.NewGuid(), "Discovery", true, SnmpVersion.V2C, null);
                    var repository = new InMemoryDiscoveryRunRepository();
                    repository.SaveRun(new DiscoveryRunRecord(
                        Guid.NewGuid(),
                        started,
                        started.AddMinutes(4).AddSeconds(37),
                        DiscoveryControlState.Completed,
                        profile.Id,
                        profile.Name,
                        "10.0.0.0/24",
                        254, 254, 37, 31, 6, 19, null));
                    var journal = new DiscoveryRunJournal(
                        repository,
                        new EmptyDiscoveryTopologyReader(),
                        new RecordingCandidateMaterializer(),
                        new EmptyDiscoveryExclusionSource());
                    var control = new RecordingDiscoveryControl();
                    var window = new MainWindow(
                        new MutableRefreshProvider(EmptySnapshot()),
                        new EmptyLookupReader(),
                        new NoopMonitoringControl(),
                        control,
                        new[] { profile },
                        journal);

                    try
                    {
                        Assert.AreEqual(
                            Visibility.Visible,
                            ((StackPanel)window.FindName("DiscoveryRunSummaryPanel")).Visibility);
                        Assert.AreEqual(
                            started.ToLocalTime().ToString("G", CultureInfo.CurrentCulture),
                            ((TextBlock)window.FindName("DiscoveryRunStartedValueText")).Text);
                        var isRussian =
                            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru";
                        Assert.AreEqual(
                            isRussian ? "4 мин 37 с" : "4 min 37 s",
                            ((TextBlock)window.FindName("DiscoveryRunDurationValueText")).Text);
                        Assert.AreEqual(
                            UiText.FormatCount("DiscoveryRunChecked", 254, 254),
                            ((TextBlock)window.FindName("DiscoveryRunCheckedValueText")).Text);
                        Assert.AreEqual(
                            UiText.Format("DiscoveryRunFoundValue", 37, 31),
                            ((TextBlock)window.FindName("DiscoveryRunFoundValueText")).Text);
                        Assert.AreEqual(
                            "6",
                            ((TextBlock)window.FindName("DiscoveryRunErrorsValueText")).Text);
                        Assert.AreEqual(
                            "19",
                            ((TextBlock)window.FindName("DiscoveryRunKnownUnchangedValueText")).Text);
                        Assert.AreEqual(
                            UiText.Get("DiscoveryStateCompleted"),
                            ((TextBlock)window.FindName("DiscoveryStateValueText")).Text);
                        Assert.AreEqual(
                            Visibility.Collapsed,
                            ((FrameworkElement)window.FindName("DiscoveryResultsEmptyCard")).Visibility);
                        Assert.AreEqual(
                            Visibility.Visible,
                            ((FrameworkElement)window.FindName("DiscoveryResultsPanel")).Visibility);
                        Assert.IsNull(control.StartRequest);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void InterruptedRunIsShownAsFaultedAfterRestart()
        {
            RunOnSta(
                () =>
                {
                    var started = new DateTime(
                        2026, 10, 7, 9, 12, 5, DateTimeKind.Utc);
                    var runId = Guid.NewGuid();
                    var profile = new AccessProfile(
                        Guid.NewGuid(), "Discovery", true, SnmpVersion.V2C, null);
                    var repository = new InMemoryDiscoveryRunRepository();
                    repository.SaveRun(new DiscoveryRunRecord(
                        runId, started, null, DiscoveryControlState.Running,
                        profile.Id, profile.Name, "10.0.0.0/24",
                        254, 100, 12, 10, 2, 4, null));
                    var journal = new DiscoveryRunJournal(
                        repository,
                        new EmptyDiscoveryTopologyReader(),
                        new RecordingCandidateMaterializer(),
                        new EmptyDiscoveryExclusionSource());
                    var window = new MainWindow(
                        new MutableRefreshProvider(EmptySnapshot()),
                        new EmptyLookupReader(),
                        new NoopMonitoringControl(),
                        new RecordingDiscoveryControl(),
                        new[] { profile },
                        journal);

                    try
                    {
                        var run = repository.GetRun(runId);
                        Assert.AreEqual(DiscoveryControlState.Faulted, run.State);
                        Assert.AreEqual("DISCOVERY_INTERRUPTED", run.FaultMessage);
                        Assert.IsTrue(run.FinishedUtc.HasValue);
                        Assert.AreEqual(
                            Visibility.Visible,
                            ((StackPanel)window.FindName("DiscoveryRunSummaryPanel")).Visibility);
                        Assert.AreEqual(
                            UiText.Get("DiscoveryStateFaulted"),
                            ((TextBlock)window.FindName("DiscoveryStateValueText")).Text);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void StartPassesExcludedAddressesToTheEngineRequest()
        {
            RunOnSta(
                () =>
                {
                    var profile = new AccessProfile(
                        Guid.NewGuid(), "Discovery", true, SnmpVersion.V2C, null);
                    var source = new RunSummaryExclusionSource();
                    var journal = new DiscoveryRunJournal(
                        new InMemoryDiscoveryRunRepository(),
                        new EmptyDiscoveryTopologyReader(),
                        new RecordingCandidateMaterializer(),
                        source);
                    var control = new RecordingDiscoveryControl();
                    var window = new MainWindow(
                        new MutableRefreshProvider(EmptySnapshot()),
                        new EmptyLookupReader(),
                        new NoopMonitoringControl(),
                        control,
                        new[] { profile },
                        journal);

                    try
                    {
                        window.Show();
                        PumpDispatcher();
                        StartDiscoveryRunThroughTheWindow(
                            window, "10.0.0.1", "10.0.0.10");

                        Assert.IsNotNull(control.StartRequest);
                        CollectionAssert.AreEqual(
                            new[] { "10.0.0.5" },
                            new List<string>(control.StartRequest.ExcludedAddresses));
                        Assert.AreEqual(profile.Id, source.ProfileId);
                        Assert.AreEqual(profile.Id, control.StartRequest.AccessProfileId);
                        Assert.AreEqual("10.0.0.1", control.StartRequest.StartAddress);
                        Assert.AreEqual("10.0.0.10", control.StartRequest.EndAddress);
                        Assert.AreEqual(
                            Visibility.Collapsed,
                            ((StackPanel)window.FindName("DiscoveryRunSummaryPanel")).Visibility);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        private static void StartDiscoveryRunThroughTheWindow(
            MainWindow window,
            string startAddress,
            string endAddress)
        {
            ((TextBox)window.FindName("DiscoveryStartAddressTextBox")).Text = startAddress;
            ((TextBox)window.FindName("DiscoveryEndAddressTextBox")).Text = endAddress;
            ((TextBox)window.FindName("DiscoverySubnetMaskTextBox")).Text = "255.255.255.0";
            ((ComboBox)window.FindName("DiscoveryProfileComboBox")).SelectedIndex = 0;
            Click((Button)window.FindName("DiscoveryStartButton"));
        }

        private sealed class RunSummaryExclusionSource : IDiscoveryExclusionSource
        {
            public Guid? ProfileId { get; private set; }

            public IReadOnlyList<DiscoveryExclusionRule> GetRules(
                Guid accessProfileId)
            {
                ProfileId = accessProfileId;
                return new[] { new DiscoveryExclusionRule("IpAddress", "10.0.0.5") };
            }
        }
    }
}
