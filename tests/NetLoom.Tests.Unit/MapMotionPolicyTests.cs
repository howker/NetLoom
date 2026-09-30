using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class MapMotionPolicyTests
    {
        [TestMethod]
        public void OffModeDisablesEverySemanticMotion()
        {
            foreach (MapMotionKind kind in
                Enum.GetValues(
                    typeof(MapMotionKind)))
            {
                Assert.AreEqual(
                    TimeSpan.Zero,
                    MapMotionPolicy.Duration(
                        MapMotionMode.Off,
                        kind));
            }

            Assert.AreEqual(
                1.0,
                MapMotionPolicy.PulseOpacity(
                    MapMotionMode.Off));
        }

        [TestMethod]
        public void ReducedModeKeepsMotionShorterAndSubtlerThanNormal()
        {
            foreach (MapMotionKind kind in
                Enum.GetValues(
                    typeof(MapMotionKind)))
            {
                Assert.IsTrue(
                    MapMotionPolicy.Duration(
                        MapMotionMode.Reduced,
                        kind) <
                    MapMotionPolicy.Duration(
                        MapMotionMode.Normal,
                        kind));
            }

            Assert.IsTrue(
                MapMotionPolicy.PulseOpacity(
                    MapMotionMode.Reduced) >
                MapMotionPolicy.PulseOpacity(
                    MapMotionMode.Normal));
        }

        [TestMethod]
        public void AlertFocusUsesThreeHalosReducedStaticHoldAndNoMotionWhenOff()
        {
            Assert.AreEqual(
                3,
                MapMotionPolicy.AlertFocusPulseCount(
                    MapMotionMode.Normal));
            Assert.AreEqual(
                0,
                MapMotionPolicy.AlertFocusPulseCount(
                    MapMotionMode.Reduced));
            Assert.AreEqual(
                0,
                MapMotionPolicy.AlertFocusPulseCount(
                    MapMotionMode.Off));

            Assert.AreEqual(
                TimeSpan.Zero,
                MapMotionPolicy.AlertFocusStaticDuration(
                    MapMotionMode.Normal));
            Assert.AreEqual(
                TimeSpan.FromSeconds(2.0),
                MapMotionPolicy.AlertFocusStaticDuration(
                    MapMotionMode.Reduced));
            Assert.AreEqual(
                TimeSpan.Zero,
                MapMotionPolicy.AlertFocusStaticDuration(
                    MapMotionMode.Off));
        }
    }
}
