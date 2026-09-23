using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Export;
using NetLoom.Application.MapLayout;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.TopologyMap;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class
        Sprint44TopologyExportSnapshotTests
    {
        [TestMethod]
        public void
            ProviderUsesOneCoherentRefreshSnapshotAndExpandsCollapsedLocations()
        {
            var mapId =
                MapLayoutScope.PhysicalTopologyMapId;

            var deviceId =
                Guid.NewGuid();

            var locationId =
                Guid.NewGuid();

            var topology =
                CreateTopologySnapshot();

            var persisted =
                new MapLayoutSnapshot(
                    mapId,
                    new MapViewportLayout(
                        0.42,
                        1234.0,
                        -567.0),
                    new[]
                    {
                        new MapDeviceLayout(
                            deviceId,
                            120.0,
                            240.0,
                            true)
                    },
                    new[]
                    {
                        new MapLocationLayout(
                            locationId,
                            50.0,
                            75.0,
                            640.0,
                            480.0,
                            true,
                            true)
                    });

            var refreshProvider =
                new RecordingRefreshProvider(
                    topology);

            var layoutStore =
                new RecordingMapLayoutStore(
                    persisted);

            var provider =
                new TopologyExportSnapshotProvider(
                    refreshProvider,
                    layoutStore,
                    mapId,
                    " cist ");

            var result =
                provider.GetSnapshot();

            Assert.AreSame(
                topology,
                result.Topology,
                "Export must reuse the coherent product refresh snapshot instead of projecting a second topology.");

            Assert.AreEqual(
                1,
                refreshProvider.CallCount);

            Assert.AreEqual(
                "cist",
                refreshProvider.LastStpInstanceId);

            Assert.AreEqual(
                1,
                layoutStore.LoadCount);

            Assert.AreEqual(
                mapId,
                layoutStore.LastMapId);

            Assert.AreEqual(
                mapId,
                result.Layout.MapId);

            Assert.AreEqual(
                1,
                result.Layout.Devices.Count);

            Assert.AreEqual(
                deviceId,
                result.Layout.Devices[0].DeviceId);

            Assert.AreEqual(
                120.0,
                result.Layout.Devices[0].X);

            Assert.AreEqual(
                240.0,
                result.Layout.Devices[0].Y);

            Assert.IsTrue(
                result.Layout.Devices[0].IsLocked);

            Assert.AreEqual(
                1,
                result.Layout.Locations.Count);

            Assert.AreEqual(
                locationId,
                result.Layout.Locations[0].LocationId);

            Assert.AreEqual(
                50.0,
                result.Layout.Locations[0].X);

            Assert.AreEqual(
                75.0,
                result.Layout.Locations[0].Y);

            Assert.AreEqual(
                640.0,
                result.Layout.Locations[0].Width);

            Assert.AreEqual(
                480.0,
                result.Layout.Locations[0].Height);

            Assert.IsFalse(
                result.Layout.Locations[0].IsCollapsed,
                "Collapsed live/persisted Locations must be expanded only in the export model.");

            Assert.IsTrue(
                result.Layout.Locations[0].IsLocked);

            Assert.IsTrue(
                persisted.Locations[0].IsCollapsed,
                "Export normalization must not mutate persisted Location collapse state.");
        }

        [TestMethod]
        public void
            ProviderWithoutPersistedLayoutReturnsEmptyViewportIndependentLayout()
        {
            var mapId =
                MapLayoutScope.PhysicalTopologyMapId;

            var topology =
                CreateTopologySnapshot();

            var refreshProvider =
                new RecordingRefreshProvider(
                    topology);

            var layoutStore =
                new RecordingMapLayoutStore(
                    null);

            var provider =
                new TopologyExportSnapshotProvider(
                    refreshProvider,
                    layoutStore,
                    mapId,
                    "cist");

            var result =
                provider.GetSnapshot();

            Assert.AreSame(
                topology,
                result.Topology);

            Assert.AreEqual(
                mapId,
                result.Layout.MapId);

            Assert.AreEqual(
                0,
                result.Layout.Devices.Count);

            Assert.AreEqual(
                0,
                result.Layout.Locations.Count);
        }

        [TestMethod]
        public void
            ExportLayoutRejectsPersistedSnapshotFromAnotherMap()
        {
            var expectedMapId =
                MapLayoutScope.PhysicalTopologyMapId;

            var otherMapId =
                Guid.NewGuid();

            var persisted =
                new MapLayoutSnapshot(
                    otherMapId,
                    new MapViewportLayout(
                        1.0,
                        0.0,
                        0.0),
                    new MapDeviceLayout[0]);

            AssertThrows<InvalidOperationException>(
                () =>
                    TopologyExportLayoutSnapshot
                        .FromPersisted(
                            expectedMapId,
                            persisted));
        }

        private static TopologyRefreshSnapshot
            CreateTopologySnapshot()
        {
            var capturedUtc =
                new DateTime(
                    2026,
                    9,
                    23,
                    12,
                    0,
                    0,
                    DateTimeKind.Utc);

            return new TopologyRefreshSnapshot(
                new MapSnapshot(
                    capturedUtc,
                    new MapNode[0],
                    new MapLink[0]),
                new TopologyAlertSnapshot(
                    capturedUtc,
                    "cist",
                    new TopologyAlert[0]));
        }

        private static void AssertThrows<TException>(
            Action action)
            where TException : Exception
        {
            try
            {
                action();

                Assert.Fail(
                    "Expected exception: " +
                    typeof(TException).FullName);
            }
            catch (TException)
            {
            }
        }

        private sealed class
            RecordingRefreshProvider :
            ITopologyRefreshSnapshotProvider
        {
            private readonly TopologyRefreshSnapshot
                _snapshot;

            public RecordingRefreshProvider(
                TopologyRefreshSnapshot snapshot)
            {
                _snapshot =
                    snapshot ??
                    throw new ArgumentNullException(
                        nameof(snapshot));
            }

            public int CallCount { get; private set; }

            public string LastStpInstanceId { get; private set; }

            public TopologyRefreshSnapshot
                GetSnapshot(
                    string stpInstanceId)
            {
                CallCount++;
                LastStpInstanceId =
                    stpInstanceId;

                return _snapshot;
            }
        }

        private sealed class
            RecordingMapLayoutStore :
            IMapLayoutStore
        {
            private readonly MapLayoutSnapshot
                _snapshot;

            public RecordingMapLayoutStore(
                MapLayoutSnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public int LoadCount { get; private set; }

            public Guid LastMapId { get; private set; }

            public MapLayoutSnapshot Load(
                Guid mapId)
            {
                LoadCount++;
                LastMapId = mapId;

                return _snapshot;
            }

            public void SaveViewport(
                Guid mapId,
                MapViewportLayout viewport)
            {
                throw new InvalidOperationException(
                    "Export snapshot capture must not persist viewport state.");
            }

            public void SaveDevice(
                Guid mapId,
                MapDeviceLayout deviceLayout)
            {
                throw new InvalidOperationException(
                    "Export snapshot capture must not persist device layout.");
            }
        }
    }
}
