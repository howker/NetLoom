using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class MapVirtualWorkspaceTests
    {
        [TestMethod]
        public void LogicalCoordinatesRoundTripAcrossVirtualOrigin()
        {
            const double origin = 500000.0;

            foreach (var logical in
                new[]
                {
                    -250000.0,
                    -123.5,
                    0.0,
                    456.25,
                    250000.0
                })
            {
                var canvas =
                    MapVirtualWorkspace
                        .ToCanvasCoordinate(
                            logical,
                            origin);

                Assert.AreEqual(
                    logical,
                    MapVirtualWorkspace
                        .ToLogicalCoordinate(
                            canvas,
                            origin),
                    0.0001);
            }
        }

        [TestMethod]
        public void ViewportPanRoundTripsAcrossZoom()
        {
            const double origin = 500000.0;
            const double zoom = 0.65;
            const double logicalPan = -1840.5;

            var scrollOffset =
                MapVirtualWorkspace
                    .ToScrollOffset(
                        logicalPan,
                        origin,
                        zoom);

            Assert.AreEqual(
                logicalPan,
                MapVirtualWorkspace
                    .ToLogicalPan(
                        scrollOffset,
                        origin,
                        zoom),
                0.0001);
        }

        [TestMethod]
        public void NegativeLogicalPositionsRemainAddressable()
        {
            const double origin = 500000.0;

            var canvas =
                MapVirtualWorkspace
                    .ToCanvasCoordinate(
                        -400000.0,
                        origin);

            Assert.IsTrue(
                canvas > 0.0);

            Assert.IsTrue(
                canvas < origin);
        }

        [TestMethod]
        public void InvalidZoomIsRejected()
        {
            AssertThrows<ArgumentOutOfRangeException>(
                () =>
                    MapVirtualWorkspace
                        .ToScrollOffset(
                            0.0,
                            500000.0,
                            0.0));

            AssertThrows<ArgumentOutOfRangeException>(
                () =>
                    MapVirtualWorkspace
                        .ToLogicalPan(
                            0.0,
                            500000.0,
                            double.NaN));
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
