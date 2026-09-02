using System;
using NetLoom.Application.Observations;
using NetLoom.Application.Observations.Cdp;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Cdp;

namespace NetLoom.Protocols.Snmp.Cdp
{
    public sealed class CdpCollector
        : ICdpCollector
    {
        private const string CdpCacheEntry =
            "1.3.6.1.4.1.9.9.23.1.2.1.1";

        private readonly ISnmpTransport _transport;
        private readonly IObservationStore _rawStore;
        private readonly ICdpObservationStore _cdpStore;
        private readonly ICdpObservationParser _parser;

        public CdpCollector(
            ISnmpTransport transport,
            IObservationStore rawStore,
            ICdpObservationStore cdpStore,
            ICdpObservationParser parser)
        {
            _transport = transport ??
                throw new ArgumentNullException(nameof(transport));

            _rawStore = rawStore ??
                throw new ArgumentNullException(nameof(rawStore));

            _cdpStore = cdpStore ??
                throw new ArgumentNullException(nameof(cdpStore));

            _parser = parser ??
                throw new ArgumentNullException(nameof(parser));
        }

        public CdpObservation Collect(
            CdpCollectionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var variables =
                _transport.Walk(
                    new SnmpWalkRequest(
                        request.Address,
                        request.Port,
                        request.Version,
                        request.Credentials,
                        CdpCacheEntry,
                        request.TimeoutMilliseconds,
                        request.RetryCount,
                        request.MaxRepetitions));

            var raw =
                new SnmpObservation(
                    new Observation(
                        Guid.NewGuid(),
                        ObservationKind.Cdp,
                        request.Address.ToString(),
                        DateTime.UtcNow),
                    variables);

            _rawStore.SaveSnmp(raw);

            var parsed =
                _parser.Parse(raw);

            _cdpStore.Save(parsed);

            return parsed;
        }
    }
}
