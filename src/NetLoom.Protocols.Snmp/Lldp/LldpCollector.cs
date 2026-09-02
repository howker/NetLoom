using System;
using System.Collections.Generic;
using NetLoom.Application.Observations;
using NetLoom.Application.Observations.Lldp;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Lldp;

namespace NetLoom.Protocols.Snmp.Lldp
{
    public sealed class LldpCollector
        : ILldpCollector
    {
        private const string LocalPortEntry =
            "1.0.8802.1.1.2.1.3.7.1";

        private const string RemoteEntry =
            "1.0.8802.1.1.2.1.4.1.1";

        private readonly ISnmpTransport _transport;
        private readonly IObservationStore _rawStore;
        private readonly ILldpObservationStore _lldpStore;
        private readonly ILldpObservationParser _parser;

        public LldpCollector(
            ISnmpTransport transport,
            IObservationStore rawStore,
            ILldpObservationStore lldpStore,
            ILldpObservationParser parser)
        {
            _transport = transport ??
                throw new ArgumentNullException(nameof(transport));

            _rawStore = rawStore ??
                throw new ArgumentNullException(nameof(rawStore));

            _lldpStore = lldpStore ??
                throw new ArgumentNullException(nameof(lldpStore));

            _parser = parser ??
                throw new ArgumentNullException(nameof(parser));
        }

        public LldpObservation Collect(
            LldpCollectionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var variables =
                new List<SnmpVariable>();

            variables.AddRange(
                Walk(request, LocalPortEntry));

            variables.AddRange(
                Walk(request, RemoteEntry));

            var raw =
                new SnmpObservation(
                    new Observation(
                        Guid.NewGuid(),
                        ObservationKind.Lldp,
                        request.Address.ToString(),
                        DateTime.UtcNow),
                    variables);

            // Raw observation сохраняется первым:
            // его можно повторно обработать новым парсером.
            _rawStore.SaveSnmp(raw);

            var parsed =
                _parser.Parse(raw);

            _lldpStore.Save(parsed);

            return parsed;
        }

        private IReadOnlyList<SnmpVariable> Walk(
            LldpCollectionRequest request,
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
