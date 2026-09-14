using System;
using System.Collections.Generic;
using NetLoom.Application.Monitoring.Health;
using NetLoom.Application.Monitoring.Interfaces;
using NetLoom.Application.Observations;
using NetLoom.Application.Observations.Arp;
using NetLoom.Application.Observations.Cdp;
using NetLoom.Application.Observations.Fdb;
using NetLoom.Application.Observations.Lldp;
using NetLoom.Application.Observations.Stp;
using NetLoom.Domain.Observations.Lldp;

namespace NetLoom.Application.Monitoring
{
    public sealed class MonitoringRuntime
    {
        private readonly ILldpCollector _lldpCollector;
        private readonly ICdpCollector _cdpCollector;
        private readonly IFdbCollector _fdbCollector;
        private readonly IArpCollector _arpCollector;
        private readonly IHealthCollector _healthCollector;
        private readonly IInterfaceCollector _interfaceCollector;
        private readonly IStpCollector _stpCollector;
        private readonly IObservationDeviceBindingStore
            _observationDeviceBindingStore;
        private readonly IMonitoringTopologyMaterializer
            _topologyMaterializer;
        private readonly IInterfaceCounterBaselineStore
            _interfaceCounterBaselineStore;
        private readonly InterfaceCounterDeltaEvaluator
            _interfaceCounterDeltaEvaluator;
        private readonly InterfaceDegradationClassifier
            _interfaceDegradationClassifier;
        private readonly InterfaceDegradationPolicy
            _interfaceDegradationPolicy;
        private readonly Func<DateTime> _utcNow;

        public MonitoringRuntime(
            ILldpCollector lldpCollector,
            ICdpCollector cdpCollector,
            IFdbCollector fdbCollector,
            IArpCollector arpCollector,
            Func<DateTime> utcNow = null)
            : this(
                lldpCollector,
                cdpCollector,
                fdbCollector,
                arpCollector,
                null,
                null,
                utcNow)
        {
        }

        public MonitoringRuntime(
            ILldpCollector lldpCollector,
            ICdpCollector cdpCollector,
            IFdbCollector fdbCollector,
            IArpCollector arpCollector,
            IHealthCollector healthCollector,
            Func<DateTime> utcNow = null)
            : this(
                lldpCollector,
                cdpCollector,
                fdbCollector,
                arpCollector,
                healthCollector,
                null,
                utcNow)
        {
        }

        public MonitoringRuntime(
            ILldpCollector lldpCollector,
            ICdpCollector cdpCollector,
            IFdbCollector fdbCollector,
            IArpCollector arpCollector,
            IHealthCollector healthCollector,
            IInterfaceCollector interfaceCollector,
            Func<DateTime> utcNow = null,
            IStpCollector stpCollector = null,
            IObservationDeviceBindingStore
                observationDeviceBindingStore = null,
            IMonitoringTopologyMaterializer
                topologyMaterializer = null,
            IInterfaceCounterBaselineStore
                interfaceCounterBaselineStore = null,
            InterfaceCounterDeltaEvaluator
                interfaceCounterDeltaEvaluator = null,
            InterfaceDegradationClassifier
                interfaceDegradationClassifier = null,
            InterfaceDegradationPolicy
                interfaceDegradationPolicy = null)
        {
            _lldpCollector =
                lldpCollector ??
                throw new ArgumentNullException(nameof(lldpCollector));

            _cdpCollector =
                cdpCollector ??
                throw new ArgumentNullException(nameof(cdpCollector));

            _fdbCollector =
                fdbCollector ??
                throw new ArgumentNullException(nameof(fdbCollector));

            _arpCollector =
                arpCollector ??
                throw new ArgumentNullException(nameof(arpCollector));

            _healthCollector =
                healthCollector;

            _interfaceCollector =
                interfaceCollector;

            _stpCollector =
                stpCollector;

            _observationDeviceBindingStore =
                observationDeviceBindingStore;

            _topologyMaterializer =
                topologyMaterializer;

            _interfaceCounterBaselineStore =
                interfaceCounterBaselineStore;

            _interfaceCounterDeltaEvaluator =
                interfaceCounterDeltaEvaluator ??
                new InterfaceCounterDeltaEvaluator();

            _interfaceDegradationPolicy =
                interfaceDegradationPolicy;

            _interfaceDegradationClassifier =
                interfaceDegradationPolicy == null
                    ? null
                    : interfaceDegradationClassifier ??
                        new InterfaceDegradationClassifier();

            _utcNow =
                utcNow ??
                (() => DateTime.UtcNow);
        }

