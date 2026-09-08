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
using NetLoom.Application.Observations.Stp;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Arp;
using NetLoom.Domain.Observations.Cdp;
using NetLoom.Domain.Observations.Fdb;
using NetLoom.Domain.Observations.Lldp;
using NetLoom.Domain.Observations.Stp;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class
        Sprint31StpObservationBindingRuntimeTests
    {
        private static readonly DateTime CapturedUtc =
            new DateTime(
                2026,
                9,
                8,
                15,
                0,
                0,
                DateTimeKind.Utc);

        [TestMethod]
        public void
            SuccessfulStpPollBindsObservationToStableDeviceId()
        {
            var deviceId =
                Guid.NewGuid();

            var observation =
                Observation();

            var bindings =
                new RecordingBindingStore();

            var runtime =
                Runtime(
                    new StpCollector(
                        observation),
                    bindings);

            var result =
                runtime.PollOnce(
                    Request(
                        deviceId));

            Assert.IsTrue(
                result.AllSucceeded);

            Assert.AreEqual(
                1,
                bindings.Values.Count);

            Assert.AreEqual(
                deviceId,
                bindings.Values[
                    observation.Id]);
        }

        [TestMethod]
        public void
            SuccessfulStpPollWithoutDeviceIdDoesNotInferIdentityFromAddress()
        {
            var observation =
                Observation();

            var bindings =
                new RecordingBindingStore();

            var runtime =
                Runtime(
                    new StpCollector(
                        observation),
                    bindings);

            var result =
                runtime.PollOnce(
                    Request(
                        null));

            Assert.IsTrue(
                result.AllSucceeded);

            Assert.AreEqual(
                0,
                bindings.Values.Count);
        }

        private static MonitoringRuntime Runtime(
            IStpCollector stpCollector,
            IObservationDeviceBindingStore bindings)
        {
            return new MonitoringRuntime(
                new LldpCollector(),
                new CdpCollector(),
                new FdbCollector(),
                new ArpCollector(),
                healthCollector: null,
                interfaceCollector: null,
                utcNow: () => CapturedUtc,
                stpCollector: stpCollector,
                observationDeviceBindingStore:
                    bindings);
        }

        private static MonitoringPollRequest Request(
            Guid? deviceId)
        {
            return new MonitoringPollRequest(
                IPAddress.Parse(
                    "192.0.2.10"),
                161,
                SnmpVersion.V2C,
                new SnmpCommunityCredentials(
                    Encoding.ASCII.GetBytes(
                        "test")),
                1000,
                0,
                10,
                new[]
                {
                    MonitoringPollKind.Stp
                },
                deviceId);
        }

        private static Observation Observation()
        {
            return new Observation(
                Guid.NewGuid(),
                ObservationKind.Stp,
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
            public FdbObservation Collect(
                FdbCollectionRequest request)
            {
                return null;
            }
        }

        private sealed class ArpCollector :
            IArpCollector
        {
            public ArpObservation Collect(
                ArpCollectionRequest request)
            {
                return null;
            }
        }

        private sealed class StpCollector :
            IStpCollector
        {
            private readonly Observation
                _observation;

            public StpCollector(
                Observation observation)
            {
                _observation =
                    observation;
            }

            public StpObservation Collect(
                StpCollectionRequest request)
            {
                return new StpObservation(
                    _observation,
                    "cist",
                    null,
                    null,
                    null,
                    null,
                    null,
                    new StpPortState[0]);
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
