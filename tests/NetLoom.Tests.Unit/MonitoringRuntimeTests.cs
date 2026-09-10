using System;
using System.Collections.Generic;
using System.Net;
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
    public sealed class MonitoringRuntimeTests
    {
        private static readonly DateTime T1 =
            new DateTime(
                2026, 1, 1, 10, 0, 0,
                DateTimeKind.Utc);

        private static readonly Guid DeviceId =
            new Guid(
                "11111111-1111-1111-1111-111111111111");

        private static readonly Guid LldpObservationId =
            new Guid(
                "22222222-2222-2222-2222-222222222222");

        private static readonly Guid CdpObservationId =
            new Guid(
                "33333333-3333-3333-3333-333333333333");

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
                        null,
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
                        null,
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
                        null,
                        MonitoringPollKind.Lldp));

                Assert.Fail(
                    "Expected invalid UTC clock failure.");
            }
            catch (InvalidOperationException)
            {
            }
        }

        [TestMethod]
        public void LldpObservationIsBoundToExplicitDevice()
        {
            var calls =
                new List<string>();
            var bindings =
                new RecordingObservationDeviceBindingStore();

            var observation =
                new Observation(
                    LldpObservationId,
                    ObservationKind.Lldp,
                    "192.0.2.10",
                    T1);

            var lldp =
                new LldpObservation(
                    observation,
                    new LldpRemoteNeighbor[0]);

            var runtime =
                CreateRuntime(
                    calls,
                    bindings,
                    lldp,
                    null);

            var result =
                runtime.PollOnce(
                    Request(
                        DeviceId,
                        MonitoringPollKind.Lldp));

            Assert.IsTrue(result.AllSucceeded);
            Assert.AreEqual(1, bindings.Bindings.Count);
            Assert.AreEqual(
                LldpObservationId,
                bindings.Bindings[0].ObservationId);
            Assert.AreEqual(
                DeviceId,
                bindings.Bindings[0].DeviceId);
        }

        [TestMethod]
        public void CdpObservationIsBoundToExplicitDevice()
        {
            var calls =
                new List<string>();
            var bindings =
                new RecordingObservationDeviceBindingStore();

            var observation =
                new Observation(
                    CdpObservationId,
                    ObservationKind.Cdp,
                    "192.0.2.10",
                    T1);

            var cdp =
                new CdpObservation(
                    observation,
                    new CdpRemoteNeighbor[0]);

            var runtime =
                CreateRuntime(
                    calls,
                    bindings,
                    null,
                    cdp);

            var result =
                runtime.PollOnce(
                    Request(
                        DeviceId,
                        MonitoringPollKind.Cdp));

            Assert.IsTrue(result.AllSucceeded);
            Assert.AreEqual(1, bindings.Bindings.Count);
            Assert.AreEqual(
                CdpObservationId,
                bindings.Bindings[0].ObservationId);
            Assert.AreEqual(
                DeviceId,
                bindings.Bindings[0].DeviceId);
        }

        [TestMethod]
        public void ObservationIsNotBoundWithoutExplicitDevice()
        {
            var calls =
                new List<string>();
            var bindings =
                new RecordingObservationDeviceBindingStore();

            var observation =
                new Observation(
                    LldpObservationId,
                    ObservationKind.Lldp,
                    "192.0.2.10",
                    T1);

            var lldp =
                new LldpObservation(
                    observation,
                    new LldpRemoteNeighbor[0]);

            var runtime =
                CreateRuntime(
                    calls,
                    bindings,
                    lldp,
                    null);

            var result =
                runtime.PollOnce(
                    Request(
                        null,
                        MonitoringPollKind.Lldp));

            Assert.IsTrue(result.AllSucceeded);
            Assert.AreEqual(0, bindings.Bindings.Count);
        }

        private static MonitoringRuntime CreateRuntime(
            IList<string> calls,
            IObservationDeviceBindingStore bindingStore,
            LldpObservation lldp,
            CdpObservation cdp)
        {
            return new MonitoringRuntime(
                new RecordingLldpCollector(
                    calls,
                    false,
                    lldp),
                new RecordingCdpCollector(
                    calls,
                    cdp),
                new RecordingFdbCollector(calls),
                new RecordingArpCollector(calls),
                null,
                null,
                () => T1,
                null,
                bindingStore);
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
                    new byte[] { 1, 2, 3 }),
                1000,
                1,
                10,
                kinds,
                deviceId);
        }

        private sealed class RecordingLldpCollector :
            ILldpCollector
        {
            private readonly IList<string> _calls;
            private readonly bool _fail;
            private readonly LldpObservation _observation;

            public RecordingLldpCollector(
                IList<string> calls,
                bool fail = false,
                LldpObservation observation = null)
            {
                _calls = calls;
                _fail = fail;
                _observation = observation;
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

                return _observation;
            }
        }

        private sealed class RecordingCdpCollector :
            ICdpCollector
        {
            private readonly IList<string> _calls;
            private readonly CdpObservation _observation;

            public RecordingCdpCollector(
                IList<string> calls,
                CdpObservation observation = null)
            {
                _calls = calls;
                _observation = observation;
            }

            public CdpObservation Collect(
                CdpCollectionRequest request)
            {
                _calls.Add("Cdp");
                return _observation;
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

        private sealed class RecordingObservationDeviceBindingStore :
            IObservationDeviceBindingStore
        {
            private readonly List<BindingCall> _bindings =
                new List<BindingCall>();

            public IReadOnlyList<BindingCall> Bindings =>
                _bindings;

            public void Bind(
                Guid observationId,
                Guid deviceId)
            {
                _bindings.Add(
                    new BindingCall(
                        observationId,
                        deviceId));
            }

            public Guid? GetDeviceId(
                Guid observationId)
            {
                for (var index = 0;
                    index < _bindings.Count;
                    index++)
                {
                    if (_bindings[index].ObservationId ==
                        observationId)
                    {
                        return _bindings[index].DeviceId;
                    }
                }

                return null;
            }
        }

        private sealed class BindingCall
        {
            public BindingCall(
                Guid observationId,
                Guid deviceId)
            {
                ObservationId = observationId;
                DeviceId = deviceId;
            }

            public Guid ObservationId { get; }

            public Guid DeviceId { get; }
        }
    }
}
