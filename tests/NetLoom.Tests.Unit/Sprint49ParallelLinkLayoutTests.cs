using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.TopologyMap;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint49ParallelLinkLayoutTests
    {
        [TestMethod]
        public void SingleLinkHasZeroSlot()
        {
            var slot = ParallelLinkLayout.Slots(new[] { Link("one", "A", "B") })["one"];
            Assert.AreEqual(0, slot.Slot);
            Assert.AreEqual(1, slot.GroupSize);
            Assert.IsTrue(slot.SourceIsCanonicalFirst);
        }

        [TestMethod]
        public void TwoLinksUseIdentityOrderAndCanonicalNodePair()
        {
            var slots = ParallelLinkLayout.Slots(new[] { Link("z", "B", "A"), Link("a", "A", "B") });
            Assert.AreEqual(-1, slots["a"].Slot);
            Assert.AreEqual(1, slots["z"].Slot);
            Assert.AreEqual(2, slots["a"].GroupSize);
            Assert.AreEqual(2, slots["z"].GroupSize);
            Assert.IsTrue(slots["a"].SourceIsCanonicalFirst);
            Assert.IsFalse(slots["z"].SourceIsCanonicalFirst);
        }

        [TestMethod]
        public void ThreeLinksAreSymmetricAndIndependentOfEnumerationOrder()
        {
            var slots = ParallelLinkLayout.Slots(new[]
            {
                Link("c", "A", "B"), Link("a", "A", "B"), Link("b", "B", "A")
            });
            var reversed = ParallelLinkLayout.Slots(new[]
            {
                Link("b", "B", "A"), Link("a", "A", "B"), Link("c", "A", "B")
            });
            Assert.AreEqual(-2, slots["a"].Slot);
            Assert.AreEqual(0, slots["b"].Slot);
            Assert.AreEqual(2, slots["c"].Slot);
            foreach (var identity in new[] { "a", "b", "c" })
            {
                Assert.AreEqual(3, slots[identity].GroupSize);
                Assert.AreEqual(slots[identity].Slot, reversed[identity].Slot);
            }
        }

        [TestMethod]
        public void LoopsAndOtherPairsDoNotChangeGroupSize()
        {
            var slots = ParallelLinkLayout.Slots(new[]
            {
                Link("loop-a", "A", "A"), Link("loop-b", "A", "A"),
                Link("ab", "A", "B"), Link("ac", "A", "C"),
                Link("delimited-one", "A|B", "C"), Link("delimited-two", "A", "B|C")
            });
            foreach (var slot in slots.Values)
            {
                Assert.AreEqual(0, slot.Slot);
                Assert.AreEqual(1, slot.GroupSize);
            }
        }

        [TestMethod]
        public void HorizontalOffsetUsesSameSideForReversedDirection()
        {
            double x1, y1, x2, y2;
            ParallelLinkLayout.Offset(0, 0, 100, 0, 2, 7, true, out x1, out y1, out x2, out y2);
            Assert.AreEqual(0.0, x1, 0.001);
            Assert.AreEqual(100.0, x2, 0.001);
            Assert.AreEqual(14.0, y1, 0.001);
            Assert.AreEqual(y1, y2, 0.001);

            double reverseX1, reverseY1, reverseX2, reverseY2;
            ParallelLinkLayout.Offset(100, 0, 0, 0, 2, 7, false,
                out reverseX1, out reverseY1, out reverseX2, out reverseY2);
            Assert.AreEqual(x2, reverseX1, 0.001);
            Assert.AreEqual(x1, reverseX2, 0.001);
            Assert.AreEqual(y1, reverseY1, 0.001);
            Assert.AreEqual(y2, reverseY2, 0.001);
        }

        [TestMethod]
        public void DiagonalOffsetIsPerpendicularAndPreservesLength()
        {
            double x1, y1, x2, y2;
            ParallelLinkLayout.Offset(0, 0, 3, 4, -1, 10, true, out x1, out y1, out x2, out y2);
            Assert.AreEqual(8.0, x1, 0.001);
            Assert.AreEqual(-6.0, y1, 0.001);
            Assert.AreEqual(3.0, x2 - x1, 0.001);
            Assert.AreEqual(4.0, y2 - y1, 0.001);
            Assert.AreEqual(0.0, (3 * x1) + (4 * y1), 0.001);
        }

        [TestMethod]
        public void ZeroLengthAndZeroSlotLeaveEndpointsUnchanged()
        {
            double x1, y1, x2, y2;
            ParallelLinkLayout.Offset(3, 4, 3, 4, 2, 7, true, out x1, out y1, out x2, out y2);
            Assert.AreEqual(3.0, x1);
            Assert.AreEqual(4.0, y1);
            Assert.AreEqual(x1, x2);
            Assert.AreEqual(y1, y2);
            ParallelLinkLayout.Offset(3, 4, 30, 40, 0, 7, true, out x1, out y1, out x2, out y2);
            Assert.AreEqual(3.0, x1);
            Assert.AreEqual(4.0, y1);
            Assert.AreEqual(30.0, x2);
            Assert.AreEqual(40.0, y2);
        }

        [TestMethod]
        public void HalfSpacingCapsTotalSpread()
        {
            Assert.AreEqual(0.0, ParallelLinkLayout.HalfSpacing(0, 14, 48));
            Assert.AreEqual(0.0, ParallelLinkLayout.HalfSpacing(1, 14, 48));
            Assert.AreEqual(7.0, ParallelLinkLayout.HalfSpacing(2, 14, 48));
            Assert.AreEqual(7.0, ParallelLinkLayout.HalfSpacing(3, 14, 48));
            var half = ParallelLinkLayout.HalfSpacing(10, 14, 48);
            Assert.AreEqual(48.0, 2 * half * (10 - 1), 0.001);
        }

        [TestMethod]
        public void RequiredIdentitiesAndUniqueLinkIdentityAreValidated()
        {
            Assert.ThrowsExactly<ArgumentException>(() => Link("", "A", "B"));
            Assert.ThrowsExactly<ArgumentException>(() => Link("link", null, "B"));
            Assert.ThrowsExactly<ArgumentException>(() => Link("link", "A", " "));
            Assert.ThrowsExactly<ArgumentNullException>(() => ParallelLinkLayout.Slots(null));
            Assert.ThrowsExactly<InvalidOperationException>(() => ParallelLinkLayout.Slots(new[]
            {
                Link("same", "A", "B"), Link("same", "B", "A")
            }));
        }

        private static ParallelLinkEndpoints Link(string identity, string source, string target)
        {
            return new ParallelLinkEndpoints(identity, source, target);
        }
    }
}
