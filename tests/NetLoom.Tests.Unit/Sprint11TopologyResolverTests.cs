using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Cdp;
using NetLoom.Domain.Observations.Lldp;
using NetLoom.Topology.Correlation;
using NetLoom.Topology.Resolution;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint11TopologyResolverTests
    {
        [TestMethod]
        public void LldpCreatesStrongHighConfidenceCandidate()
        {
            var localPort =
                new LldpLocalPort(
                    7,
                    5,
                    "Gi1/0/7",
                    "uplink");

            var lldp =
                new LldpObservation(
                    Observation(
                        ObservationKind.Lldp,
                        "192.0.2.10"),
                    new[]
                    {
                        new LldpRemoteNeighbor(
                            1,
                            7,
                            1,
                            4,
                            "00:11:22:33:44:55",
                            5,
                            "Gi0/1",
                            "uplink",
                            "switch-b",
                            null,
                            null,
                            null,
                            localPort)
                    });

            var result =
                new TopologyResolver().Resolve(
                    new TopologyResolutionInput(
                        new[] { lldp },
                        new CdpObservation[0],
                        new MacCorrelation[0]));

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(
                TopologyConfidence.High,
                result[0].Confidence);

            Assert.AreEqual(
                LinkPortReferenceKind.LldpLocalPortNumber,
                result[0].LocalEndpoint.PortReferenceKind);

            Assert.AreEqual(
                7,
                result[0].LocalEndpoint.PortIndex);

            Assert.AreEqual(
                TopologyEvidenceKind.Lldp,
                result[0].Evidence[0].Kind);

            Assert.AreEqual(
                TopologyEvidenceStrength.Strong,
                result[0].Evidence[0].Strength);
        }

        [TestMethod]
        public void CdpCacheIndexIsNotRepresentedAsIfIndex()
        {
            var cdp =
                new CdpObservation(
                    Observation(
                        ObservationKind.Cdp,
                        "192.0.2.20"),
                    new[]
                    {
                        CdpNeighbor(
                            17,
                            "192.0.2.50",
                            "switch-c",
                            "Gi0/24")
                    });

            var result =
                new TopologyResolver().Resolve(
                    new TopologyResolutionInput(
                        new LldpObservation[0],
                        new[] { cdp },
                        new MacCorrelation[0]));

            Assert.AreEqual(
                LinkPortReferenceKind.CdpCacheIfIndex,
                result[0].LocalEndpoint.PortReferenceKind);

            Assert.AreEqual(
                17,
                result[0].LocalEndpoint.PortIndex);
        }

        [TestMethod]
        public void FdbArpCorrelationAloneCreatesNoLinkCandidate()
        {
            var correlations =
                new[]
                {
                    new MacCorrelation(
                        "192.0.2.50",
                        "00:11:22:33:44:55",
                        "192.0.2.1",
                        10,
                        "192.0.2.20",
                        5,
                        101)
                };

            var result =
                new TopologyResolver().Resolve(
                    new TopologyResolutionInput(
                        new LldpObservation[0],
                        new CdpObservation[0],
                        correlations));

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void MatchingCdpAddressAttachesWeakCorrelation()
        {
            var cdp =
                new CdpObservation(
                    Observation(
                        ObservationKind.Cdp,
                        "192.0.2.20"),
                    new[]
                    {
                        CdpNeighbor(
                            17,
                            "192.0.2.50",
                            "switch-c",
                            "Gi0/24")
                    });

            var correlation =
                new MacCorrelation(
                    "192.0.2.50",
                    "00:11:22:33:44:55",
                    "192.0.2.1",
                    10,
                    "192.0.2.20",
                    5,
                    101);

            var result =
                new TopologyResolver().Resolve(
                    new TopologyResolutionInput(
                        new LldpObservation[0],
                        new[] { cdp },
                        new[] { correlation }));

            Assert.AreEqual(
                2,
                result[0].Evidence.Count);

            Assert.AreEqual(
                TopologyEvidenceKind.ArpFdbCorrelation,
                result[0].Evidence[1].Kind);

            Assert.AreEqual(
                TopologyEvidenceStrength.Weak,
                result[0].Evidence[1].Strength);
        }

        [TestMethod]
        public void MatchingLldpChassisMacAttachesWeakCorrelation()
        {
            var lldp =
                new LldpObservation(
                    Observation(
                        ObservationKind.Lldp,
                        "192.0.2.30"),
                    new[]
                    {
                        new LldpRemoteNeighbor(
                            1,
                            4,
                            1,
                            4,
                            "00-11-22-33-44-55",
                            5,
                            "Gi0/1",
                            null,
                            "switch-d",
                            null,
                            null,
                            null,
                            new LldpLocalPort(
                                4,
                                5,
                                "Gi1/0/4",
                                null))
                    });

            var correlation =
                new MacCorrelation(
                    "198.51.100.10",
                    "00:11:22:33:44:55",
                    "192.0.2.1",
                    10,
                    "192.0.2.30",
                    8,
                    null);

            var result =
                new TopologyResolver().Resolve(
                    new TopologyResolutionInput(
                        new[] { lldp },
                        new CdpObservation[0],
                        new[] { correlation }));

            Assert.AreEqual(
                2,
                result[0].Evidence.Count);
        }

        [TestMethod]
        public void UnrelatedCorrelationIsNotAttached()
        {
            var cdp =
                new CdpObservation(
                    Observation(
                        ObservationKind.Cdp,
                        "192.0.2.20"),
                    new[]
                    {
                        CdpNeighbor(
                            17,
                            "192.0.2.50",
                            "switch-c",
                            "Gi0/24")
                    });

            var correlation =
                new MacCorrelation(
                    "203.0.113.99",
                    "AA:BB:CC:DD:EE:FF",
                    "192.0.2.1",
                    10,
                    "192.0.2.20",
                    5,
                    101);

            var result =
                new TopologyResolver().Resolve(
                    new TopologyResolutionInput(
                        new LldpObservation[0],
                        new[] { cdp },
                        new[] { correlation }));

            Assert.AreEqual(
                1,
                result[0].Evidence.Count);
        }

        private static Observation Observation(
            ObservationKind kind,
            string sourceAddress)
        {
            return new Observation(
                Guid.NewGuid(),
                kind,
                sourceAddress,
                DateTime.UtcNow);
        }

        private static CdpRemoteNeighbor CdpNeighbor(
            int cacheIfIndex,
            string address,
            string systemName,
            string devicePort)
        {
            return new CdpRemoteNeighbor(
                cacheIfIndex,
                1,
                1,
                address,
                "IOS",
                systemName,
                devicePort,
                "Cisco",
                null,
                null,
                null,
                systemName,
                null,
                1,
                address,
                null,
                null);
        }
    }
}
