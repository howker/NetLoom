using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Topology.Lifecycle;
using NetLoom.Topology.Map;
using NetLoom.Topology.Resolution;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint13MapProjectionTests
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
        public void PresentationKeyIsNotRawIpOrDeviceId()
        {
            var candidate =
                Candidate(
                    Endpoint(
                        "192.0.2.10",
                        "switch-a",
                        null,
                        null,
                        "Gi1/0/1"),
                    Endpoint(
                        "192.0.2.20",
                        "switch-b",
                        null,
                        null,
                        "Gi1/0/2"),
                    TopologyConfidence.High,
                    Evidence(
                        TopologyEvidenceKind.Lldp));

            var map =
                Project(candidate);

            Assert.AreEqual(2, map.Nodes.Count);

            foreach (var node in map.Nodes)
            {
                Assert.IsFalse(
                    node.Key.Contains("192.0.2."));

                Assert.IsFalse(
                    string.Equals(
                        node.Key,
                        node.Label,
                        StringComparison.OrdinalIgnoreCase));

                Assert.IsTrue(
                    node.Key.StartsWith(
                        "map-",
                        StringComparison.Ordinal));
            }
        }

        [TestMethod]
        public void ReverseCandidateDoesNotDuplicateUndirectedLink()
        {
            var a =
                Endpoint(
                    "192.0.2.10",
                    null,
                    null,
                    null,
                    "Gi1");

            var b =
                Endpoint(
                    "192.0.2.20",
                    null,
                    null,
                    null,
                    "Gi2");

            var first =
                Candidate(
                    a,
                    b,
                    TopologyConfidence.High,
                    Evidence(
                        TopologyEvidenceKind.Lldp));

            var second =
                Candidate(
                    Endpoint(
                        "192.0.2.20",
                        null,
                        null,
                        null,
                        "Gi2"),
                    Endpoint(
                        "192.0.2.10",
                        null,
                        null,
                        null,
                        "Gi1"),
                    TopologyConfidence.High,
                    Evidence(
                        TopologyEvidenceKind.Cdp));

            var projector =
                new TopologyMapProjector();

            var map =
                projector.Project(
                    new[]
                    {
                        new MapProjectionItem(
                            first,
                            TopologyFreshness.Fresh),
                        new MapProjectionItem(
                            second,
                            TopologyFreshness.Fresh)
                    },
                    Now);

            Assert.AreEqual(2, map.Nodes.Count);
            Assert.AreEqual(1, map.Links.Count);
            Assert.AreEqual(
                2,
                map.Links[0].Evidence.Count);
        }

        [TestMethod]
        public void LayoutIsDeterministicForInputOrder()
        {
            var first =
                Candidate(
                    Endpoint(
                        "192.0.2.1",
                        "a",
                        null,
                        null,
                        "1"),
                    Endpoint(
                        "192.0.2.2",
                        "b",
                        null,
                        null,
                        "2"),
                    TopologyConfidence.High,
                    Evidence(
                        TopologyEvidenceKind.Lldp));

            var second =
                Candidate(
                    Endpoint(
                        "192.0.2.2",
                        "b",
                        null,
                        null,
                        "3"),
                    Endpoint(
                        "192.0.2.3",
                        "c",
                        null,
                        null,
                        "4"),
                    TopologyConfidence.Medium,
                    Evidence(
                        TopologyEvidenceKind.Cdp));

            var projector =
                new TopologyMapProjector();

            var map1 =
                projector.Project(
                    new[]
                    {
                        new MapProjectionItem(
                            first,
                            TopologyFreshness.Fresh),
                        new MapProjectionItem(
                            second,
                            TopologyFreshness.Aging)
                    },
                    Now);

            var map2 =
                projector.Project(
                    new[]
                    {
                        new MapProjectionItem(
                            second,
                            TopologyFreshness.Aging),
                        new MapProjectionItem(
                            first,
                            TopologyFreshness.Fresh)
                    },
                    Now);

            Assert.AreEqual(
                string.Join(
                    "|",
                    map1.Nodes.Select(
                        node =>
                            node.Key + ":" +
                            node.X + ":" +
                            node.Y)),
                string.Join(
                    "|",
                    map2.Nodes.Select(
                        node =>
                            node.Key + ":" +
                            node.X + ":" +
                            node.Y)));

            Assert.AreEqual(
                string.Join(
                    "|",
                    map1.Links.Select(
                        link => link.Key)),
                string.Join(
                    "|",
                    map2.Links.Select(
                        link => link.Key)));
        }

        [TestMethod]
        public void DuplicateEvidenceMergesConfidenceAndFreshness()
        {
            var a =
                Endpoint(
                    "192.0.2.10",
                    null,
                    null,
                    null,
                    "Gi1");

            var b =
                Endpoint(
                    "192.0.2.20",
                    null,
                    null,
                    null,
                    "Gi2");

            var medium =
                Candidate(
                    a,
                    b,
                    TopologyConfidence.Medium,
                    Evidence(
                        TopologyEvidenceKind.Cdp));

            var high =
                Candidate(
                    a,
                    b,
                    TopologyConfidence.High,
                    Evidence(
                        TopologyEvidenceKind.Lldp));

            var map =
                new TopologyMapProjector()
                    .Project(
                        new[]
                        {
                            new MapProjectionItem(
                                medium,
                                TopologyFreshness.Stale),
                            new MapProjectionItem(
                                high,
                                TopologyFreshness.Fresh)
                        },
                        Now);

            Assert.AreEqual(1, map.Links.Count);

            Assert.AreEqual(
                MapConfidence.High,
                map.Links[0].Confidence);

            Assert.AreEqual(
                MapFreshness.Fresh,
                map.Links[0].Freshness);

            Assert.AreEqual(
                2,
                map.Links[0].Evidence.Count);
        }

        [TestMethod]
        public void SelfLinkIsNotProjected()
        {
            var candidate =
                Candidate(
                    Endpoint(
                        "192.0.2.10",
                        null,
                        null,
                        null,
                        "Gi1"),
                    Endpoint(
                        "192.0.2.10",
                        null,
                        null,
                        null,
                        "Gi2"),
                    TopologyConfidence.High,
                    Evidence(
                        TopologyEvidenceKind.Lldp));

            var map =
                Project(candidate);

            Assert.AreEqual(0, map.Nodes.Count);
            Assert.AreEqual(0, map.Links.Count);
        }

        private static MapSnapshot Project(
            PhysicalLinkCandidate candidate)
        {
            return new TopologyMapProjector()
                .Project(
                    new[]
                    {
                        new MapProjectionItem(
                            candidate,
                            TopologyFreshness.Fresh)
                    },
                    Now);
        }

        private static PhysicalLinkCandidate Candidate(
            LinkEndpointClaim local,
            LinkEndpointClaim remote,
            TopologyConfidence confidence,
            TopologyEvidence evidence)
        {
            return new PhysicalLinkCandidate(
                local,
                remote,
                confidence,
                new[] { evidence });
        }

        private static LinkEndpointClaim Endpoint(
            string managementAddress,
            string systemName,
            string chassisId,
            string deviceId,
            string portId)
        {
            return new LinkEndpointClaim(
                managementAddress,
                systemName,
                chassisId,
                deviceId,
                LinkPortReferenceKind.ProtocolPortId,
                null,
                portId,
                null);
        }

        private static TopologyEvidence Evidence(
            TopologyEvidenceKind kind)
        {
            return new TopologyEvidence(
                kind,
                TopologyEvidenceStrength.Strong,
                Guid.NewGuid(),
                Now,
                "192.0.2.254",
                "test-slot",
                "test evidence");
        }
    }
}
