using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.MapLayout;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class MapLayoutModelTests
    {
        [TestMethod]
        public void PhysicalTopologyMapIdentityIsStableAndNonEmpty()
        {
            Assert.AreNotEqual(
                Guid.Empty,
                MapLayoutScope.PhysicalTopologyMapId);

            Assert.AreEqual(
                "Physical topology",
                MapLayoutScope.PhysicalTopologyMapName);
        }

        [TestMethod]
        public void ViewportRejectsInvalidZoomAndCoordinates()
        {
            AssertThrows<ArgumentOutOfRangeException>(
                () =>
                    new MapViewportLayout(
                        0.0,
                        0.0,
                        0.0));

            AssertThrows<ArgumentOutOfRangeException>(
                () =>
                    new MapViewportLayout(
                        1.0,
                        double.NaN,
                        0.0));

            AssertThrows<ArgumentOutOfRangeException>(
                () =>
                    new MapViewportLayout(
                        1.0,
                        0.0,
                        double.PositiveInfinity));
        }

        [TestMethod]
        public void DeviceLayoutRequiresStableDeviceIdentityAndFiniteCoordinates()
        {
            AssertThrows<ArgumentException>(
                () =>
                    new MapDeviceLayout(
                        Guid.Empty,
                        1.0,
                        2.0,
                        false));

            AssertThrows<ArgumentOutOfRangeException>(
                () =>
                    new MapDeviceLayout(
                        Guid.NewGuid(),
                        double.NegativeInfinity,
                        2.0,
                        false));
        }

        [TestMethod]
        public void SnapshotRejectsDuplicateDeviceLayouts()
        {
            var deviceId =
                Guid.NewGuid();

            AssertThrows<ArgumentException>(
                () =>
                    new MapLayoutSnapshot(
                        MapLayoutScope.PhysicalTopologyMapId,
                        new MapViewportLayout(
                            1.0,
                            0.0,
                            0.0),
                        new[]
                        {
                            new MapDeviceLayout(
                                deviceId,
                                10.0,
                                20.0,
                                false),
                            new MapDeviceLayout(
                                deviceId,
                                30.0,
                                40.0,
                                true)
                        }));
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
    }
}
