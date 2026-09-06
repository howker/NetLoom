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
using NetLoom.Protocols.Snmp.Interfaces;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class InterfaceMonitoringTests
    {
        private static readonly DateTime T1 =
            new DateTime(
                2026, 1, 4, 12, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void StatusParserAcceptsIntegerAndNamedInteger()
        {
            Assert.AreEqual(
                1,
                SnmpInterfaceStatusParser.ParseStatus(
                    "1"));

            Assert.AreEqual(
                2,
                SnmpInterfaceStatusParser.ParseStatus(
                    "down(2)"));
        }

        [TestMethod]
        public void IfIndexComesFromColumnOidSuffix()
        {
            Assert.AreEqual(
                17,
                SnmpInterfaceStatusParser.ParseIfIndex(
                    "1.3.6.1.2.1.2.2.1.8.17",
                    "1.3.6.1.2.1.2.2.1.8"));
        }

        [TestMethod]
        public void InterfaceSnapshotKeepsIpSeparateFromDeviceId()
        {
            var runtime =
                Runtime(
                    new RecordingInterfaceCollector(
                        false));

            var result =
                runtime.PollOnce(
                    Request(
                        null,
                        MonitoringPollKind.Interface));

            Assert.IsTrue(
                result.Steps[0].Succeeded);

            Assert.AreEqual(
                1,
                result.Steps[0].
                    InterfaceSnapshots.Count);

            Assert.IsFalse(
                result.Steps[0].
                    InterfaceSnapshots[0].
                    DeviceId.HasValue);

            Assert.AreEqual(
                7,
                result.Steps[0].
                    InterfaceSnapshots[0].
                    IfIndex);
        }

        [TestMethod]
        public void StableDeviceIdFlowsIntoInterfaceSnapshot()
        {
            var deviceId =
                Guid.NewGuid();

            var runtime =
                Runtime(
                    new RecordingInterfaceCollector(
                        false));

            var result =
                runtime.PollOnce(
                    Request(
                        deviceId,
                        MonitoringPollKind.Interface));

            Assert.AreEqual(
                deviceId,
                result.Steps[0].
                    InterfaceSnapshots[0].
                    DeviceId);
        }

        [TestMethod]
        public void InterfaceFailureDoesNotDeleteOrStopLaterStep()
        {
            var calls =
                new List<string>();

            var runtime =
                new MonitoringRuntime(
                    new NoopLldpCollector(),
                    new NoopCdpCollector(),
                    new NoopFdbCollector(),
                    new RecordingArpCollector(
                        calls),
                    new NoopHealthCollector(),
                    new RecordingInterfaceCollector(
                        true),
                    () => T1);

            var result =
                runtime.PollOnce(
                    Request(
                        null,
                        MonitoringPollKind.Interface,
                        MonitoringPollKind.Arp));

            Assert.IsFalse(
                result.Steps[0].Succeeded);

            Assert.AreEqual(
                0,
                result.Steps[0].
                    InterfaceSnapshots.Count);

            Assert.IsTrue(
                result.Steps[1].Succeeded);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Arp"
                },
                calls);
        }

        [TestMethod]
        public void InterfaceSnapshotRejectsInvalidIfIndex()
        {
            try
            {
                new InterfaceMonitoringSnapshot(
                    null,
                    0,
                    1,
                    1,
                    T1);

                Assert.Fail(
                    "Expected invalid ifIndex rejection.");
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        private static MonitoringRuntime Runtime(
            IInterfaceCollector interfaceCollector)
        {
            return new MonitoringRuntime(
                new NoopLldpCollector(),
                new NoopCdpCollector(),
                new NoopFdbCollector(),
                new RecordingArpCollector(
                    new List<string>()),
                new NoopHealthCollector(),
                interfaceCollector,
                () => T1);
        }

        private static MonitoringPollRequest Request(
            Guid? deviceId,
            params MonitoringPollKind[] kinds)
        {
            return new MonitoringPollRequest(
                IPAddress.Parse(
                    "192.0.2.50"),
                161,
                SnmpVersion.V2C,
                new SnmpCommunityCredentials(
                    new byte[] { 1, 2, 3 }),
                1000,
                1,
                10,
                kinds,
                deviceId);
        }

        private sealed class RecordingInterfaceCollector :
            IInterfaceCollector
        {
            private readonly bool _fail;

            public RecordingInterfaceCollector(
                bool fail)
            {
                _fail = fail;
            }

            public IReadOnlyList<InterfaceMonitoringSnapshot> Collect(
                InterfaceCollectionRequest request)
            {
                if (_fail)
                {
                    throw new InvalidOperationException(
                        "interface test failure");
                }

                return new[]
                {
                    new InterfaceMonitoringSnapshot(
                        request.DeviceId,
                        7,
                        1,
                        1,
                        T1)
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

        private sealed class RecordingArpCollector :
            IArpCollector
        {
            private readonly IList<string> _calls;

            public RecordingArpCollector(
                IList<string> calls)
            {
                _calls = calls;
            }

            public ArpObservation Collect(
                ArpCollectionRequest request)
            {
                _calls.Add("Arp");
                return null;
            }
        }
    }
}
