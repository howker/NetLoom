using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring;
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
    public sealed class MonitoringRuntimeTests
    {
        private static readonly DateTime T1 =
            new DateTime(
                2026, 1, 1, 10, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void FailureInOneCollectorDoesNotStopOthers()
        {
            var calls =
                new List<string>();

            var runtime =
                new MonitoringRuntime(
                    new RecordingLldpCollector(
                        calls,
                        true),
                    new RecordingCdpCollector(calls),
                    new RecordingFdbCollector(calls),
                    new RecordingArpCollector(calls),
                    () => T1);

            var result =
                runtime.PollOnce(
                    Request(
                        MonitoringPollKind.Lldp,
                        MonitoringPollKind.Cdp,
                        MonitoringPollKind.Fdb,
                        MonitoringPollKind.Arp));

            CollectionAssert.AreEqual(
                new[]
                {
                    "Lldp",
                    "Cdp",
                    "Fdb",
                    "Arp"
                },
                calls);

            Assert.AreEqual(4, result.Steps.Count);
            Assert.IsFalse(result.Steps[0].Succeeded);
            Assert.IsTrue(result.Steps[1].Succeeded);
            Assert.IsTrue(result.Steps[2].Succeeded);
            Assert.IsTrue(result.Steps[3].Succeeded);
            Assert.IsTrue(result.AnySucceeded);
            Assert.IsFalse(result.AllSucceeded);
        }

        [TestMethod]
        public void SelectedKindsOnlyAreExecuted()
        {
            var calls =
                new List<string>();

            var runtime =
                new MonitoringRuntime(
                    new RecordingLldpCollector(calls),
                    new RecordingCdpCollector(calls),
                    new RecordingFdbCollector(calls),
                    new RecordingArpCollector(calls),
                    () => T1);

            var result =
                runtime.PollOnce(
                    Request(
                        MonitoringPollKind.Lldp,
                        MonitoringPollKind.Arp));

            CollectionAssert.AreEqual(
                new[]
                {
                    "Lldp",
                    "Arp"
                },
                calls);

            Assert.AreEqual(2, result.Steps.Count);
            Assert.IsTrue(result.AllSucceeded);
        }

        [TestMethod]
        public void RuntimeRejectsNonUtcClock()
        {
            var runtime =
                new MonitoringRuntime(
                    new RecordingLldpCollector(
                        new List<string>()),
                    new RecordingCdpCollector(
                        new List<string>()),
                    new RecordingFdbCollector(
                        new List<string>()),
                    new RecordingArpCollector(
                        new List<string>()),
                    () => new DateTime(
                        2026, 1, 1, 10, 0, 0,
                        DateTimeKind.Local));

            try
            {
                runtime.PollOnce(
                    Request(
                        MonitoringPollKind.Lldp));

                Assert.Fail(
                    "Expected invalid UTC clock failure.");
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static MonitoringPollRequest Request(
            params MonitoringPollKind[] kinds)
        {
            return new MonitoringPollRequest(
                IPAddress.Parse("192.0.2.10"),
                161,
                SnmpVersion.V2C,
                new SnmpCommunityCredentials(
                    new byte[] { 1, 2, 3 }),
                1000,
                1,
                10,
                kinds);
        }

        private sealed class RecordingLldpCollector :
            ILldpCollector
        {
            private readonly IList<string> _calls;
            private readonly bool _fail;

            public RecordingLldpCollector(
                IList<string> calls,
                bool fail = false)
            {
                _calls = calls;
                _fail = fail;
            }

            public LldpObservation Collect(
                LldpCollectionRequest request)
            {
                _calls.Add("Lldp");

                if (_fail)
                {
                    throw new InvalidOperationException(
                        "test failure");
                }

                return null;
            }
        }

        private sealed class RecordingCdpCollector :
            ICdpCollector
        {
            private readonly IList<string> _calls;

            public RecordingCdpCollector(
                IList<string> calls)
            {
                _calls = calls;
            }

            public CdpObservation Collect(
                CdpCollectionRequest request)
            {
                _calls.Add("Cdp");
                return null;
            }
        }

        private sealed class RecordingFdbCollector :
            IFdbCollector
        {
            private readonly IList<string> _calls;

            public RecordingFdbCollector(
                IList<string> calls)
            {
                _calls = calls;
            }

            public FdbObservation Collect(
                FdbCollectionRequest request)
            {
                _calls.Add("Fdb");
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
