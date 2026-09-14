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
    public sealed class Sprint34DMonitoringRuntimeTests
    {
        private static readonly Guid DeviceId =
            new Guid(
                "88888888-9999-aaaa-bbbb-cccccccccccc");

        private static readonly DateTime T0 =
            new DateTime(
                2026, 1, 12, 12, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void NewRuntimeSuppressesSamePersistedDegradation()
        {
            var counterStore =
                new MemoryBaselineStore();

            var stateStore =
                new MemoryDegradationStateStore();

            var policy =
                new InterfaceDegradationPolicy(
                    5.0,
                    null);

            var first =
                Runtime(
                    counterStore,
                    stateStore,
                    Snapshot(
                        T0,
                        10,
                        20),
                    policy)
                    .PollOnce(
                        Request());

            Assert.AreEqual(
                InterfaceDegradationTransitionKind.Indeterminate,
                first.Steps[0]
                    .InterfaceDegradationTransitions[0]
                    .Kind);

            var degraded =
                Runtime(
                    counterStore,
                    stateStore,
                    Snapshot(
                        T0.AddMinutes(1),
                        13,
                        23),
                    policy)
                    .PollOnce(
                        Request());

            Assert.AreEqual(
                InterfaceDegradationTransitionKind.FirstAppearance,
                degraded.Steps[0]
                    .InterfaceDegradationTransitions[0]
                    .Kind);

            var restarted =
                Runtime(
                    counterStore,
                    stateStore,
                    Snapshot(
                        T0.AddMinutes(2),
                        16,
                        26),
                    policy)
                    .PollOnce(
                        Request());

            Assert.AreEqual(
                InterfaceDegradationTransitionKind.Unchanged,
                restarted.Steps[0]
                    .InterfaceDegradationTransitions[0]
                    .Kind);

            Assert.IsFalse(
                restarted.Steps[0]
                    .InterfaceDegradationTransitions[0]
                    .HasStateChange);
        }

        [TestMethod]
        public void RuntimeDoesNotTrackTransitionsWithoutTracker()
        {
            var counterStore =
                new MemoryBaselineStore();

            var policy =
                new InterfaceDegradationPolicy(
                    5.0,
                    null);

            Runtime(
                counterStore,
                null,
                Snapshot(
                    T0,
                    10,
                    20),
                policy)
                .PollOnce(
                    Request());

            var result =
                Runtime(
                    counterStore,
                    null,
                    Snapshot(
                        T0.AddMinutes(1),
                        13,
                        23),
                    policy)
                    .PollOnce(
                        Request());

            Assert.AreEqual(
                1,
                result.Steps[0]
                    .InterfaceDegradationClassifications
                    .Count);

            Assert.AreEqual(
                InterfaceDegradationStatus.Degraded,
                result.Steps[0]
                    .InterfaceDegradationClassifications[0]
                    .Status);

            Assert.AreEqual(
                0,
                result.Steps[0]
                    .InterfaceDegradationTransitions
                    .Count);
        }

        private static MonitoringRuntime Runtime(
            IInterfaceCounterBaselineStore counterStore,
            IInterfaceDegradationStateStore stateStore,
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
                    counterStore,
                interfaceDegradationPolicy:
                    policy,
                interfaceDegradationTransitionTracker:
                    stateStore == null
                        ? null
                        : new InterfaceDegradationTransitionTracker(
                            stateStore));
        }

        private static MonitoringPollRequest Request()
        {
            return new MonitoringPollRequest(
                IPAddress.Parse(
                    "192.0.2.100"),
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
            uint inErrors,
            uint outErrors)
        {
            return new InterfaceMonitoringSnapshot(
                DeviceId,
                7,
                1,
                1,
                capturedUtc,
                inErrors,
                outErrors,
                0,
                0,
                500);
        }

        private sealed class MemoryBaselineStore :
            IInterfaceCounterBaselineStore
        {
            public InterfaceMonitoringSnapshot
                Current { get; private set; }

            public InterfaceMonitoringSnapshot
                ReplaceAndGetPrevious(
                    InterfaceMonitoringSnapshot current)
            {
                var previous =
                    Current;

                Current =
                    current;

                return previous;
            }
        }

        private sealed class MemoryDegradationStateStore :
            IInterfaceDegradationStateStore
        {
            public InterfaceDegradationState
                Current { get; private set; }

            public InterfaceDegradationState Load(
                Guid deviceId,
                int ifIndex)
            {
                if (Current == null ||
                    Current.DeviceId != deviceId ||
                    Current.IfIndex != ifIndex)
                {
                    return null;
                }

                return Current;
            }

            public InterfaceDegradationState
                ReplaceAndGetPrevious(
                    InterfaceDegradationState current)
            {
                var previous =
                    Load(
                        current.DeviceId,
                        current.IfIndex);

                Current =
                    current;

                return previous;
            }
        }

        private sealed class FixedInterfaceCollector :
            IInterfaceCollector
        {
            private readonly InterfaceMonitoringSnapshot
                _snapshot;

            public FixedInterfaceCollector(
                InterfaceMonitoringSnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public IReadOnlyList<InterfaceMonitoringSnapshot>
                Collect(
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
