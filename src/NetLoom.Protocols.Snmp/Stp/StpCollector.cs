using System;
using System.Collections.Generic;
using NetLoom.Application.Observations;
using NetLoom.Application.Observations.Stp;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Stp;

namespace NetLoom.Protocols.Snmp.Stp
{
    public sealed class StpCollector :
        IStpCollector
    {
        private const string BasePortIfIndex =
            "1.3.6.1.2.1.17.1.4.1.2";

        private const string StpPortEntry =
            "1.3.6.1.2.1.17.2.15.1";

        private static readonly string[] Scalars =
        {
            "1.3.6.1.2.1.17.2.1.0",
            "1.3.6.1.2.1.17.2.5.0",
            "1.3.6.1.2.1.17.2.6.0",
            "1.3.6.1.2.1.17.2.7.0"
        };

        private readonly ISnmpTransport _transport;
        private readonly IObservationStore _rawStore;
        private readonly IStpObservationStore _stpStore;
        private readonly IStpObservationParser _parser;
        private readonly Func<DateTime> _utcNow;

        public StpCollector(
            ISnmpTransport transport,
            IObservationStore rawStore,
            IStpObservationStore stpStore,
            IStpObservationParser parser,
            Func<DateTime> utcNow = null)
        {
            _transport = transport ??
                throw new ArgumentNullException(
                    nameof(transport));

            _rawStore = rawStore ??
                throw new ArgumentNullException(
                    nameof(rawStore));

            _stpStore = stpStore ??
                throw new ArgumentNullException(
                    nameof(stpStore));

            _parser = parser ??
                throw new ArgumentNullException(
                    nameof(parser));

            _utcNow =
                utcNow ??
                (() => DateTime.UtcNow);
        }

        public StpObservation Collect(
            StpCollectionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(
                    nameof(request));
            }

            var variables =
                new List<SnmpVariable>();

            variables.AddRange(
                _transport.Get(
                    new SnmpGetRequest(
                        request.Address,
                        request.Port,
                        request.Version,
                        request.Credentials,
                        Scalars,
                        request.TimeoutMilliseconds,
                        request.RetryCount)));

            variables.AddRange(
                Walk(
                    request,
                    BasePortIfIndex));

            variables.AddRange(
                Walk(
                    request,
                    StpPortEntry));

            var capturedUtc =
                _utcNow();

            if (capturedUtc.Kind !=
                DateTimeKind.Utc)
            {
                throw new InvalidOperationException(
                    "STP collector clock must return UTC.");
            }

            var raw =
                new SnmpObservation(
                    new Observation(
                        Guid.NewGuid(),
                        ObservationKind.Stp,
                        request.Address.ToString(),
                        capturedUtc),
                    variables);

            _rawStore.SaveSnmp(raw);

            var parsed =
                _parser.Parse(raw);

            _stpStore.Save(parsed);

            return parsed;
        }

        private IReadOnlyList<SnmpVariable> Walk(
            StpCollectionRequest request,
            string rootOid)
        {
            return _transport.Walk(
                new SnmpWalkRequest(
                    request.Address,
                    request.Port,
                    request.Version,
                    request.Credentials,
                    rootOid,
                    request.TimeoutMilliseconds,
                    request.RetryCount,
                    request.MaxRepetitions));
        }
    }
}
