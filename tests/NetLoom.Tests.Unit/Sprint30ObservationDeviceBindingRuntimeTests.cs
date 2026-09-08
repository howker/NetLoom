using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring;
using NetLoom.Application.Observations;
using NetLoom.Application.Observations.Arp;
using NetLoom.Application.Observations.Cdp;
using NetLoom.Application.Observations.Fdb;
using NetLoom.Application.Observations.Lldp;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Arp;
using NetLoom.Domain.Observations.Cdp;
using NetLoom.Domain.Observations.Fdb;
using NetLoom.Domain.Observations.Lldp;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class
        Sprint30ObservationDeviceBindingRuntimeTests
    {
        private static readonly DateTime CapturedUtc =
            new DateTime(
                2026, 9, 8, 10, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void
            RuntimeBindsSuccessfulArpAndFdbObservationsToStableDeviceId()
        {
            var deviceId =
                Guid.NewGuid();

            var fdb =
                Observation(
                    ObservationKind.Fdb);

            var arp =
                Observation(
                    ObservationKind.Arp);

            var bindings =
                new RecordingBindingStore();

            var runtime =
                Runtime(
                    new FdbCollector(fdb),
                    new ArpCollector(arp),
                    bindings);

            var result =
                runtime.PollOnce(
                    Request(
                        deviceId,
                        MonitoringPollKind.Fdb,
                        MonitoringPollKind.Arp));

            Assert.IsTrue(result.AllSucceeded);
            Assert.AreEqual(2, bindings.Values.Count);

            Assert.AreEqual(
                deviceId,
                bindings.Values[fdb.Id]);

            Assert.AreEqual(
                deviceId,
                bindings.Values[arp.Id]);
        }

        [TestMethod]
        public void
            RuntimeLeavesObservationUnboundWhenRequestHasNoDeviceId()
        {
            var bindings =
                new RecordingBindingStore();

            var runtime =
                Runtime(
                    new FdbCollector(
                        Observation(
                            ObservationKind.Fdb)),
                    new ArpCollector(
                        Observation(
                            ObservationKind.Arp)),
                    bindings);

            var result =
                runtime.PollOnce(
                    Request(
                        null,
                        MonitoringPollKind.Fdb,
                        MonitoringPollKind.Arp));

            Assert.IsTrue(result.AllSucceeded);
            Assert.AreEqual(0, bindings.Values.Count);
        }

        private static MonitoringRuntime Runtime(
            IFdbCollector fdb,
            IArpCollector arp,
            IObservationDeviceBindingStore bindings)
        {
            return new MonitoringRuntime(
                new LldpCollector(),
                new CdpCollector(),
                fdb,
                arp,
                healthCollector: null,
                interfaceCollector: null,
                utcNow: () => CapturedUtc,
                stpCollector: null,
                observationDeviceBindingStore:
                    bindings);
        }

        private static MonitoringPollRequest Request(
            Guid? deviceId,
            params MonitoringPollKind[] kinds)
        {
            return new MonitoringPollRequest(
                IPAddress.Parse("192.0.2.10"),
                161,
                SnmpVersion.V2C,
                new SnmpCommunityCredentials(
                    Encoding.ASCII.GetBytes(
                        "test")),
                1000,
                0,
                10,
                kinds,
                deviceId);
        }

        private static Observation Observation(
            ObservationKind kind)
        {
            return new Observation(
                Guid.NewGuid(),
                kind,
                "192.0.2.10",
                CapturedUtc);
        }

        private sealed class LldpCollector :
            ILldpCollector
        {
            public LldpObservation Collect(
                LldpCollectionRequest request)
            {
                return null;
            }
        }

        private sealed class CdpCollector :
            ICdpCollector
        {
            public CdpObservation Collect(
                CdpCollectionRequest request)
            {
                return null;
            }
        }

        private sealed class FdbCollector :
            IFdbCollector
        {
            private readonly Observation
                _observation;

            public FdbCollector(
                Observation observation)
            {
                _observation = observation;
            }

            public FdbObservation Collect(
                FdbCollectionRequest request)
            {
                return new FdbObservation(
                    _observation,
                    new BridgePortMapping[0],
                    new FdbEntry[0]);
            }
        }

        private sealed class ArpCollector :
            IArpCollector
        {
            private readonly Observation
                _observation;

            public ArpCollector(
                Observation observation)
            {
                _observation = observation;
            }

            public ArpObservation Collect(
                ArpCollectionRequest request)
            {
                return new ArpObservation(
                    _observation,
                    new ArpEntry[0]);
            }
        }

        private sealed class RecordingBindingStore :
            IObservationDeviceBindingStore
        {
            public Dictionary<Guid, Guid> Values { get; } =
                new Dictionary<Guid, Guid>();

            public void Bind(
                Guid observationId,
                Guid deviceId)
            {
                Values.Add(
                    observationId,
                    deviceId);
            }

            public Guid? GetDeviceId(
                Guid observationId)
            {
                Guid value;

                return Values.TryGetValue(
                    observationId,
                    out value)
                        ? (Guid?)value
                        : null;
            }
        }
    }
}