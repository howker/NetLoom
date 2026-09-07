using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Domain.Topology;
using NetLoom.Topology.Rings;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class PhysicalCycleBasisDetectorTests
    {
        private static readonly DateTime Now =
            new DateTime(
                2026,
                1,
                1,
                12,
                0,
                0,
                DateTimeKind.Utc);

        [TestMethod]
        public void TriangleProducesOneDeterministicCycleBasisElement()
        {
            var a =
                Guid.Parse(
                    "00000000-0000-0000-0000-00000000000a");

            var b =
                Guid.Parse(
                    "00000000-0000-0000-0000-00000000000b");

            var c =
                Guid.Parse(
                    "00000000-0000-0000-0000-00000000000c");

            var ab =
                Link(
                    "00000000-0000-0000-0000-000000000101",
                    a,
                    null,
                    b,
                    null);

            var bc =
                Link(
                    "00000000-0000-0000-0000-000000000102",
                    b,
                    null,
                    c,
                    null);

            var ca =
                Link(
                    "00000000-0000-0000-0000-000000000103",
                    c,
                    null,
                    a,
                    null);

            var detector =
                new PhysicalCycleBasisDetector();

            var first =
                detector.Detect(
                    new[] { ca, ab, bc });

            var second =
                detector.Detect(
                    new[] { bc, ca, ab });

            Assert.AreEqual(
                1,
                first.Count);

            Assert.AreEqual(
                first[0].CycleKey,
                second[0].CycleKey);

            CollectionAssert.AreEqual(
                first[0].PhysicalLinkIds.ToArray(),
                second[0].PhysicalLinkIds.ToArray());

            CollectionAssert.AreEqual(
                new[] { a, b, c }
                    .OrderBy(id => id)
                    .ToArray(),
                first[0].DeviceIds.ToArray());

            Assert.IsFalse(
                first[0].IsParallelLinkCycle);
        }

        [TestMethod]
        public void AcyclicAndDisconnectedGraphProducesNoRing()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();
            var d = Guid.NewGuid();
            var e = Guid.NewGuid();

            var result =
                new PhysicalCycleBasisDetector()
                    .Detect(
                        new[]
                        {
                            Link(
                                Guid.NewGuid(),
                                a,
                                null,
                                b,
                                null),
                            Link(
                                Guid.NewGuid(),
                                b,
                                null,
                                c,
                                null),
                            Link(
                                Guid.NewGuid(),
                                d,
                                null,
                                e,
                                null)
                        });

            Assert.AreEqual(
                0,
                result.Count);
        }

        [TestMethod]
        public void ManualLinkParticipatesInCycleBasis()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();

            var manual =
                Link(
                    Guid.NewGuid(),
                    c,
                    null,
                    a,
                    null,
                    PhysicalLinkStrength.Manual);

            var result =
                new PhysicalCycleBasisDetector()
                    .Detect(
                        new[]
                        {
                            Link(
                                Guid.NewGuid(),
                                a,
                                null,
                                b,
                                null),
                            Link(
                                Guid.NewGuid(),
                                b,
                                null,
                                c,
                                null),
                            manual
                        });

            Assert.AreEqual(
                1,
                result.Count);

            CollectionAssert.Contains(
                result[0].PhysicalLinkIds.ToArray(),
                manual.Id);
        }

        [TestMethod]
        public void ReverseDuplicateIdentityDoesNotCreateFalseParallelCycle()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();

            var interfaceA =
                Guid.NewGuid();

            var interfaceB =
                Guid.NewGuid();

            var forward =
                Link(
                    Guid.NewGuid(),
                    a,
                    interfaceA,
                    b,
                    interfaceB);

            var reverse =
                Link(
                    Guid.NewGuid(),
                    b,
                    interfaceB,
                    a,
                    interfaceA);

            Assert.AreEqual(
                forward.LinkKey,
                reverse.LinkKey);

            var result =
                new PhysicalCycleBasisDetector()
                    .Detect(
                        new[] { reverse, forward });

            Assert.AreEqual(
                0,
                result.Count);
        }

        [TestMethod]
        public void DistinctParallelLinksProduceTwoEdgePhysicalCycle()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();

            var first =
                Link(
                    Guid.NewGuid(),
                    a,
                    Guid.NewGuid(),
                    b,
                    Guid.NewGuid());

            var second =
                Link(
                    Guid.NewGuid(),
                    a,
                    Guid.NewGuid(),
                    b,
                    Guid.NewGuid());

            Assert.AreNotEqual(
                first.LinkKey,
                second.LinkKey);

            var result =
                new PhysicalCycleBasisDetector()
                    .Detect(
                        new[] { second, first });

            Assert.AreEqual(
                1,
                result.Count);

            Assert.IsTrue(
                result[0].IsParallelLinkCycle);

            Assert.AreEqual(
                2,
                result[0].PhysicalLinkIds.Count);

            Assert.AreEqual(
                2,
                result[0].DeviceIds.Count);
        }

        [TestMethod]
        public void HiddenAndStaleLinksRemainPhysicalFactsButArchivedLinkDoesNot()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();

            var hidden =
                Link(
                    Guid.NewGuid(),
                    a,
                    null,
                    b,
                    null,
                    PhysicalLinkStrength.Confirmed,
                    PhysicalLinkFreshness.Fresh,
                    true,
                    false);

            var stale =
                Link(
                    Guid.NewGuid(),
                    b,
                    null,
                    c,
                    null,
                    PhysicalLinkStrength.Confirmed,
                    PhysicalLinkFreshness.Stale,
                    false,
                    false);

            var active =
                Link(
                    Guid.NewGuid(),
                    c,
                    null,
                    a,
                    null);

            var detector =
                new PhysicalCycleBasisDetector();

            var current =
                detector.Detect(
                    new[]
                    {
                        hidden,
                        stale,
                        active
                    });

            Assert.AreEqual(
                1,
                current.Count);

            var archived =
                Link(
                    active.Id,
                    active.DeviceAId,
                    active.InterfaceAId,
                    active.DeviceBId,
                    active.InterfaceBId,
                    active.Strength,
                    active.Freshness,
                    active.IsHidden,
                    true);

            var withoutArchived =
                detector.Detect(
                    new[]
                    {
                        hidden,
                        stale,
                        archived
                    });

            Assert.AreEqual(
                0,
                withoutArchived.Count);
        }

        [TestMethod]
        public void CycleBasisIsBoundedForGraphWithChord()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();
            var d = Guid.NewGuid();

            var result =
                new PhysicalCycleBasisDetector()
                    .Detect(
                        new[]
                        {
                            Link(
                                Guid.NewGuid(),
                                a,
                                null,
                                b,
                                null),
                            Link(
                                Guid.NewGuid(),
                                b,
                                null,
                                c,
                                null),
                            Link(
                                Guid.NewGuid(),
                                c,
                                null,
                                d,
                                null),
                            Link(
                                Guid.NewGuid(),
                                d,
                                null,
                                a,
                                null),
                            Link(
                                Guid.NewGuid(),
                                a,
                                null,
                                c,
                                null)
                        });

            Assert.AreEqual(
                2,
                result.Count);

            Assert.AreEqual(
                2,
                result
                    .Select(item => item.CycleKey)
                    .Distinct(
                        StringComparer.Ordinal)
                    .Count());
        }

        private static PhysicalLink Link(
            string id,
            Guid deviceAId,
            Guid? interfaceAId,
            Guid deviceBId,
            Guid? interfaceBId)
        {
            return Link(
                Guid.Parse(id),
                deviceAId,
                interfaceAId,
                deviceBId,
                interfaceBId);
        }

        private static PhysicalLink Link(
            Guid id,
            Guid deviceAId,
            Guid? interfaceAId,
            Guid deviceBId,
            Guid? interfaceBId,
            PhysicalLinkStrength strength =
                PhysicalLinkStrength.Confirmed,
            PhysicalLinkFreshness freshness =
                PhysicalLinkFreshness.Fresh,
            bool isHidden = false,
            bool isArchived = false)
        {
            return new PhysicalLink(
                id,
                deviceAId,
                interfaceAId,
                deviceBId,
                interfaceBId,
                strength,
                freshness,
                null,
                null,
                "cycle-basis-test",
                Now,
                Now,
                Now,
                "sprint25",
                isHidden,
                isArchived,
                null);
        }
    }
}
