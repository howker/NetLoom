using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Threading;
using NetLoom.Application.Lookup;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint41GlanceReadableTopologyTests
    {
        [TestMethod]
        public void
            LongDeviceNamesRemainReadableAndDenseMetadataStaysOutOfTheCard()
        {
            RunOnSta(
                () =>
                {
                    var deviceId =
                        Guid.NewGuid();

                    var window =
                        new MainWindow();

                    try
                    {
                        window.Show();

                        window.ShowMap(
                            new MapSnapshot(
                                DateTime.UtcNow,
                                new[]
                                {
                                    new MapNode(
                                        "device:central-a",
                                        "Центральный коммутатор A",
                                        "MOXA PT-7728",
                                        100.0,
                                        100.0,
                                        null,
                                        MapNodeOrigin.Automatic,
                                        MapMonitoringCapability.Unknown,
                                        MapNodeCategory.UnmanagedSwitch,
                                        deviceId,
                                        "192.0.2.11")
                                },
                                new MapLink[0]));

                        var card =
                            DeviceCard(
                                window,
                                deviceId);

                        Assert.IsNotNull(
                            card,
                            "Production map card was not created.");

                        Assert.AreEqual(
                            160.0,
                            card.Width,
                            "The accepted glance-readable card width is 160 px.");

                        Assert.AreEqual(
                            56.0,
                            card.MinHeight,
                            "The accepted glance-readable card base height is 56 px.");

                        card.Measure(
                            new Size(
                                card.Width,
                                double.PositiveInfinity));

                        var textBlocks =
                            Descendants<TextBlock>(
                                    card)
                                .Where(
                                    item =>
                                        item.Visibility ==
                                            Visibility.Visible &&
                                        !string.IsNullOrWhiteSpace(
                                            item.Text))
                                .ToArray();

                        var title =
                            textBlocks.Single(
                                item =>
                                    item.Text ==
                                    "Центральный коммутатор A");

                        Assert.AreEqual(
                            TextWrapping.Wrap,
                            title.TextWrapping,
                            "Long device names must wrap rather than being reduced to an ellipsis.");

                        Assert.AreEqual(
                            TextTrimming.None,
                            title.TextTrimming,
                            "The production title must not hide the distinguishing end of a long device name.");

                        CollectionAssert.AreEquivalent(
                            new[]
                            {
                                "Центральный коммутатор A"
                            },
                            textBlocks
                                .Select(
                                    item => item.Text)
                                .ToArray(),
                            "The glance-readable card must keep the name only; type is carried by the icon and details stay in diagnostics.");

                        Assert.IsFalse(
                            textBlocks.Any(
                                item =>
                                    item.Text.Contains(
                                        "192.0.2.11")),
                            "Management details belong to diagnostics, not the glance-readable card.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            EveryKnownDeviceCategoryHasItsOwnVectorIconAndUnknownIsDistinct()
        {
            RunOnSta(
                () =>
                {
                    var fixtures =
                        new[]
                        {
                            CategoryFixture(
                                MapNodeCategory.Unknown,
                                "Unknown"),
                            CategoryFixture(
                                MapNodeCategory.MediaConverter,
                                "Media converter"),
                            CategoryFixture(
                                MapNodeCategory.UnmanagedSwitch,
                                "Unmanaged switch"),
                            CategoryFixture(
                                MapNodeCategory.OpticalConverter,
                                "Optical converter"),
                            CategoryFixture(
                                MapNodeCategory.PassiveNetworkEquipment,
                                "Passive equipment")
                        };

                    var window =
                        new MainWindow();

                    try
                    {
                        window.Show();

                        window.ShowMap(
                            new MapSnapshot(
                                DateTime.UtcNow,
                                fixtures
                                    .Select(
                                        (item, index) =>
                                            new MapNode(
                                                "device:" + index,
                                                item.Label,
                                                null,
                                                index * 220.0,
                                                100.0,
                                                null,
                                                MapNodeOrigin.Automatic,
                                                MapMonitoringCapability.Unknown,
                                                item.Category,
                                                item.DeviceId,
                                                null))
                                    .ToArray(),
                                new MapLink[0]));

                        var geometries =
                            new Dictionary<
                                MapNodeCategory,
                                string>();

                        foreach (var fixture in fixtures)
                        {
                            var card =
                                DeviceCard(
                                    window,
                                    fixture.DeviceId);

                            Assert.IsNotNull(
                                card);

                            var icon =
                                Descendants<Path>(
                                        card)
                                    .Single();

                            Assert.IsNotNull(
                                icon.Data,
                                "Every production category must render a vector geometry.");

                            geometries[fixture.Category] =
                                icon.Data.ToString();
                        }

                        Assert.AreEqual(
                            fixtures.Length,
                            geometries.Values
                                .Distinct(
                                    StringComparer.Ordinal)
                                .Count(),
                            "Unknown and each known OT category must be visually distinguishable by geometry.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            NodeStateIsAColoredLeftStripeAndSelectionAddsFullBlueOutline()
        {
            RunOnSta(
                () =>
                {
                    var healthyId =
                        Guid.NewGuid();

                    var degradedId =
                        Guid.NewGuid();

                    var unknownId =
                        Guid.NewGuid();

                    var provider =
                        new FixedRefreshProvider(
                            StatusSnapshot(
                                healthyId,
                                degradedId,
                                unknownId));

                    var window =
                        new MainWindow(
                            provider,
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                DeviceCard(
                                    window,
                                    healthyId) != null &&
                                provider.ReadCount > 0);

                        var healthy =
                            DeviceCard(
                                window,
                                healthyId);

                        var degraded =
                            DeviceCard(
                                window,
                                degradedId);

                        var unknown =
                            DeviceCard(
                                window,
                                unknownId);

                        Assert.AreSame(
                            window.FindResource(
                                "NetLoom.Brush.Success"),
                            StateStripe(healthy).Background,
                            "Only proven healthy diagnostic evidence may render the green state stripe.");

                        Assert.AreSame(
                            window.FindResource(
                                "NetLoom.Brush.Warning"),
                            StateStripe(degraded).Background,
                            "Confirmed degradation must render the warning state stripe.");

                        Assert.AreSame(
                            window.FindResource(
                                "NetLoom.Brush.TextDisabled"),
                            StateStripe(unknown).Background,
                            "Unknown evidence must remain neutral rather than pretending the node is healthy.");

                        SelectDevice(
                            window,
                            degradedId);

                        Assert.AreSame(
                            window.FindResource(
                                "NetLoom.Brush.Selection"),
                            degraded.BorderBrush,
                            "Selection must be explicit through a full blue card outline.");

                        Assert.AreSame(
                            window.FindResource(
                                "NetLoom.Brush.Selection"),
                            StateStripe(degraded).Background,
                            "Selection must also override the left stripe with blue so it cannot be confused with health state.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            BasePhysicalLinkIsVisiblyWeightedAndUsesRoundedEnds()
        {
            RunOnSta(
                () =>
                {
                    var sourceId =
                        Guid.NewGuid();

                    var targetId =
                        Guid.NewGuid();

                    var window =
                        new MainWindow();

                    try
                    {
                        window.Show();

                        window.ShowMap(
                            new MapSnapshot(
                                DateTime.UtcNow,
                                new[]
                                {
                                    MapNodeFixture(
                                        "source",
                                        sourceId,
                                        100.0,
                                        100.0),
                                    MapNodeFixture(
                                        "target",
                                        targetId,
                                        360.0,
                                        100.0)
                                },
                                new[]
                                {
                                    new MapLink(
                                        "link:source-target",
                                        "source",
                                        "target",
                                        "Gi0/1",
                                        "Gi0/2",
                                        MapConfidence.High,
                                        MapFreshness.Fresh,
                                        new MapEvidenceItem[0],
                                        Guid.NewGuid())
                                }));

                        var canvas =
                            (Canvas)window.FindName(
                                "MapCanvas");

                        var line =
                            canvas.Children
                                .OfType<Line>()
                                .Single();

                        var baseThickness =
                            (double)window.FindResource(
                                "NetLoom.Map.LinkStrokeThickness");

                        var selectedThickness =
                            (double)window.FindResource(
                                "NetLoom.Map.LinkSelectedStrokeThickness");

                        Assert.AreEqual(
                            baseThickness,
                            line.StrokeThickness,
                            "A normal physical link must use the production visual-weight token.");

                        Assert.IsTrue(
                            selectedThickness > baseThickness,
                            "Selected links must remain more prominent than normal links.");

                        Assert.AreEqual(
                            PenLineCap.Round,
                            line.StrokeStartLineCap);

                        Assert.AreEqual(
                            PenLineCap.Round,
                            line.StrokeEndLineCap);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        private static CategoryTestFixture CategoryFixture(
            MapNodeCategory category,
            string label)
        {
            return new CategoryTestFixture(
                Guid.NewGuid(),
                category,
                label);
        }

        private static Border DeviceCard(
            MainWindow window,
            Guid deviceId)
        {
            var canvas =
                window.FindName(
                    "MapCanvas") as Canvas;

            Assert.IsNotNull(
                canvas,
                "Production MapCanvas was not found.");

            return canvas.Children
                .OfType<Border>()
                .SingleOrDefault(
                    item =>
                        item.Tag is Guid &&
                        (Guid)item.Tag ==
                            deviceId);
        }

        private static IEnumerable<T>
            Descendants<T>(
                DependencyObject root)
            where T : DependencyObject
        {
            if (root == null)
            {
                yield break;
            }

            var count =
                VisualTreeHelper.GetChildrenCount(
                    root);

            for (var index = 0;
                 index < count;
                 index++)
            {
                var child =
                    VisualTreeHelper.GetChild(
                        root,
                        index);

                var match =
                    child as T;

                if (match != null)
                {
                    yield return match;
                }

                foreach (var nested in
                    Descendants<T>(child))
                {
                    yield return nested;
                }
            }
        }

        private static Border StateStripe(
            Border card)
        {
            Assert.IsNotNull(card);

            var stripe =
                Descendants<Border>(card)
                    .SingleOrDefault(
                        item =>
                            Math.Abs(item.Width - 5.0) < 0.001);

            Assert.IsNotNull(
                stripe,
                "Production card state stripe was not found.");

            return stripe;
        }

        private static void SelectDevice(
            MainWindow window,
            Guid deviceId)
        {
            var border =
                DeviceCard(
                    window,
                    deviceId);

            Assert.IsNotNull(border);

            border.RaiseEvent(
                new MouseButtonEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    MouseButton.Left)
                {
                    RoutedEvent =
                        UIElement.MouseLeftButtonDownEvent,
                    Source = border
                });

            border.RaiseEvent(
                new MouseButtonEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    MouseButton.Left)
                {
                    RoutedEvent =
                        UIElement.MouseLeftButtonUpEvent,
                    Source = border
                });

            PumpDispatcher();
        }

        private static TopologyRefreshSnapshot StatusSnapshot(
            Guid healthyId,
            Guid degradedId,
            Guid unknownId)
        {
            var now =
                DateTime.UtcNow;

            var nodes =
                new[]
                {
                    MapNodeFixture(
                        "healthy",
                        healthyId,
                        100.0,
                        100.0),
                    MapNodeFixture(
                        "degraded",
                        degradedId,
                        320.0,
                        100.0),
                    MapNodeFixture(
                        "unknown",
                        unknownId,
                        540.0,
                        100.0)
                };

            var diagnostics =
                new[]
                {
                    DiagnosticDevice(
                        healthyId,
                        DiagnosticDegradationStatus.Healthy,
                        now),
                    DiagnosticDevice(
                        degradedId,
                        DiagnosticDegradationStatus.Degraded,
                        now),
                    DiagnosticDevice(
                        unknownId,
                        DiagnosticDegradationStatus.Unknown,
                        now)
                };

            return new TopologyRefreshSnapshot(
                new MapSnapshot(
                    now,
                    nodes,
                    new MapLink[0]),
                new TopologyAlertSnapshot(
                    now,
                    "cist",
                    new TopologyAlert[0]),
                new NetworkDiagnosticSnapshot(
                    now,
                    diagnostics,
                    new PhysicalLinkDiagnostic[0]));
        }

        private static MapNode MapNodeFixture(
            string key,
            Guid deviceId,
            double x,
            double y)
        {
            return new MapNode(
                key,
                key,
                null,
                x,
                y,
                null,
                MapNodeOrigin.Automatic,
                MapMonitoringCapability.Unknown,
                MapNodeCategory.UnmanagedSwitch,
                deviceId,
                null);
        }

        private static DeviceDiagnostic DiagnosticDevice(
            Guid deviceId,
            DiagnosticDegradationStatus status,
            DateTime now)
        {
            return new DeviceDiagnostic(
                deviceId,
                deviceId.ToString("D"),
                null,
                null,
                now,
                now,
                new[]
                {
                    new InterfaceDiagnostic(
                        Guid.NewGuid(),
                        deviceId,
                        1,
                        "Gi0/1",
                        null,
                        "up",
                        "up",
                        1000000000L,
                        now,
                        NetLoom.Contracts.StpTree.StpTreePortState.Unknown,
                        status,
                        now,
                        status == DiagnosticDegradationStatus.Degraded
                            ? new[]
                              {
                                  DiagnosticDegradationReason.ErrorRateThresholdExceeded
                              }
                            : new DiagnosticDegradationReason[0])
                });
        }

        private static void WaitForCondition(
            Func<bool> condition)
        {
            var deadline =
                DateTime.UtcNow +
                TimeSpan.FromSeconds(5);

            while (!condition())
            {
                if (DateTime.UtcNow >= deadline)
                {
                    Assert.Fail(
                        "The expected WPF state was not reached.");
                }

                PumpDispatcher();
                Thread.Sleep(10);
            }

            PumpDispatcher();
        }

        private static void PumpDispatcher()
        {
            var frame =
                new DispatcherFrame();

            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new DispatcherOperationCallback(
                    state =>
                    {
                        ((DispatcherFrame)state)
                            .Continue = false;
                        return null;
                    }),
                frame);

            Dispatcher.PushFrame(
                frame);
        }

        private sealed class FixedRefreshProvider :
            ITopologyRefreshSnapshotProvider
        {
            private readonly TopologyRefreshSnapshot
                _snapshot;

            private int _readCount;

            public FixedRefreshProvider(
                TopologyRefreshSnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public int ReadCount =>
                Volatile.Read(
                    ref _readCount);

            public TopologyRefreshSnapshot GetSnapshot(
                string stpInstanceId)
            {
                Interlocked.Increment(
                    ref _readCount);
                return _snapshot;
            }
        }

        private sealed class EmptyLookupReader :
            IMacIpLookupReader
        {
            public MacIpLookupResult FindByMac(
                string macAddress,
                int maxCandidates)
            {
                return new MacIpLookupResult(
                    MacIpLookupKind.Mac,
                    macAddress,
                    new MacIpLookupCandidate[0]);
            }

            public MacIpLookupResult FindByIp(
                string ipAddress,
                int maxCandidates)
            {
                return new MacIpLookupResult(
                    MacIpLookupKind.Ip,
                    ipAddress,
                    new MacIpLookupCandidate[0]);
            }
        }

        private static void RunOnSta(
            Action action)
        {
            Exception failure =
                null;

            var thread =
                new Thread(
                    () =>
                    {
                        try
                        {
                            action();
                        }
                        catch (Exception error)
                        {
                            failure = error;
                        }
                    });

            thread.SetApartmentState(
                ApartmentState.STA);

            thread.Start();

            if (!thread.Join(
                    TimeSpan.FromSeconds(20)))
            {
                throw new AssertFailedException(
                    "STA WPF test did not complete.");
            }

            if (failure != null)
            {
                throw failure;
            }
        }

        private sealed class CategoryTestFixture
        {
            public CategoryTestFixture(
                Guid deviceId,
                MapNodeCategory category,
                string label)
            {
                DeviceId = deviceId;
                Category = category;
                Label = label;
            }

            public Guid DeviceId { get; }

            public MapNodeCategory Category { get; }

            public string Label { get; }
        }
    }
}
