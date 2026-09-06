using System;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring;
using NetLoom.Application.Observations.Arp;
using NetLoom.Application.Observations.Cdp;
using NetLoom.Application.Observations.Fdb;
using NetLoom.Application.Observations.Lldp;
using NetLoom.Application.Observations.Stp;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using NetLoom.Domain.Observations.Arp;
using NetLoom.Domain.Observations.Cdp;
using NetLoom.Domain.Observations.Fdb;
using NetLoom.Domain.Observations.Lldp;
using NetLoom.Domain.Observations.Stp;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class StpMonitoringTests
    {
        [TestMethod]
        public void FailedStpStepDoesNotCancelLaterCollector()
        {
            var lldpCollector =
                new RecordingLldpCollector();

            var runtime =
                new MonitoringRuntime(
                    lldpCollector,
                    new NullCdpCollector(),
                    new NullFdbCollector(),
                    new NullArpCollector(),
                    null,
                    null,
                    stpCollector:
                        new ThrowingStpCollector());

            var result =
                runtime.PollOnce(
                    Request(
                        MonitoringPollKind.Stp,
                        MonitoringPollKind.Lldp));

            var stpStep =
                FindStep(
                    result,
                    MonitoringPollKind.Stp);

            var lldpStep =
                FindStep(
                    result,
                    MonitoringPollKind.Lldp);

            Assert.IsFalse(stpStep.Succeeded);
            Assert.IsTrue(lldpStep.Succeeded);
            Assert.AreEqual(
                1,
                lldpCollector.CollectionCount);
            Assert.IsFalse(result.AllSucceeded);
        }

        private static MonitoringPollRequest Request(
            params MonitoringPollKind[] kinds)
        {
            return new MonitoringPollRequest(
                IPAddress.Loopback,
                161,
                SnmpVersion.V2C,
                new SnmpCommunityCredentials(
                    new byte[] { 1 }),
                1000,
                0,
                10,
                kinds,
                Guid.Parse(
                    "22222222-3333-4444-5555-666666666666"));
        }

        private static MonitoringPollStepResult FindStep(
            MonitoringPollResult result,
            MonitoringPollKind kind)
        {
            foreach (var step in result.Steps)
            {
                if (step.Kind == kind)
                {
                    return step;
                }
            }

            Assert.Fail(
                "Expected monitoring step was not found: " +
                kind);

            return null;
        }

        private sealed class RecordingLldpCollector :
            ILldpCollector
        {
            public int CollectionCount
            {
                get;
                private set;
            }

            public LldpObservation Collect(
                LldpCollectionRequest request)
            {
                CollectionCount++;
                return null;
            }
        }

        private sealed class NullCdpCollector :
            ICdpCollector
        {
            public CdpObservation Collect(
                CdpCollectionRequest request)
            {
                return null;
            }
        }

        private sealed class NullFdbCollector :
            IFdbCollector
        {
            public FdbObservation Collect(
                FdbCollectionRequest request)
            {
                return null;
            }
        }

        private sealed class NullArpCollector :
            IArpCollector
        {
            public ArpObservation Collect(
                ArpCollectionRequest request)
            {
                return null;
            }
        }

        private sealed class ThrowingStpCollector :
            IStpCollector
        {
            public StpObservation Collect(
                StpCollectionRequest request)
            {
                throw new InvalidOperationException(
                    "STP_TEST_FAILURE");
            }
        }
    }
}