using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint49MapViewHistoryTests
    {
        [TestMethod]
        public void NewestEntryIsPoppedFirst()
        {
            var history = new MapViewHistory<int>(5);
            history.Push(1);
            history.Push(2);
            history.Push(3);

            int state;
            Assert.IsTrue(history.TryPop(out state));
            Assert.AreEqual(3, state);
            Assert.IsTrue(history.TryPop(out state));
            Assert.AreEqual(2, state);
            Assert.AreEqual(1, history.Count);
        }

        [TestMethod]
        public void LimitDropsTheOldestEntries()
        {
            var history = new MapViewHistory<int>(3);
            for (var i = 1; i <= 5; i++) history.Push(i);

            Assert.AreEqual(3, history.Count);
            Assert.AreEqual(3, history.Limit);
            int state;
            Assert.IsTrue(history.TryPop(out state));
            Assert.AreEqual(5, state);
            Assert.IsTrue(history.TryPop(out state));
            Assert.AreEqual(4, state);
            Assert.IsTrue(history.TryPop(out state));
            Assert.AreEqual(3, state);
            Assert.IsFalse(history.TryPop(out state));
        }

        [TestMethod]
        public void EmptyHistoryPopsNothingAndClearEmptiesIt()
        {
            var history = new MapViewHistory<string>(2);
            string state;
            Assert.IsFalse(history.TryPop(out state));
            Assert.IsNull(state);

            history.Push("a");
            history.Clear();
            Assert.AreEqual(0, history.Count);
            Assert.IsFalse(history.TryPop(out state));
        }

        [TestMethod]
        public void LimitMustBePositive()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MapViewHistory<int>(0));
        }
    }
}
