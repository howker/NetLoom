using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring.Interfaces;
using NetLoom.Application.Observations.Stp;
using NetLoom.Application.Topology;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.StpTree;
using NetLoom.Domain.Locations;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Stp;
using NetLoom.Domain.Topology;
using NetLoom.Topology.Diagnostics;
using NetLoom.Topology.Map;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class MaterializedTopologyDiagnosticSnapshotProjectorTests
    {
        private static readonly DateTime Now =
            new DateTime(
                2026,
                9,
                16,
                12,
                0,
                0,
                DateTimeKind.Utc);

        [TestMethod]
        public void DeviceDiagnosticUsesReadableNamesAndDurableDegradation()
        {
            var deviceId = Guid.NewGuid();
            var interfaceId = Guid.NewGuid();

            var device =
                Device(
                    deviceId,
                    "Core switch");

            var networkInterface =
                Interface(
                    interfaceId,
                    deviceId,
                    101,
                    "sfp1");

            var readSet =
                ReadSet(
                    new[] { device },
                    new[] { networkInterface },
                    new PhysicalLink[0],
                    new[]
                    {
                        new InterfaceDegradationState(
                            deviceId,
                            101,
                            Now,
                            InterfaceDegradationStatus.Degraded,
                            "4,5")
                    },
                    new BoundStpObservation[0]);

            var map =
                new MaterializedTopologyMapProjector()
                    .Project(
                        readSet.Devices,
                        readSet.Interfaces,
                        readSet.PhysicalLinks,
                        readSet.PhysicalLinkEvidence,
                        readSet.Locations,
                        Now);

            var snapshot =
                new MaterializedTopologyDiagnosticSnapshotProjector()
                    .Project(
                        readSet,
                        map,
                        "cist");

            var diagnostic =
                snapshot.Devices.Single();

            Assert.AreEqual(
                "Core switch",
                diagnostic.DisplayName);

            var interfaceDiagnostic =
                diagnostic.Interfaces.Single();

            Assert.AreEqual(
                "sfp1",
                interfaceDiagnostic.DisplayName);

            Assert.AreEqual(
                DiagnosticDegradationStatus.Degraded,
                interfaceDiagnostic.DegradationStatus);

            CollectionAssert.AreEqual(
                new[]
                {
                    DiagnosticDegradationReason.ErrorRateThresholdExceeded,
                    DiagnosticDegradationReason.DiscardRateThresholdExceeded
                },
                interfaceDiagnostic.DegradationReasons.ToArray());
        }

        [TestMethod]
        public void InterfaceDiagnosticBindsProjectedStpByStableInterfaceId()
        {
            var deviceId = Guid.NewGuid();
            var interfaceId = Guid.NewGuid();

            var device =
                Device(
                    deviceId,
                    "Distribution");

            var networkInterface =
                Interface(
                    interfaceId,
                    deviceId,
                    101,
                    "port-101");

            var observationId = Guid.NewGuid();

            var raw =
                new Observation(
                    observationId,
                    ObservationKind.Stp,
                    "192.0.2.1",
                    Now);

            var stp =
                new StpObservation(
                    raw,
                    "cist",
                    null,
                    "8000.001122334455",
                    0,
                    5,
                    101,
                    new[]
                    {
                        new StpPortState(
                            5,
                            101,
                            null,
                            5,
                            1,
                            20000,
                            null,
                            null,
                            null,
                            null,
                            null)
                    });

            var readSet =
                ReadSet(
                    new[] { device },
                    new[] { networkInterface },
                    new PhysicalLink[0],
                    new InterfaceDegradationState[0],
                    new[]
                    {
                        new BoundStpObservation(
                            deviceId,
                            stp)
                    });

            var map =
                new MaterializedTopologyMapProjector()
                    .Project(
                        readSet.Devices,
                        readSet.Interfaces,
                        readSet.PhysicalLinks,
                        readSet.PhysicalLinkEvidence,
                        readSet.Locations,
                        Now);

            var snapshot =
                new MaterializedTopologyDiagnosticSnapshotProjector()
                    .Project(
                        readSet,
                        map,
                        "cist");

            Assert.AreEqual(
                StpTreePortState.Forwarding,
                snapshot.Devices.Single()
                    .Interfaces.Single()
                    .StpState);
        }

        [TestMethod]
        public void LinkDiagnosticIncludesFreshnessEvidenceAndFailureImpact()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();
            var ab = Guid.NewGuid();
            var bc = Guid.NewGuid();

            var devices =
                new[]
                {
                    Device(a, "A"),
                    Device(b, "B"),
                    Device(c, "C")
                };

            var links =
                new[]
                {
                    Link(ab, a, b),
                    Link(bc, b, c)
                };

            var evidence =
                new[]
                {
                    new PhysicalLinkEvidence(
                        ab,
                        PhysicalLinkEvidenceKind.Lldp,
                        PhysicalLinkEvidenceStrength.Strong,
                        "192.0.2.10",
                        "port:1",
                        Guid.NewGuid(),
                        Now,
                        "neighbor")
                };

            var readSet =
                new MaterializedTopologyReadSet(
                    devices,
                    new DeviceInterface[0],
                    links,
                    evidence,
                    new Location[0],
                    new BoundStpObservation[0],
                    new InterfaceDegradationState[0],
                    new[]
                    {
                        new PhysicalLinkEvidenceExplanation(
                            evidence[0],
                            ObservationRawAvailability.Expired)
                    });

            var map =
                new MaterializedTopologyMapProjector()
                    .Project(
                        readSet.Devices,
                        readSet.Interfaces,
                        readSet.PhysicalLinks,
                        readSet.PhysicalLinkEvidence,
                        readSet.Locations,
                        Now);

            var diagnostic =
                new MaterializedTopologyDiagnosticSnapshotProjector()
                    .Project(
                        readSet,
                        map,
                        "cist")
                    .Links
                    .Single(item => item.PhysicalLinkId == ab);

            Assert.IsTrue(diagnostic.IsBridge);

            CollectionAssert.AreEqual(
                new[] { 1, 2 },
                new[]
                {
                    diagnostic.SideADeviceCount,
                    diagnostic.SideBDeviceCount
                }
                .OrderBy(item => item)
                .ToArray());

            Assert.AreEqual(2L, diagnostic.SeparatedDevicePairCount);
            Assert.AreEqual(1, diagnostic.Evidence.Count);
            Assert.AreEqual(
                DiagnosticRawAvailability.Expired,
                diagnostic.Evidence[0].RawAvailability);
            Assert.AreEqual(Now, diagnostic.LastSeenUtc);
        }

        [TestMethod]
        public void RedundantLinkReportsAlternativePhysicalPath()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();

            var links =
                new[]
                {
                    Link(Guid.NewGuid(), a, b),
                    Link(Guid.NewGuid(), b, c),
                    Link(Guid.NewGuid(), c, a)
                };

            var readSet =
                ReadSet(
                    new[]
                    {
                        Device(a, "A"),
                        Device(b, "B"),
                        Device(c, "C")
                    },
                    new DeviceInterface[0],
                    links,
                    new InterfaceDegradationState[0],
                    new BoundStpObservation[0]);

            var map =
                new MaterializedTopologyMapProjector()
                    .Project(
                        readSet.Devices,
                        readSet.Interfaces,
                        readSet.PhysicalLinks,
                        readSet.PhysicalLinkEvidence,
                        readSet.Locations,
                        Now);

            var diagnostics =
                new MaterializedTopologyDiagnosticSnapshotProjector()
                    .Project(
                        readSet,
                        map,
                        "cist");

            Assert.IsTrue(
                diagnostics.Links.All(
                    item => !item.IsBridge));
        }

        private static MaterializedTopologyReadSet ReadSet(
            TopologyDevice[] devices,
            DeviceInterface[] interfaces,
            PhysicalLink[] links,
            InterfaceDegradationState[] degradation,
            BoundStpObservation[] stp)
        {
            return new MaterializedTopologyReadSet(
                devices,
                interfaces,
                links,
                new PhysicalLinkEvidence[0],
                new Location[0],
                stp,
                degradation);
        }

        private static TopologyDevice Device(
            Guid id,
            string name)
        {
            return new TopologyDevice(
                id,
                null,
                name,
                DeviceCategory.Unknown,
                DeviceDiscoveryOrigin.Automatic,
                MonitoringCapability.Unknown,
                null,
                null,
                null,
                false,
                false,
                Now.AddHours(-1),
                Now,
                Now,
                name,
                null);
        }

        private static DeviceInterface Interface(
            Guid id,
            Guid deviceId,
            int ifIndex,
            string ifName)
        {
            return new DeviceInterface(
                id,
                deviceId,
                ifIndex,
                ifName,
                null,
                null,
                null,
                "00:11:22:33:44:55",
                "up",
                "up",
                1000000000L,
                null,
                null,
                false,
                false,
                Now.AddHours(-1),
                Now,
                ifName,
                null);
        }

        private static PhysicalLink Link(
            Guid id,
            Guid a,
            Guid b)
        {
            return new PhysicalLink(
                id,
                a,
                null,
                b,
                null,
                PhysicalLinkStrength.Confirmed,
                PhysicalLinkFreshness.Fresh,
                null,
                1000000000L,
                "test",
                Now.AddHours(-1),
                Now,
                Now,
                "sprint35",
                false,
                false,
                null);
        }
    }
}
