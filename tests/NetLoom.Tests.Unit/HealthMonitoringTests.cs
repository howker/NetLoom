using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring;
using NetLoom.Application.Monitoring.Health;
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
using NetLoom.Protocols.Snmp.Health;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class HealthMonitoringTests
    {
        private static readonly DateTime T1 =
            new DateTime(
                2026, 1, 3, 12, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void NumericSysUpTimeParsesAsHundredthsOfSecond()
        {
            var uptime =
                SnmpHealthUptimeParser.Parse(
                    "12345");

            Assert.IsTrue(
                uptime.HasValue);

            Assert.AreEqual(
                123.45,
                uptime.Value.TotalSeconds,
                0.001);
        }

        [TestMethod]
        public void ParenthesizedSysUpTimeParsesAsHundredthsOfSecond()
        {
            var uptime =
                SnmpHealthUptimeParser.Parse(
                    "Timeticks: (12345) 0:02:03.45");

            Assert.IsTrue(
                uptime.HasValue);

            Assert.AreEqual(
                123.45,
                uptime.Value.TotalSeconds,
                0.001);
        }

        [TestMethod]
        public void RuntimeReturnsHealthSnapshotWithoutUsingIpAsDeviceId()
        {
            var runtime =
                Runtime(
                    new RecordingHealthCollector(
                        false));

            var result =
                runtime.PollOnce(
                    Request(
                        null,
                        MonitoringPollKind.Health));

            Assert.AreEqual(
                1,
                result.Steps.Count);

            Assert.IsTrue(
                result.Steps[0].Succeeded);

            Assert.IsNotNull(
                result.Steps[0].HealthSnapshot);

            Assert.IsFalse(
                result.Steps[0].
                    HealthSnapshot.
                    DeviceId.HasValue);
        }

        [TestMethod]
        public void StableDeviceIdFlowsIntoHealthSnapshot()
        {
            var deviceId =
                Guid.NewGuid();

            var runtime =
                Runtime(
                    new RecordingHealthCollector(
                        false));

            var result =
                runtime.PollOnce(
                    Request(
                        deviceId,
                        MonitoringPollKind.Health));

            Assert.AreEqual(
                deviceId,
                result.Steps[0].
                    HealthSnapshot.
                    DeviceId);
        }

        [TestMethod]
        public void HealthFailureDoesNotStopLaterProtocolStep()
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
                    new RecordingHealthCollector(
                        true),
                    () => T1);

            var result =
                runtime.PollOnce(
                    Request(
                        null,
                        MonitoringPollKind.Health,
                        MonitoringPollKind.Arp));

            Assert.AreEqual(
                2,
                result.Steps.Count);

            Assert.IsFalse(
                result.Steps[0].Succeeded);

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
        public void RequestRejectsEmptyDeviceId()
        {
            try
            {
                Request(
                    Guid.Empty,
                    MonitoringPollKind.Health);

                Assert.Fail(
                    "Expected empty DeviceId rejection.");
            }
            catch (ArgumentException)
            {
            }
        }

        private static MonitoringRuntime Runtime(
            IHealthCollector healthCollector)
        {
            return new MonitoringRuntime(
                new NoopLldpCollector(),
                new NoopCdpCollector(),
                new NoopFdbCollector(),
                new RecordingArpCollector(
                    new List<string>()),
                healthCollector,
                () => T1);
        }

        private static MonitoringPollRequest Request(
            Guid? deviceId,
            params MonitoringPollKind[] kinds)
        {
            return new MonitoringPollRequest(
                IPAddress.Parse(
                    "192.0.2.40"),
                161,
                SnmpVersion.V2C,
                new SnmpCommunityCredentials(
                    new byte[] { 7, 8, 9 }),
                1000,
                1,
                10,
                kinds,
                deviceId);
        }

        private sealed class RecordingHealthCollector :
            IHealthCollector
        {
            private readonly bool _fail;

            public RecordingHealthCollector(
                bool fail)
            {
                _fail = fail;
            }

            public HealthSnapshot Collect(
                HealthCollectionRequest request)
            {
                if (_fail)
                {
                    throw new InvalidOperationException(
                        "health test failure");
                }

                return new HealthSnapshot(
                    request.DeviceId,
                    request.Address,
                    T1,
                    HealthStatus.Up,
                    TimeSpan.FromSeconds(42));
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
