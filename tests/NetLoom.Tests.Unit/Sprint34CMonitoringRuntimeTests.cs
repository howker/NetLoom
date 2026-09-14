using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring;
using NetLoom.Application.Monitoring.Health;
using NetLoom.Application.Monitoring.Interfaces;
using NetLoom.Application.Observations.Arp;
using NetLoom.Application.Observations.Cdp;
using NetLoom.Application.Observations.Fdb;
using NetLoom.Application.Observations.Lldp;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using NetLoom.Domain.Observations.Arp;
using NetLoom.Domain.Observations.Cdp;
using NetLoom.Domain.Observations.Fdb;
using NetLoom.Domain.Observations.Lldp;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint34CMonitoringRuntimeTests
    {
        private static readonly Guid DeviceId =
            new Guid(
                "55555555-6666-7777-8888-999999999999");

        private static readonly DateTime T1 =
            new DateTime(
                2026, 1, 9, 12, 0, 0,
                DateTimeKind.Utc);

        private static readonly DateTime T2 =
            T1.AddMinutes(1);

        [TestMethod]
        public void ConfiguredRuntimeClassifiesRestartSafeValidInterval()
        {
            var store =
                new MemoryBaselineStore();

            var policy =
                new InterfaceDegradationPolicy(
                    5.0,
                    null);

            var first =
                Runtime(
                    store,
                    Snapshot(
                        T1,
                        10,
                        20,
                        30,
                        40,
                        500),
                    policy)
                    .PollOnce(
                        Request());

            Assert.AreEqual(
                1,
                first.Steps[0]
                    .InterfaceDegradationClassifications
                    .Count);

            Assert.AreEqual(
                InterfaceDegradationStatus.Indeterminate,
                first.Steps[0]
                    .InterfaceDegradationClassifications[0]
                    .Status);

            var second =
                Runtime(
                    store,
                    Snapshot(
                        T2,
                        13,
                        23,
                        30,
                        40,
                        500),
                    policy)
                    .PollOnce(
                        Request());

            Assert.AreEqual(
                1,
                second.Steps[0]
                    .InterfaceDegradationClassifications
                    .Count);

            var classification =
                second.Steps[0]
                    .InterfaceDegradationClassifications[0];

            Assert.AreEqual(
                InterfaceDegradationStatus.Degraded,
                classification.Status);

            Assert.AreEqual(
                6.0,
                classification
                    .ErrorRatePerMinute
                    .Value,
                0.000001);
        }

        [TestMethod]
        public void RuntimeDoesNotClassifyWithoutExplicitPolicy()
        {
            var result =
                Runtime(
                    new MemoryBaselineStore(),
                    Snapshot(
                        T1,
                        10,
                        20,
                        30,
                        40,
                        500),
                    null)
                    .PollOnce(
                        Request());

            Assert.AreEqual(
                1,
                result.Steps[0]
                    .InterfaceCounterEvaluations
                    .Count);

            Assert.AreEqual(
                0,
                result.Steps[0]
                    .InterfaceDegradationClassifications
                    .Count);
        }

        private static MonitoringRuntime Runtime(
            IInterfaceCounterBaselineStore store,
            InterfaceMonitoringSnapshot snapshot,
            InterfaceDegradationPolicy policy)
        {
            return new MonitoringRuntime(
                new NoopLldpCollector(),
                new NoopCdpCollector(),
                new NoopFdbCollector(),
                new NoopArpCollector(),
                new NoopHealthCollector(),
                new FixedInterfaceCollector(
                    snapshot),
                () => snapshot.CapturedUtc,
                interfaceCounterBaselineStore:
                    store,
                interfaceDegradationPolicy:
                    policy);
        }

        private static MonitoringPollRequest Request()
        {
            return new MonitoringPollRequest(
                IPAddress.Parse(
                    "192.0.2.90"),
                161,
                SnmpVersion.V2C,
                new SnmpCommunityCredentials(
                    new byte[]
                    {
                        1,
                        2,
                        3
                    }),
                1000,
                1,
                10,
                new[]
                {
                    MonitoringPollKind.Interface
                },
                DeviceId);
        }

        private static InterfaceMonitoringSnapshot Snapshot(
            DateTime capturedUtc,
            uint? inErrors,
            uint? outErrors,
            uint? inDiscards,
            uint? outDiscards,
            uint? discontinuity)
        {
            return new InterfaceMonitoringSnapshot(
                DeviceId,
                7,
                1,
                1,
                capturedUtc,
                inErrors,
                outErrors,
                inDiscards,
                outDiscards,
                discontinuity);
        }

        private sealed class MemoryBaselineStore :
            IInterfaceCounterBaselineStore
        {
            public InterfaceMonitoringSnapshot Current { get; private set; }

            public InterfaceMonitoringSnapshot ReplaceAndGetPrevious(
                InterfaceMonitoringSnapshot current)
            {
                var previous =
                    Current;

                Current =
                    current;

                return previous;
            }
        }

        private sealed class FixedInterfaceCollector :
            IInterfaceCollector
        {
            private readonly InterfaceMonitoringSnapshot _snapshot;

            public FixedInterfaceCollector(
                InterfaceMonitoringSnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public IReadOnlyList<InterfaceMonitoringSnapshot> Collect(
                InterfaceCollectionRequest request)
            {
                return new[]
                {
                    _snapshot
                };
            }
        }

        private sealed class NoopHealthCollector :
            IHealthCollector
        {
            public HealthSnapshot Collect(
                HealthCollectionRequest request)
            {
                return null;
            }
        }

        private sealed class NoopLldpCollector :
            ILldpCollector
        {
            public LldpObservation Collect(
                LldpCollectionRequest request)
            {
                return null;
            }
        }

        private sealed class NoopCdpCollector :
            ICdpCollector
        {
            public CdpObservation Collect(
                CdpCollectionRequest request)
            {
                return null;
            }
        }

        private sealed class NoopFdbCollector :
            IFdbCollector
        {
            public FdbObservation Collect(
                FdbCollectionRequest request)
            {
                return null;
            }
        }

        private sealed class NoopArpCollector :
            IArpCollector
        {
            public ArpObservation Collect(
                ArpCollectionRequest request)
            {
                return null;
            }
        }
    }
}
