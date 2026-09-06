using System;
using System.Collections.Generic;
using NetLoom.Application.Monitoring.Health;
using NetLoom.Application.Monitoring.Interfaces;
using NetLoom.Application.Observations.Arp;
using NetLoom.Application.Observations.Cdp;
using NetLoom.Application.Observations.Fdb;
using NetLoom.Application.Observations.Lldp;
using NetLoom.Application.Observations.Stp;

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
            IStpCollector stpCollector = null)
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
            _stpCollector = stpCollector;

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
                        _lldpCollector.Collect(
                            new LldpCollectionRequest(
                                request.Address,
                                request.Port,
                                request.Version,
                                request.Credentials,
                                request.TimeoutMilliseconds,
                                request.RetryCount,
                                request.MaxRepetitions));
                        break;

                    case MonitoringPollKind.Cdp:
                        _cdpCollector.Collect(
                            new CdpCollectionRequest(
                                request.Address,
                                request.Port,
                                request.Version,
                                request.Credentials,
                                request.TimeoutMilliseconds,
                                request.RetryCount,
                                request.MaxRepetitions));
                        break;

                    case MonitoringPollKind.Fdb:
                        _fdbCollector.Collect(
                            new FdbCollectionRequest(
                                request.Address,
                                request.Port,
                                request.Version,
                                request.Credentials,
                                request.TimeoutMilliseconds,
                                request.RetryCount,
                                request.MaxRepetitions));
                        break;

                    case MonitoringPollKind.Arp:
                        _arpCollector.Collect(
                            new ArpCollectionRequest(
                                request.Address,
                                request.Port,
                                request.Version,
                                request.Credentials,
                                request.TimeoutMilliseconds,
                                request.RetryCount,
                                request.MaxRepetitions));
                        break;

                    case MonitoringPollKind.Stp:
                        if (_stpCollector == null)
                        {
                            throw new InvalidOperationException(
                                "STP collector is not configured.");
                        }

                        _stpCollector.Collect(
                            new StpCollectionRequest(
                                request.Address,
                                request.Port,
                                request.Version,
                                request.Credentials,
                                request.TimeoutMilliseconds,
                                request.RetryCount,
                                request.MaxRepetitions));
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

                        return new MonitoringPollStepResult(
                            kind,
                            true,
                            null,
                            null,
                            null,
                            interfaces);

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