        public MonitoringPollResult PollOnce(
            MonitoringPollRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var startedUtc = NowUtc();
            var steps =
                new List<MonitoringPollStepResult>();

            foreach (var kind in request.Kinds)
            {
                steps.Add(
                    Execute(
                        kind,
                        request));
            }

            var completedUtc = NowUtc();

            return new MonitoringPollResult(
                request.Address,
                startedUtc,
                completedUtc,
                steps);
        }

        private MonitoringPollStepResult Execute(
            MonitoringPollKind kind,
            MonitoringPollRequest request)
        {
            try
            {
                switch (kind)
                {
                    case MonitoringPollKind.Lldp:
                        var lldp =
                            _lldpCollector.Collect(
                                new LldpCollectionRequest(
                                    request.Address,
                                    request.Port,
                                    request.Version,
                                    request.Credentials,
                                    request.TimeoutMilliseconds,
                                    request.RetryCount,
                                    request.MaxRepetitions));

                        BindObservation(
                            lldp == null
                                ? (Guid?)null
                                : lldp.Observation.Id,
                            request.DeviceId);

                        if (lldp != null)
                        {
                            MaterializeLldp(
                                request.DeviceId,
                                lldp);
                        }

                        break;

                    case MonitoringPollKind.Cdp:
                        var cdp =
                            _cdpCollector.Collect(
                                new CdpCollectionRequest(
                                    request.Address,
                                    request.Port,
                                    request.Version,
                                    request.Credentials,
                                    request.TimeoutMilliseconds,
                                    request.RetryCount,
                                    request.MaxRepetitions));

                        BindObservation(
                            cdp == null
                                ? (Guid?)null
                                : cdp.Observation.Id,
                            request.DeviceId);

                        if (cdp != null)
                        {
                            MaterializeDevice(
                                request.DeviceId,
                                cdp.Observation.CapturedUtc);
                        }

                        break;

                    case MonitoringPollKind.Fdb:
                        var fdb =
                            _fdbCollector.Collect(
                                new FdbCollectionRequest(
                                    request.Address,
                                    request.Port,
                                    request.Version,
                                    request.Credentials,
                                    request.TimeoutMilliseconds,
                                    request.RetryCount,
                                    request.MaxRepetitions));

                        BindObservation(
                            fdb == null
                                ? (Guid?)null
                                : fdb.Observation.Id,
                            request.DeviceId);

                        if (fdb != null)
                        {
                            MaterializeDevice(
                                request.DeviceId,
                                fdb.Observation.CapturedUtc);
                        }

                        break;

                    case MonitoringPollKind.Arp:
                        var arp =
                            _arpCollector.Collect(
                                new ArpCollectionRequest(
                                    request.Address,
                                    request.Port,
                                    request.Version,
                                    request.Credentials,
                                    request.TimeoutMilliseconds,
                                    request.RetryCount,
                                    request.MaxRepetitions));

                        BindObservation(
                            arp == null
                                ? (Guid?)null
                                : arp.Observation.Id,
                            request.DeviceId);

                        if (arp != null)
                        {
                            MaterializeDevice(
                                request.DeviceId,
                                arp.Observation.CapturedUtc);
                        }

                        break;

                    case MonitoringPollKind.Stp:
                        if (_stpCollector == null)
                        {
                            throw new InvalidOperationException(
                                "STP collector is not configured.");
                        }

                        var stp =
                            _stpCollector.Collect(
                                new StpCollectionRequest(
                                    request.Address,
                                    request.Port,
                                    request.Version,
                                    request.Credentials,
                                    request.TimeoutMilliseconds,
                                    request.RetryCount,
                                    request.MaxRepetitions));

                        BindObservation(
                            stp == null
                                ? (Guid?)null
                                : stp.Observation.Id,
                            request.DeviceId);

                        if (stp != null)
                        {
                            MaterializeDevice(
                                request.DeviceId,
                                stp.Observation.CapturedUtc);
                        }

                        break;

                    case MonitoringPollKind.Health:
                        if (_healthCollector == null)
                        {
                            throw new InvalidOperationException(
                                "Health collector is not configured.");
                        }

                        var health =
                            _healthCollector.Collect(
                                new HealthCollectionRequest(
                                    request.DeviceId,
                                    request.Address,
                                    request.Port,
                                    request.Version,
                                    request.Credentials,
                                    request.TimeoutMilliseconds,
                                    request.RetryCount));

                        MaterializeDevice(
                            request.DeviceId,
                            NowUtc());

                        return new MonitoringPollStepResult(
                            kind,
                            true,
                            null,
                            null,
                            health);

                    case MonitoringPollKind.Interface:
                        if (_interfaceCollector == null)
                        {
                            throw new InvalidOperationException(
                                "Interface collector is not configured.");
                        }

                        var interfaces =
                            _interfaceCollector.Collect(
                                new InterfaceCollectionRequest(
                                    request.DeviceId,
                                    request.Address,
                                    request.Port,
                                    request.Version,
                                    request.Credentials,
                                    request.TimeoutMilliseconds,
                                    request.RetryCount,
                                    request.MaxRepetitions));

                        var interfaceObservedUtc =
                            NowUtc();

                        MaterializeDevice(
                            request.DeviceId,
                            interfaceObservedUtc);

                        if (_topologyMaterializer != null &&
                            request.DeviceId.HasValue)
                        {
                            foreach (var snapshot in interfaces)
                            {
                                _topologyMaterializer
                                    .MaterializeInterface(
                                        request.DeviceId.Value,
                                        snapshot.IfIndex,
                                        interfaceObservedUtc);
                            }
                        }

                        var counterEvaluations =
                            EvaluateInterfaceCounters(
                                interfaces);

                        var degradationClassifications =
                            ClassifyInterfaceDegradation(
                                counterEvaluations);

                        return new MonitoringPollStepResult(
                            kind,
                            true,
                            null,
                            null,
                            null,
                            interfaces,
                            counterEvaluations,
                            degradationClassifications);

                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(kind));
                }

                return new MonitoringPollStepResult(
                    kind,
                    true,
                    null,
                    null);
            }
            catch (Exception exception)
            {
                return new MonitoringPollStepResult(
                    kind,
                    false,
                    exception.GetType().Name,
                    exception.Message);
            }
        }

