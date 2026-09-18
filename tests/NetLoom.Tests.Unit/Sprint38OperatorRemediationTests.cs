using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Lookup;
using NetLoom.Application.Locations;
using NetLoom.Application.MapLayout;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint38OperatorRemediationTests
    {
        private static readonly DateTime Now =
            new DateTime(
                2026,
                9,
                18,
                14,
                0,
                0,
                DateTimeKind.Utc);

        [TestMethod]
        public void CreateButtonChainsSiteBuildingRoomRackWithoutReopeningWindow()
        {
            RunOnSta(
                () =>
                {
                    var service =
                        new RecordingLocationTopologyService();

                    var window =
                        new LocationTopologyWindow(
                            service);

                    try
                    {
                        var name =
                            (TextBox)window.FindName(
                                "LocationNameTextBox");

                        var create =
                            (Button)window.FindName(
                                "LocationCreateButton");

                        name.Text =
                            "Site";
                        Click(
                            create);

                        Assert.IsTrue(
                            create.IsEnabled,
                            "After creating a Location, the same open editor must immediately be ready to create the next child.");

                        name.Text =
                            "Building";
                        Click(
                            create);

                        name.Text =
                            "Room";
                        Click(
                            create);

                        name.Text =
                            "Rack";
                        Click(
                            create);

                        var locations =
                            service.GetSnapshot()
                                .Locations
                                .ToArray();

                        Assert.AreEqual(
                            4,
                            locations.Length,
                            "One editor session must create Site -> Building -> Room -> Rack without reopening the window.");

                        var site =
                            locations.Single(
                                item =>
                                    item.Name ==
                                    "Site");

                        var building =
                            locations.Single(
                                item =>
                                    item.Name ==
                                    "Building");

                        var room =
                            locations.Single(
                                item =>
                                    item.Name ==
                                    "Room");

                        var rack =
                            locations.Single(
                                item =>
                                    item.Name ==
                                    "Rack");

                        Assert.IsNull(
                            site.ParentLocationId);

                        Assert.AreEqual(
                            site.Id,
                            building.ParentLocationId);

                        Assert.AreEqual(
                            building.Id,
                            room.ParentLocationId);

                        Assert.AreEqual(
                            room.Id,
                            rack.ParentLocationId);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void StartupFitsTopologyWhenRestoredViewportMissesEveryVisibleObject()
        {
            RunOnSta(
                () =>
                {
                    var locationId =
                        Guid.NewGuid();

                    var map =
                        new MapSnapshot(
                            Now,
                            new MapNode[0],
                            new MapLink[0],
                            new[]
                            {
                                new MapLocation(
                                    locationId,
                                    null,
                                    "Room",
                                    null)
                            });

                    var store =
                        new StaleViewportLayoutStore(
                            locationId);

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                map),
                            new EmptyLookupReader(),
                            store);

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                LocationBorder(
                                    MapCanvas(
                                        window),
                                    locationId) !=
                                null);

                        PumpDispatcher();
                        PumpDispatcher();
                        PumpDispatcher();

                        window.UpdateLayout();

                        var canvas =
                            MapCanvas(
                                window);

                        var location =
                            LocationBorder(
                                canvas,
                                locationId);

                        Assert.IsNotNull(
                            location);

                        var scroll =
                            (ScrollViewer)window.FindName(
                                "MapScrollViewer");

                        var scale =
                            canvas.LayoutTransform
                                as ScaleTransform;

                        Assert.IsNotNull(
                            scale);

                        Assert.IsTrue(
                            scroll.ViewportWidth > 0.0 &&
                            scroll.ViewportHeight > 0.0,
                            "The startup regression requires a measured map viewport.");

                        var viewport =
                            new Rect(
                                scroll.HorizontalOffset /
                                scale.ScaleX,
                                scroll.VerticalOffset /
                                scale.ScaleY,
                                scroll.ViewportWidth /
                                scale.ScaleX,
                                scroll.ViewportHeight /
                                scale.ScaleY);

                        Assert.IsTrue(
                            viewport.IntersectsWith(
                                Bounds(
                                    location)),
                            "After restart, a stale persisted viewport must not leave all visible topology off-screen.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        private static void Click(
            Button button)
        {
            Assert.IsNotNull(
                button);

            Assert.IsTrue(
                button.IsEnabled,
                "The operator-facing button must be enabled before it can be used.");

            button.RaiseEvent(
                new RoutedEventArgs(
                    Button.ClickEvent));
        }

        private static Canvas MapCanvas(
            MainWindow window)
        {
            var canvas =
                window.FindName(
                    "MapCanvas")
                    as Canvas;

            Assert.IsNotNull(
                canvas);

            return canvas;
        }

        private static Border LocationBorder(
            Canvas canvas,
            Guid locationId)
        {
            return canvas.Children
                .OfType<Border>()
                .FirstOrDefault(
                    item =>
                        Panel.GetZIndex(
                            item) < 0 &&
                        item.Tag is Guid &&
                        (Guid)item.Tag ==
                        locationId);
        }

        private static Rect Bounds(
            FrameworkElement element)
        {
            element.Measure(
                new Size(
                    double.PositiveInfinity,
                    double.PositiveInfinity));

            var width =
                double.IsNaN(
                    element.Width)
                    ? Math.Max(
                        element.ActualWidth,
                        element.DesiredSize.Width)
                    : element.Width;

            var height =
                double.IsNaN(
                    element.Height)
                    ? Math.Max(
                        element.ActualHeight,
                        element.DesiredSize.Height)
                    : element.Height;

            return new Rect(
                Canvas.GetLeft(
                    element),
                Canvas.GetTop(
                    element),
                width,
                height);
        }

        private static void WaitForCondition(
            Func<bool> condition)
        {
            for (var attempt = 0;
                 attempt < 100;
                 attempt++)
            {
                PumpDispatcher();

                if (condition())
                {
                    return;
                }

                Thread.Sleep(
                    10);
            }

            Assert.Fail(
                "The expected WPF state was not reached.");
        }

        private static void PumpDispatcher()
        {
            var frame =
                new DispatcherFrame();

            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(
                    () =>
                    {
                        frame.Continue =
                            false;
                    }));

            Dispatcher.PushFrame(
                frame);
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
                    TimeSpan.FromSeconds(10)))
            {
                Assert.Fail(
                    "STA WPF test did not complete.");
            }

            if (failure != null)
            {
                throw failure;
            }
        }

        private sealed class RecordingLocationTopologyService :
            ILocationTopologyService
        {
            private readonly List<LocationTopologyLocation>
                _locations =
                    new List<LocationTopologyLocation>();

            public LocationTopologySnapshot GetSnapshot()
            {
                return new LocationTopologySnapshot(
                    _locations.ToArray(),
                    new LocationTopologyDevice[0]);
            }

            public Guid CreateLocation(
                Guid? parentLocationId,
                string name,
                string description)
            {
                var id =
                    Guid.NewGuid();

                _locations.Add(
                    new LocationTopologyLocation(
                        id,
                        parentLocationId,
                        name,
                        description));

                return id;
            }

            public void UpdateLocation(
                Guid locationId,
                Guid? parentLocationId,
                string name,
                string description)
            {
                throw new NotSupportedException();
            }

            public void DeleteLocation(
                Guid locationId)
            {
                throw new NotSupportedException();
            }

            public void AssignDevice(
                Guid deviceId,
                Guid? locationId)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class FixedRefreshProvider :
            ITopologyRefreshSnapshotProvider
        {
            private readonly MapSnapshot
                _map;

            public FixedRefreshProvider(
                MapSnapshot map)
            {
                _map =
                    map;
            }

            public TopologyRefreshSnapshot GetSnapshot(
                string stpInstanceId)
            {
                return new TopologyRefreshSnapshot(
                    _map,
                    new TopologyAlertSnapshot(
                        Now,
                        stpInstanceId,
                        new TopologyAlert[0]));
            }
        }

        private sealed class StaleViewportLayoutStore :
            IMapLayoutStore,
            IMapLocationLayoutStore
        {
            private readonly Guid
                _locationId;

            public StaleViewportLayoutStore(
                Guid locationId)
            {
                _locationId =
                    locationId;
            }

            public MapLayoutSnapshot Load(
                Guid mapId)
            {
                return new MapLayoutSnapshot(
                    mapId,
                    new MapViewportLayout(
                        1.0,
                        1000000.0,
                        1000000.0),
                    new MapDeviceLayout[0],
                    new[]
                    {
                        new MapLocationLayout(
                            _locationId,
                            0.0,
                            0.0,
                            560.0,
                            360.0,
                            false,
                            false)
                    });
            }

            public void SaveViewport(
                Guid mapId,
                MapViewportLayout viewport)
            {
            }

            public void SaveDevice(
                Guid mapId,
                MapDeviceLayout deviceLayout)
            {
            }

            public void SaveLocation(
                Guid mapId,
                MapLocationLayout locationLayout)
            {
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
    }
}
