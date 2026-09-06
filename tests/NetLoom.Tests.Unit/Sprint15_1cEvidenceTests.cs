using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Domain.Topology;
using NetLoom.Topology.Resolution;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint15_1cEvidenceTests
    {
        private static readonly DateTime Now =
            new DateTime(
                2026, 1, 1, 12, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void MaterializerPreservesSlotAndProvenance()
        {
            var linkId = Guid.NewGuid();
            var observationId = Guid.NewGuid();

            var source =
                new TopologyEvidence(
                    TopologyEvidenceKind.Lldp,
                    TopologyEvidenceStrength.Strong,
                    observationId,
                    Now,
                    "192.0.2.10",
                    "lldp-local:index:7",
                    "LLDP adjacency");

            var result =
                new PhysicalLinkEvidenceMaterializer()
                    .Materialize(
                        linkId,
                        new[] { source });

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(linkId, result[0].PhysicalLinkId);

            Assert.AreEqual(
                PhysicalLinkEvidenceKind.Lldp,
                result[0].Kind);

            Assert.AreEqual(
                PhysicalLinkEvidenceStrength.Strong,
                result[0].Strength);

            Assert.AreEqual(
                "lldp-local:index:7",
                result[0].SlotDiscriminator);

            Assert.AreEqual(
                observationId,
                result[0].ObservationId.Value);

            Assert.AreEqual(
                Now,
                result[0].CapturedUtc.Value);
        }

        [TestMethod]
        public void TopologyEvidenceRequiresSlotDiscriminator()
        {
            try
            {
                new TopologyEvidence(
                    TopologyEvidenceKind.Cdp,
                    TopologyEvidenceStrength.Strong,
                    Guid.NewGuid(),
                    Now,
                    "192.0.2.20",
                    null,
                    "CDP adjacency");

                Assert.Fail(
                    "ArgumentException was expected.");
            }
            catch (ArgumentException)
            {
            }
        }
    }
}
