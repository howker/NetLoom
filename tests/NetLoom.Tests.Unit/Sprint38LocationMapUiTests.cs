using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Lookup;
using NetLoom.Application.MapLayout;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint38LocationMapUiTests
    {
        private static readonly DateTime Now =
            new DateTime(
                2026,
                9,
                17,
                18,
                0,
                0,
                DateTimeKind.Utc);

        [TestMethod]
        public void LocationContainerEnclosesAssignedNodesBehindTheGraph()
        {
            RunOnSta(
                () =>
                {
                    var locationId =
                        Guid.NewGuid();

                    var window =
                        new MainWindow();

                    try
                    {
                        window.ShowMap(
                            new MapSnapshot(
                                Now,
                                new[]
                                {
                                    Node(
                                        "a",
                                        "A",
                                        100,
                                        120,
                                        locationId),
                                    Node(
                                        "b",
                                        "B",
                                        420,
                                        280,
                                        locationId)
                                },
                                new MapLink[0],
                                new[]
                                {
                                    new MapLocation(
                                        locationId,
                                        null,
                                        "Room 101",
                                        "Production")
                                }));

                        var canvas =
                            MapCanvas(
                                window);

                        var location =
                            LocationBorder(
                                canvas);

                        var nodes =
                            canvas.Children
                                .OfType<Border>()
                                .Where(
                                    item =>
                                        Panel.GetZIndex(
                                            item) == 2)
                                .ToArray();

                        Assert.AreEqual(
                            2,
                            nodes.Length);

                        Assert.IsTrue(
                            Panel.GetZIndex(
                                location) < 0,
                            "Location containers must render behind links and device cards.");

                        var locationBounds =
                            Bounds(
                                location);

                        foreach (var node in nodes)
                        {
                            Assert.IsTrue(
                                locationBounds.Contains(
                                    Bounds(node)),
                                "The production location container must enclose its assigned node card.");
                        }
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void NestedParentLocationEnclosesChildLocation()
        {
            RunOnSta(
                () =>
                {
                    var parentId =
                        Guid.NewGuid();

                    var childId =
                        Guid.NewGuid();

                    var window =
                        new MainWindow();

                    try
                    {
                        window.ShowMap(
                            new MapSnapshot(
                                Now,
                                new[]
                                {
                                    Node(
                                        "a",
                                        "A",
                                        200,
                                        220,
                                        childId)
                                },
                                new MapLink[0],
                                new[]
                                {
                                    new MapLocation(
                                        parentId,
                                        null,
                                        "Building",
                                        null),
                                    new MapLocation(
                                        childId,
                                        parentId,
                                        "Room",
                                        null)
                                }));

                        var canvas =
                            MapCanvas(
                                window);

                        var locations =
                            canvas.Children
                                .OfType<Border>()
                                .Where(
                                    item =>
                                        Panel.GetZIndex(
                                            item) < 0)
                                .ToArray();

                        Assert.AreEqual(
                            2,
                            locations.Length);

                        var outer =
                            locations
                                .OrderByDescending(
                                    item =>
                                        item.Width *
                                        item.Height)
                                .First();

                        var inner =
                            locations
                                .OrderBy(
                                    item =>
                                        item.Width *
                                        item.Height)
                                .First();

                        Assert.IsTrue(
                            Bounds(outer)
                                .Contains(
                                    Bounds(inner)),
                            "A parent location must be derived around its child location when no persisted layout exists.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void IncrementalEmptyChildLocationStartsInsideExistingParent()
        {
            RunOnSta(
                () =>
                {
                    var parentId =
                        Guid.NewGuid();

                    var childId =
                        Guid.NewGuid();

                    var window =
                        new MainWindow();

                    try
                    {
                        window.ShowMap(
                            new MapSnapshot(
                                Now,
                                new MapNode[0],
                                new MapLink[0],
                                new[]
                                {
                                    new MapLocation(
                                        parentId,
                                        null,
                                        "Building",
                                        null)
                                }));

                        window.ShowMap(
                            new MapSnapshot(
                                Now,
                                new MapNode[0],
                                new MapLink[0],
                                new[]
                                {
                                    new MapLocation(
                                        parentId,
                                        null,
                                        "Building",
                                        null),
                                    new MapLocation(
                                        childId,
                                        parentId,
                                        "Room",
                                        null)
                                }));

                        var canvas =
                            MapCanvas(
                                window);

                        var parentBounds =
                            Bounds(
                                LocationBorder(
                                    canvas,
                                    parentId));

                        var childBounds =
                            Bounds(
                                LocationBorder(
                                    canvas,
                                    childId));

                        Assert.IsTrue(
                            parentBounds.Contains(
                                childBounds),
                            "An incrementally created child location must remain visually inside its existing parent.");

                        Assert.IsTrue(
                            childBounds.Left >
                            parentBounds.Left &&
                            childBounds.Top >
                            parentBounds.Top,
                            "An empty child location must be inset from the parent instead of overlapping the parent frame.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void EmptyChildLocationUsesPersistedParentBoundsAfterRestart()
        {
            RunOnSta(
                () =>
                {
                    var parentId =
                        Guid.NewGuid();

                    var childId =
                        Guid.NewGuid();

                    var store =
                        new RecordingMapLayoutStore(
                            new MapLocationLayout(
                                parentId,
                                1200.0,
                                900.0,
                                800.0,
                                600.0,
                                false,
                                false));

                    var window =
                        new MainWindow(
                            new EmptyRefreshProvider(),
                            new EmptyLookupReader(),
                            store);

                    try
                    {
                        window.ShowMap(
                            new MapSnapshot(
                                Now,
                                new MapNode[0],
                                new MapLink[0],
                                new[]
                                {
                                    new MapLocation(
                                        parentId,
                                        null,
                                        "Building",
                                        null),
                                    new MapLocation(
                                        childId,
                                        parentId,
                                        "Room",
                                        null)
                                }));

                        var canvas =
                            MapCanvas(
                                window);

                        Assert.IsTrue(
                            Bounds(
                                LocationBorder(
                                    canvas,
                                    parentId))
                                .Contains(
                                    Bounds(
                                        LocationBorder(
                                            canvas,
                                            childId))),
                            "A child without its own persisted layout must start inside the persisted parent after restart.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void CollapseChangesRenderedContainerHeightWithoutMovingNodes()
        {
            RunOnSta(
                () =>
                {
                    var locationId =
                        Guid.NewGuid();

                    var window =
                        new MainWindow();

                    try
                    {
                        window.ShowMap(
                            new MapSnapshot(
                                Now,
                                new[]
                                {
                                    Node(
                                        "a",
                                        "A",
                                        100,
                                        120,
                                        locationId)
                                },
                                new MapLink[0],
                                new[]
                                {
                                    new MapLocation(
                                        locationId,
                                        null,
                                        "Rack",
                                        null)
                                }));

                        var canvas =
                            MapCanvas(
                                window);

                        var location =
                            LocationBorder(
                                canvas);

                        var node =
                            canvas.Children
                                .OfType<Border>()
                                .Single(
                                    item =>
                                        Panel.GetZIndex(
                                            item) == 2);

                        var nodeLeft =
                            Canvas.GetLeft(
                                node);

                        var nodeTop =
                            Canvas.GetTop(
                                node);

                        var expandedHeight =
                            location.Height;

                        var method =
                            typeof(MainWindow)
                                .GetMethod(
                                    "ToggleLocationCollapsed",
                                    BindingFlags.Instance |
                                    BindingFlags.NonPublic);

                        Assert.IsNotNull(
                            method);

                        method.Invoke(
                            window,
                            new object[]
                            {
                                locationId
                            });

                        Assert.IsTrue(
                            location.Height <
                            expandedHeight,
                            "Collapsing must alter the actual rendered container, not merely a stored flag.");

                        Assert.AreEqual(
                            nodeLeft,
                            Canvas.GetLeft(
                                node));

                        Assert.AreEqual(
                            nodeTop,
                            Canvas.GetTop(
                                node));
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void CollapsePersistsThroughProductionLocationLayoutPath()
        {
            RunOnSta(
                () =>
                {
                    var locationId =
                        Guid.NewGuid();

                    var store =
                        new RecordingMapLayoutStore();

                    var window =
                        new MainWindow(
                            new EmptyRefreshProvider(),
                            new EmptyLookupReader(),
                            store);

                    try
                    {
                        window.ShowMap(
                            new MapSnapshot(
                                Now,
                                new[]
                                {
                                    Node(
                                        "a",
                                        "A",
                                        100,
                                        120,
                                        locationId)
                                },
                                new MapLink[0],
                                new[]
                                {
                                    new MapLocation(
                                        locationId,
                                        null,
                                        "Rack",
                                        null)
                                }));

                        var method =
                            typeof(MainWindow)
                                .GetMethod(
                                    "ToggleLocationCollapsed",
                                    BindingFlags.Instance |
                                    BindingFlags.NonPublic);

                        Assert.IsNotNull(
                            method);

                        method.Invoke(
                            window,
                            new object[]
                            {
                                locationId
                            });

                        Assert.IsNotNull(
                            store.LastLocation);

                        Assert.AreEqual(
                            locationId,
                            store.LastLocation.LocationId);

                        Assert.IsTrue(
                            store.LastLocation.IsCollapsed);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        private static MapNode Node(
            string key,
            string label,
            double x,
            double y,
            Guid locationId)
        {
            return new MapNode(
                key,
                label,
                null,
                x,
                y,
                locationId,
                deviceId: Guid.NewGuid());
        }

        private static Canvas MapCanvas(
            MainWindow window)
        {
            var canvas =
                window.FindName(
                    "MapCanvas") as Canvas;

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
                .Single(
                    item =>
                        Panel.GetZIndex(
                            item) < 0 &&
                        item.Tag is Guid &&
                        (Guid)item.Tag ==
                        locationId);
        }

        private static Border LocationBorder(
            Canvas canvas)
        {
            var location =
                canvas.Children
                    .OfType<Border>()
                    .Single(
                        item =>
                            Panel.GetZIndex(
                                item) < 0);

            return location;
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

        private sealed class RecordingMapLayoutStore :
            IMapLayoutStore,
            IMapLocationLayoutStore
        {
            private readonly MapLocationLayout[]
                _locations;

            public RecordingMapLayoutStore(
                params MapLocationLayout[] locations)
            {
                _locations =
                    locations ??
                    new MapLocationLayout[0];
            }

            public MapLocationLayout LastLocation { get; private set; }

            public MapLayoutSnapshot Load(
                Guid mapId)
            {
                return new MapLayoutSnapshot(
                    mapId,
                    new MapViewportLayout(
                        1.0,
                        0.0,
                        0.0),
                    new MapDeviceLayout[0],
                    _locations);
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
                LastLocation =
                    locationLayout;
            }
        }

        private sealed class EmptyRefreshProvider :
            ITopologyRefreshSnapshotProvider
        {
            public TopologyRefreshSnapshot GetSnapshot(
                string stpInstanceId)
            {
                return new TopologyRefreshSnapshot(
                    new MapSnapshot(
                        Now,
                        new MapNode[0],
                        new MapLink[0]),
                    new TopologyAlertSnapshot(
                        Now,
                        stpInstanceId,
                        new TopologyAlert[0]));
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
    }
}
