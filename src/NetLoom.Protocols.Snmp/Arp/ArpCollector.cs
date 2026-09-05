using System;
using System.Collections.Generic;
using NetLoom.Application.Observations;
using NetLoom.Application.Observations.Arp;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Arp;

namespace NetLoom.Protocols.Snmp.Arp
{
    public sealed class ArpCollector
        : IArpCollector
    {
        private const string ModernEntry =
            "1.3.6.1.2.1.4.35.1";

        private const string LegacyEntry =
            "1.3.6.1.2.1.4.22.1";

        private readonly ISnmpTransport _transport;
        private readonly IObservationStore _rawStore;
        private readonly IArpObservationStore _arpStore;
        private readonly IArpObservationParser _parser;

        public ArpCollector(
            ISnmpTransport transport,
            IObservationStore rawStore,
            IArpObservationStore arpStore,
            IArpObservationParser parser)
        {
            _transport = transport ??
                throw new ArgumentNullException(nameof(transport));

            _rawStore = rawStore ??
                throw new ArgumentNullException(nameof(rawStore));

            _arpStore = arpStore ??
                throw new ArgumentNullException(nameof(arpStore));

            _parser = parser ??
                throw new ArgumentNullException(nameof(parser));
        }

        public ArpObservation Collect(
            ArpCollectionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            IReadOnlyList<SnmpVariable> variables;

            try
            {
                variables =
                    Walk(request, ModernEntry);
            }
            catch (SnmpTransportException exception)
            {
                if (exception.Failure !=
                    SnmpTransportFailure.Protocol)
                {
                    throw;
                }

                variables =
                    Walk(request, LegacyEntry);
            }

            var raw =
                new SnmpObservation(
                    new Observation(
                        Guid.NewGuid(),
                        ObservationKind.Arp,
                        request.Address.ToString(),
                        DateTime.UtcNow),
                    variables);

            // Сначала сохраняем raw evidence.
            _rawStore.SaveSnmp(raw);

            var parsed =
                _parser.Parse(raw);

            // атем сохраняем нормализованное наблюдение.
            _arpStore.Save(parsed);

            return parsed;
        }

        private IReadOnlyList<SnmpVariable> Walk(
            ArpCollectionRequest request,
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
