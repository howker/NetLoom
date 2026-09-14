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
    public sealed class Sprint34BMonitoringRuntimeTests
    {
        private static readonly Guid DeviceId =
            new Guid(
                "33333333-4444-5555-6666-777777777777");

        private static readonly DateTime T1 =
            new DateTime(
                2026, 1, 7, 12, 0, 0,
                DateTimeKind.Utc);

        private static readonly DateTime T2 =
            T1.AddSeconds(30);

        [TestMethod]
        public void FirstStableSampleIsPersistedAndReportedAsNoBaseline()
        {
            var store =
                new MemoryBaselineStore();

            var result =
                Runtime(
                    store,
                    Snapshot(
                        T1,
                        10,
                        20,
                        30,
                        40,
                        500))
                    .PollOnce(
                        Request());

            var step =
                result.Steps[0];

            Assert.IsTrue(
                step.Succeeded);

            Assert.AreEqual(
                1,
                step.InterfaceCounterEvaluations.Count);

            Assert.AreEqual(
                InterfaceCounterDeltaStatus.NoBaseline,
                step.InterfaceCounterEvaluations[0]
                    .Delta.Status);

            Assert.AreEqual(
                T1,
                store.Current.CapturedUtc);
        }

        [TestMethod]
        public void NewRuntimeUsesPersistedBaselineForValidInterval()
        {
            var store =
                new MemoryBaselineStore();

            Runtime(
                store,
                Snapshot(
                    T1,
                    10,
                    20,
                    30,
                    40,
                    500))
                .PollOnce(
                    Request());

            var result =
                Runtime(
                    store,
                    Snapshot(
                        T2,
                        13,
                        27,
                        35,
                        49,
                        500))
                .PollOnce(
                    Request());

            var evaluation =
                result.Steps[0]
                    .InterfaceCounterEvaluations[0];

            Assert.AreEqual(
                DeviceId,
                evaluation.DeviceId);

            Assert.AreEqual(
                7,
                evaluation.IfIndex);

            Assert.AreEqual(
                InterfaceCounterDeltaStatus.Valid,
                evaluation.Delta.Status);

            Assert.AreEqual(
                (ulong)3,
                evaluation.Delta.InErrors);

            Assert.AreEqual(
                (ulong)9,
                evaluation.Delta.OutDiscards);
        }

        private static MonitoringRuntime Runtime(
            IInterfaceCounterBaselineStore store,
            InterfaceMonitoringSnapshot snapshot)
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
                    store);
        }

        private static MonitoringPollRequest Request()
        {
            return new MonitoringPollRequest(
                IPAddress.Parse(
                    "192.0.2.80"),
                161,
                SnmpVersion.V2C,
                new SnmpCommunityCredentials(
                    new byte[] { 1, 2, 3 }),
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