        private IReadOnlyList<InterfaceCounterEvaluation>
            EvaluateInterfaceCounters(
                IReadOnlyList<InterfaceMonitoringSnapshot> snapshots)
        {
            if (_interfaceCounterBaselineStore == null ||
                snapshots == null ||
                snapshots.Count == 0)
            {
                return Array.Empty<InterfaceCounterEvaluation>();
            }

            var evaluations =
                new List<InterfaceCounterEvaluation>();

            foreach (var snapshot in snapshots)
            {
                if (snapshot == null ||
                    !snapshot.DeviceId.HasValue)
                {
                    continue;
                }

                var previous =
                    _interfaceCounterBaselineStore
                        .ReplaceAndGetPrevious(
                            snapshot);

                var delta =
                    _interfaceCounterDeltaEvaluator
                        .Evaluate(
                            previous,
                            snapshot);

                evaluations.Add(
                    new InterfaceCounterEvaluation(
                        snapshot,
                        delta));
            }

            return evaluations;
        }

        private IReadOnlyList<InterfaceDegradationClassification>
            ClassifyInterfaceDegradation(
                IReadOnlyList<InterfaceCounterEvaluation> evaluations)
        {
            if (_interfaceDegradationPolicy == null ||
                _interfaceDegradationClassifier == null ||
                evaluations == null ||
                evaluations.Count == 0)
            {
                return Array.Empty<InterfaceDegradationClassification>();
            }

            var classifications =
                new List<InterfaceDegradationClassification>();

            foreach (var evaluation in evaluations)
            {
                if (evaluation == null)
                {
                    continue;
                }

                classifications.Add(
                    _interfaceDegradationClassifier
                        .Classify(
                            evaluation,
                            _interfaceDegradationPolicy));
            }

            return classifications;
        }

        private void BindObservation(
            Guid? observationId,
            Guid? deviceId)
        {
            if (_observationDeviceBindingStore == null ||
                !observationId.HasValue ||
                !deviceId.HasValue)
            {
                return;
            }

            _observationDeviceBindingStore.Bind(
                observationId.Value,
                deviceId.Value);
        }

        private void MaterializeLldp(
            Guid? deviceId,
            LldpObservation observation)
        {
            if (_topologyMaterializer == null ||
                !deviceId.HasValue ||
                observation == null)
            {
                return;
            }

            _topologyMaterializer.MaterializeLldp(
                deviceId.Value,
                observation);
        }

        private void MaterializeDevice(
            Guid? deviceId,
            DateTime observedUtc)
        {
            if (_topologyMaterializer == null ||
                !deviceId.HasValue)
            {
                return;
            }

            _topologyMaterializer.MaterializeDevice(
                deviceId.Value,
                observedUtc);
        }

        private DateTime NowUtc()
        {
            var value = _utcNow();

            if (value.Kind != DateTimeKind.Utc)
            {
                throw new InvalidOperationException(
                    "Monitoring runtime clock must return UTC.");
            }

            return value;
        }
    }
}
