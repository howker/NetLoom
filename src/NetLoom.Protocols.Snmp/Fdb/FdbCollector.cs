using System;
using System.Collections.Generic;
using NetLoom.Application.Observations;
using NetLoom.Application.Observations.Fdb;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Fdb;

namespace NetLoom.Protocols.Snmp.Fdb
{
    public sealed class FdbCollector
        : IFdbCollector
    {
        private const string BasePortIfIndex =
            "1.3.6.1.2.1.17.1.4.1.2";

        private const string FdbEntry =
            "1.3.6.1.2.1.17.4.3.1";

        private readonly ISnmpTransport _transport;
        private readonly IObservationStore _rawStore;
        private readonly IFdbObservationStore _fdbStore;
        private readonly IFdbObservationParser _parser;

        public FdbCollector(
            ISnmpTransport transport,
            IObservationStore rawStore,
            IFdbObservationStore fdbStore,
            IFdbObservationParser parser)
        {
            _transport = transport ??
                throw new ArgumentNullException(nameof(transport));

            _rawStore = rawStore ??
                throw new ArgumentNullException(nameof(rawStore));

            _fdbStore = fdbStore ??
                throw new ArgumentNullException(nameof(fdbStore));

            _parser = parser ??
                throw new ArgumentNullException(nameof(parser));
        }

        public FdbObservation Collect(
            FdbCollectionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var variables =
                new List<SnmpVariable>();

            variables.AddRange(
                Walk(request, BasePortIfIndex));

            variables.AddRange(
                Walk(request, FdbEntry));

            var raw =
                new SnmpObservation(
                    new Observation(
                        Guid.NewGuid(),
                        ObservationKind.Fdb,
                        request.Address.ToString(),
                        DateTime.UtcNow),
                    variables);

            // Сначала сохраняем исходное SNMP evidence.
            _rawStore.SaveSnmp(raw);

            var parsed =
                _parser.Parse(raw);

            _fdbStore.Save(parsed);

            return parsed;
        }

        private IReadOnlyList<SnmpVariable> Walk(
            FdbCollectionRequest request,
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
