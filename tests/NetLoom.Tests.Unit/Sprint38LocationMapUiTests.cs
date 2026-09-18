using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        public void EmptySiteBuildingRoomRackHierarchyRendersAsNestedContainers()
        {
            RunOnSta(
                () =>
                {
                    var siteId =
                        Guid.NewGuid();

                    var buildingId =
                        Guid.NewGuid();

                    var roomId =
                        Guid.NewGuid();

                    var rackId =
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
                                        siteId,
                                        null,
                                        "Site",
                                        null),
                                    new MapLocation(
                                        buildingId,
                                        siteId,
                                        "Building",
                                        null),
                                    new MapLocation(
                                        roomId,
                                        buildingId,
                                        "Room",
                                        null),
                                    new MapLocation(
                                        rackId,
                                        roomId,
                                        "Rack",
                                        null)
                                }));

                        var canvas =
                            MapCanvas(
                                window);

                        var site =
                            Bounds(
                                LocationBorder(
                                    canvas,
                                    siteId));

                        var building =
                            Bounds(
                                LocationBorder(
                                    canvas,
                                    buildingId));

                        var room =
                            Bounds(
                                LocationBorder(
                                    canvas,
                                    roomId));

                        var rack =
                            Bounds(
                                LocationBorder(
                                    canvas,
                                    rackId));

                        Assert.IsTrue(
                            site.Contains(
                                building),
                            "Building must render inside Site from ParentLocationId.");

                        Assert.IsTrue(
                            building.Contains(
                                room),
                            "Room must render inside Building from ParentLocationId.");

                        Assert.IsTrue(
                            room.Contains(
                                rack),
                            "Rack must render inside Room from ParentLocationId.");
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
        public void LocationSelectionIsBrowseOnlyUntilExplicitEdit()
        {
            RunOnSta(
                () =>
                {
                    var locationId =
                        Guid.NewGuid();

                    var service =
                        new RecordingLocationTopologyService(
                            new LocationTopologyLocation(
                                locationId,
                                null,
                                "Existing",
                                "Keep"));

                    var window =
                        new LocationTopologyWindow(
                            service,
                            locationId);

                    try
                    {
                        var name =
                            (TextBox)window.FindName(
                                "LocationNameTextBox");

                        var save =
                            (Button)window.FindName(
                                "LocationSaveButton");

                        var edit =
                            (Button)window.FindName(
                                "LocationEditButton");

                        Assert.IsTrue(
                            name.IsReadOnly,
                            "Selecting a location must be a read-only browse action.");

                        Assert.IsFalse(
                            save.IsEnabled,
                            "Save must not be available merely because an existing location is selected.");

                        Assert.IsTrue(
                            edit.IsEnabled,
                            "Editing must require an explicit operator action.");

                        name.Text =
                            "Accidental rename";

                        InvokePrivate(
                            window,
                            "OnSaveLocationClick");

                        Assert.AreEqual(
                            0,
                            service.UpdateCalls,
                            "A browse selection must never be persisted by the Save command.");

                        Assert.AreEqual(
                            "Existing",
                            service.GetSnapshot()
                                .Locations
                                .Single()
                                .Name);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void CreateCreateEditLifecycleWorksInOneOpenWindow()
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

                        var parent =
                            (ComboBox)window.FindName(
                                "LocationParentComboBox");

                        name.Text =
                            "Site A";

                        InvokePrivate(
                            window,
                            "OnCreateLocationClick");

                        var site =
                            service.GetSnapshot()
                                .Locations
                                .Single();

                        InvokePrivate(
                            window,
                            "OnNewLocationClick");

                        name.Text =
                            "Building 1";

                        SelectOption(
                            parent,
                            site.Id);

                        InvokePrivate(
                            window,
                            "OnCreateLocationClick");

                        var afterBuilding =
                            service.GetSnapshot();

                        Assert.AreEqual(
                            2,
                            afterBuilding.Locations.Count,
                            "A second Create in the same window must create a distinct location.");

                        Assert.AreEqual(
                            "Site A",
                            afterBuilding.Locations
                                .Single(
                                    item =>
                                        item.Id ==
                                        site.Id)
                                .Name,
                            "Creating a child must not silently rename the previously selected location.");

                        var building =
                            afterBuilding.Locations
                                .Single(
                                    item =>
                                        item.Id !=
                                        site.Id);

                        Assert.AreEqual(
                            site.Id,
                            building.ParentLocationId);

                        InvokePrivate(
                            window,
                            "OnEditLocationClick");

                        name.Text =
                            "Building 01";

                        InvokePrivate(
                            window,
                            "OnSaveLocationClick");

                        Assert.AreEqual(
                            1,
                            service.UpdateCalls,
                            "An explicit Edit followed by Save must update exactly one existing location.");

                        Assert.AreEqual(
                            building.Id,
                            service.GetSnapshot()
                                .Locations
                                .Single(
                                    item =>
                                        item.Name ==
                                        "Building 01")
                                .Id,
                            "Rename must preserve stable LocationId.");

                        InvokePrivate(
                            window,
                            "OnNewLocationClick");

                        name.Text =
                            "Room 101";

                        SelectOption(
                            parent,
                            building.Id);

                        InvokePrivate(
                            window,
                            "OnCreateLocationClick");

                        var final =
                            service.GetSnapshot();

                        Assert.AreEqual(
                            3,
                            final.Locations.Count);

                        Assert.AreEqual(
                            building.Id,
                            final.Locations
                                .Single(
                                    item =>
                                        item.Name ==
                                        "Room 101")
                                .ParentLocationId,
                            "Repeated Create must preserve the requested hierarchy in the same open editor.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void PersistedInvalidChildIsContainedWithoutMovingParentAnchor()
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
                                100.0,
                                100.0,
                                320.0,
                                240.0,
                                false,
                                false),
                            new MapLocationLayout(
                                childId,
                                -900.0,
                                -700.0,
                                280.0,
                                220.0,
                                false,
                                false));

                    var parentOnly =
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
                            });

                    var hierarchy =
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
                            });

                    var window =
                        new MainWindow(
                            new EmptyRefreshProvider(),
                            new EmptyLookupReader(),
                            store);

                    try
                    {
                        window.ShowMap(
                            parentOnly);

                        var canvas =
                            MapCanvas(
                                window);

                        var parentBefore =
                            Bounds(
                                LocationBorder(
                                    canvas,
                                    parentId));

                        window.ShowMap(
                            hierarchy);

                        var parentAfter =
                            Bounds(
                                LocationBorder(
                                    canvas,
                                    parentId));

                        var childAfter =
                            Bounds(
                                LocationBorder(
                                    canvas,
                                    childId));

                        Assert.AreEqual(
                            parentBefore.Left,
                            parentAfter.Left,
                            0.001,
                            "Adding or repairing a child must not autonomously move the persisted parent horizontally.");

                        Assert.AreEqual(
                            parentBefore.Top,
                            parentAfter.Top,
                            0.001,
                            "Adding or repairing a child must not autonomously move the persisted parent vertically.");

                        Assert.IsTrue(
                            parentAfter.Contains(
                                childAfter),
                            "Persisted geometry must be repaired by containing the child inside ParentLocationId, not by detaching the hierarchy.");

                        window.ShowMap(
                            hierarchy);

                        var stableParent =
                            Bounds(
                                LocationBorder(
                                    canvas,
                                    parentId));

                        var stableChild =
                            Bounds(
                                LocationBorder(
                                    canvas,
                                    childId));

                        Assert.AreEqual(
                            parentAfter,
                            stableParent,
                            "A repaired parent must not drift on subsequent refreshes.");

                        Assert.AreEqual(
                            childAfter,
                            stableChild,
                            "A repaired child must not drift on subsequent refreshes.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void AssignedNodeIsContainedWithoutMovingPersistedLocationAnchor()
        {
            RunOnSta(
                () =>
                {
                    var locationId =
                        Guid.NewGuid();

                    var store =
                        new RecordingMapLayoutStore(
                            new MapLocationLayout(
                                locationId,
                                200.0,
                                180.0,
                                560.0,
                                360.0,
                                false,
                                false));

                    var locationOnly =
                        new MapSnapshot(
                            Now,
                            new MapNode[0],
                            new MapLink[0],
                            new[]
                            {
                                new MapLocation(
                                    locationId,
                                    null,
                                    "Server room",
                                    null)
                            });

                    var withDevice =
                        new MapSnapshot(
                            Now,
                            new[]
                            {
                                Node(
                                    "device",
                                    "Device",
                                    -1200.0,
                                    -900.0,
                                    locationId)
                            },
                            new MapLink[0],
                            new[]
                            {
                                new MapLocation(
                                    locationId,
                                    null,
                                    "Server room",
                                    null)
                            });

                    var window =
                        new MainWindow(
                            new EmptyRefreshProvider(),
                            new EmptyLookupReader(),
                            store);

                    try
                    {
                        window.ShowMap(
                            locationOnly);

                        var canvas =
                            MapCanvas(
                                window);

                        var before =
                            Bounds(
                                LocationBorder(
                                    canvas,
                                    locationId));

                        window.ShowMap(
                            withDevice);

                        var after =
                            Bounds(
                                LocationBorder(
                                    canvas,
                                    locationId));

                        var node =
                            canvas.Children
                                .OfType<Border>()
                                .Single(
                                    item =>
                                        Panel.GetZIndex(
                                            item) == 2);

                        Assert.AreEqual(
                            before.Left,
                            after.Left,
                            0.001,
                            "Assigning an out-of-bounds device must not drag the Location container to the device.");

                        Assert.AreEqual(
                            before.Top,
                            after.Top,
                            0.001);

                        Assert.IsTrue(
                            after.Contains(
                                Bounds(
                                    node)),
                            "An assigned device must be normalized inside its physical Location.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void ReparentedLocationMovesInsideNewParentWithoutMovingParentAnchors()
        {
            RunOnSta(
                () =>
                {
                    var firstParentId =
                        Guid.NewGuid();

                    var secondParentId =
                        Guid.NewGuid();

                    var childId =
                        Guid.NewGuid();

                    var store =
                        new RecordingMapLayoutStore(
                            new MapLocationLayout(
                                firstParentId,
                                100.0,
                                100.0,
                                640.0,
                                480.0,
                                false,
                                false),
                            new MapLocationLayout(
                                secondParentId,
                                1200.0,
                                900.0,
                                640.0,
                                480.0,
                                false,
                                false),
                            new MapLocationLayout(
                                childId,
                                220.0,
                                220.0,
                                280.0,
                                220.0,
                                false,
                                false));

                    var firstSnapshot =
                        new MapSnapshot(
                            Now,
                            new MapNode[0],
                            new MapLink[0],
                            new[]
                            {
                                new MapLocation(
                                    firstParentId,
                                    null,
                                    "Building A",
                                    null),
                                new MapLocation(
                                    secondParentId,
                                    null,
                                    "Building B",
                                    null),
                                new MapLocation(
                                    childId,
                                    firstParentId,
                                    "Room",
                                    null)
                            });

                    var secondSnapshot =
                        new MapSnapshot(
                            Now,
                            new MapNode[0],
                            new MapLink[0],
                            new[]
                            {
                                new MapLocation(
                                    firstParentId,
                                    null,
                                    "Building A",
                                    null),
                                new MapLocation(
                                    secondParentId,
                                    null,
                                    "Building B",
                                    null),
                                new MapLocation(
                                    childId,
                                    secondParentId,
                                    "Room",
                                    null)
                            });

                    var window =
                        new MainWindow(
                            new EmptyRefreshProvider(),
                            new EmptyLookupReader(),
                            store);

                    try
                    {
                        window.ShowMap(
                            firstSnapshot);

                        var canvas =
                            MapCanvas(
                                window);

                        var secondParentBefore =
                            Bounds(
                                LocationBorder(
                                    canvas,
                                    secondParentId));

                        window.ShowMap(
                            secondSnapshot);

                        var secondParentAfter =
                            Bounds(
                                LocationBorder(
                                    canvas,
                                    secondParentId));

                        var childAfter =
                            Bounds(
                                LocationBorder(
                                    canvas,
                                    childId));

                        Assert.AreEqual(
                            secondParentBefore.Left,
                            secondParentAfter.Left,
                            0.001,
                            "Reparenting must move the child to the new parent, not drag the new parent toward stale child coordinates.");

                        Assert.AreEqual(
                            secondParentBefore.Top,
                            secondParentAfter.Top,
                            0.001);

                        Assert.IsTrue(
                            secondParentAfter.Contains(
                                childAfter),
                            "A reparented Location must be visually contained by its new ParentLocationId immediately after reconciliation.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void MovingParentMovesChildAndAssignedDeviceAsOneSubtree()
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
                                        "device",
                                        "Device",
                                        340,
                                        320,
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

                        var parent =
                            LocationBorder(
                                canvas,
                                parentId);

                        var child =
                            LocationBorder(
                                canvas,
                                childId);

                        var node =
                            canvas.Children
                                .OfType<Border>()
                                .Single(
                                    item =>
                                        Panel.GetZIndex(
                                            item) == 2);

                        var parentStart =
                            Bounds(
                                parent);

                        var childStart =
                            Bounds(
                                child);

                        var nodeStart =
                            Bounds(
                                node);

                        SetPrivateField(
                            window,
                            "_locationDragStartLeft",
                            parentStart.Left);

                        SetPrivateField(
                            window,
                            "_locationDragStartTop",
                            parentStart.Top);

                        InvokePrivate(
                            window,
                            "CaptureLocationSubtreeStarts",
                            parentId);

                        InvokePrivate(
                            window,
                            "CaptureLocationDeviceStarts",
                            parentId);

                        var parentVisual =
                            InvokePrivate(
                                window,
                                "LocationVisual",
                                parentId);

                        InvokePrivate(
                            window,
                            "ApplyLocationDrag",
                            parentVisual,
                            parentStart.Left + 120.0,
                            parentStart.Top + 80.0);

                        var parentEnd =
                            Bounds(parent);

                        var childEnd =
                            Bounds(child);

                        var nodeEnd =
                            Bounds(node);

                        Assert.AreEqual(
                            120.0,
                            parentEnd.Left -
                            parentStart.Left,
                            0.001);

                        Assert.AreEqual(
                            parentEnd.Left -
                            parentStart.Left,
                            childEnd.Left -
                            childStart.Left,
                            0.001,
                            "Moving a parent must move its child frame by the same delta.");

                        Assert.AreEqual(
                            parentEnd.Top -
                            parentStart.Top,
                            childEnd.Top -
                            childStart.Top,
                            0.001);

                        Assert.AreEqual(
                            parentEnd.Left -
                            parentStart.Left,
                            nodeEnd.Left -
                            nodeStart.Left,
                            0.001,
                            "Moving a physical location must move its assigned device without a Shift-only mode.");

                        Assert.AreEqual(
                            parentEnd.Top -
                            parentStart.Top,
                            nodeEnd.Top -
                            nodeStart.Top,
                            0.001);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void ChildDragIsConstrainedInsideParentBounds()
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

                        var parent =
                            LocationBorder(
                                canvas,
                                parentId);

                        var child =
                            LocationBorder(
                                canvas,
                                childId);

                        var childStart =
                            Bounds(
                                child);

                        SetPrivateField(
                            window,
                            "_locationDragStartLeft",
                            childStart.Left);

                        SetPrivateField(
                            window,
                            "_locationDragStartTop",
                            childStart.Top);

                        InvokePrivate(
                            window,
                            "CaptureLocationSubtreeStarts",
                            childId);

                        InvokePrivate(
                            window,
                            "CaptureLocationDeviceStarts",
                            childId);

                        var childVisual =
                            InvokePrivate(
                                window,
                                "LocationVisual",
                                childId);

                        InvokePrivate(
                            window,
                            "ApplyLocationDrag",
                            childVisual,
                            -10000.0,
                            -10000.0);

                        Assert.IsTrue(
                            Bounds(parent)
                                .Contains(
                                    Bounds(child)),
                            "A child location must not be draggable outside its parent boundary.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void ParentResizeCannotClipChildLocation()
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

                        var parent =
                            LocationBorder(
                                canvas,
                                parentId);

                        var child =
                            LocationBorder(
                                canvas,
                                childId);

                        var parentVisual =
                            InvokePrivate(
                                window,
                                "LocationVisual",
                                parentId);

                        var thumb =
                            (Thumb)parentVisual
                                .GetType()
                                .GetProperty(
                                    "ResizeThumb")
                                .GetValue(
                                    parentVisual,
                                    null);

                        InvokePrivate(
                            window,
                            "OnMapLocationResizeDragDelta",
                            thumb,
                            new DragDeltaEventArgs(
                                -10000.0,
                                -10000.0));

                        Assert.IsTrue(
                            Bounds(parent)
                                .Contains(
                                    Bounds(child)),
                            "Resizing a parent must not make it smaller than its child location subtree.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void LocationLockPersistsThroughProductionLayoutPath()
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
                                new MapNode[0],
                                new MapLink[0],
                                new[]
                                {
                                    new MapLocation(
                                        locationId,
                                        null,
                                        "Rack",
                                        null)
                                }));

                        InvokePrivate(
                            window,
                            "ToggleLocationLocked",
                            locationId,
                            null);

                        Assert.IsNotNull(
                            store.LastLocation);

                        Assert.AreEqual(
                            locationId,
                            store.LastLocation.LocationId);

                        Assert.IsTrue(
                            store.LastLocation.IsLocked,
                            "Location lock must persist through the production location layout store path.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void CollapsingParentHidesAndRestoresItsPhysicalSubtree()
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
                                        "device",
                                        "Device",
                                        320,
                                        300,
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

                        var child =
                            LocationBorder(
                                canvas,
                                childId);

                        var node =
                            canvas.Children
                                .OfType<Border>()
                                .Single(
                                    item =>
                                        Panel.GetZIndex(
                                            item) == 2);

                        InvokePrivate(
                            window,
                            "ToggleLocationCollapsed",
                            parentId);

                        Assert.AreEqual(
                            Visibility.Collapsed,
                            child.Visibility,
                            "Collapsing a parent must hide child location frames instead of leaving them detached on the global canvas.");

                        Assert.AreEqual(
                            Visibility.Collapsed,
                            node.Visibility,
                            "Collapsing a parent must hide devices in its descendant locations.");

                        InvokePrivate(
                            window,
                            "ToggleLocationCollapsed",
                            parentId);

                        Assert.AreEqual(
                            Visibility.Visible,
                            child.Visibility);

                        Assert.AreEqual(
                            Visibility.Visible,
                            node.Visibility);
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

        private static void InvokePrivate(
            LocationTopologyWindow window,
            string methodName)
        {
            InvokePrivate(
                (object)window,
                methodName,
                window,
                new RoutedEventArgs());
        }

        private static object InvokePrivate(
            object target,
            string methodName,
            params object[] arguments)
        {
            var method =
                target.GetType()
                    .GetMethod(
                        methodName,
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);

            Assert.IsNotNull(
                method,
                methodName);

            return method.Invoke(
                target,
                arguments);
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            var field =
                target.GetType()
                    .GetField(
                        fieldName,
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);

            Assert.IsNotNull(
                field,
                fieldName);

            field.SetValue(
                target,
                value);
        }

        private static void SelectOption(
            ComboBox comboBox,
            Guid locationId)
        {
            foreach (var item in
                comboBox.Items)
            {
                var property =
                    item.GetType()
                        .GetProperty(
                            "LocationId");

                if (property == null)
                {
                    continue;
                }

                var value =
                    (Guid?)property.GetValue(
                        item,
                        null);

                if (value == locationId)
                {
                    comboBox.SelectedItem =
                        item;
                    return;
                }
            }

            Assert.Fail(
                "Requested location option was not found.");
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

        private sealed class RecordingLocationTopologyService :
            ILocationTopologyService
        {
            private readonly List<LocationTopologyLocation>
                _locations =
                    new List<LocationTopologyLocation>();

            public RecordingLocationTopologyService(
                params LocationTopologyLocation[] locations)
            {
                if (locations != null)
                {
                    _locations.AddRange(
                        locations);
                }
            }

            public int UpdateCalls { get; private set; }

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
                var index =
                    _locations.FindIndex(
                        item =>
                            item.Id ==
                            locationId);

                if (index < 0)
                {
                    throw new InvalidOperationException();
                }

                UpdateCalls++;

                _locations[index] =
                    new LocationTopologyLocation(
                        locationId,
                        parentLocationId,
                        name,
                        description);
            }

            public void DeleteLocation(
                Guid locationId)
            {
                _locations.RemoveAll(
                    item =>
                        item.Id ==
                        locationId);
            }

            public void AssignDevice(
                Guid deviceId,
                Guid? locationId)
            {
            }
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
