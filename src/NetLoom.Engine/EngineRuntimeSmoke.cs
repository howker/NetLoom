using System;
using System.Net;
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

namespace NetLoom.Engine
{
    internal static class EngineRuntimeSmoke
    {
        public static bool Run()
        {
            var runtime =
                new MonitoringRuntime(
                    new SmokeLldpCollector(),
                    new SmokeCdpCollector(),
                    new SmokeFdbCollector(),
                    new SmokeArpCollector());

            var result =
                runtime.PollOnce(
                    new MonitoringPollRequest(
                        IPAddress.Loopback,
                        161,
                        SnmpVersion.V2C,
                        new SnmpCommunityCredentials(
                            new byte[] { 1 }),
                        1,
                        0,
                        1,
                        new[]
                        {
                            MonitoringPollKind.Lldp,
                            MonitoringPollKind.Cdp,
                            MonitoringPollKind.Fdb,
                            MonitoringPollKind.Arp
                        }));

            return
                result.AllSucceeded &&
                result.Steps.Count == 4;
        }

        private sealed class SmokeLldpCollector :
            ILldpCollector
        {
            public LldpObservation Collect(
                LldpCollectionRequest request)
            {
                return null;
            }
        }

        private sealed class SmokeCdpCollector :
            ICdpCollector
        {
            public CdpObservation Collect(
                CdpCollectionRequest request)
            {
                return null;
            }
        }

        private sealed class SmokeFdbCollector :
            IFdbCollector
        {
            public FdbObservation Collect(
                FdbCollectionRequest request)
            {
                return null;
            }
        }

        private sealed class SmokeArpCollector :
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
