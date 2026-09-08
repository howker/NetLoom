using System;

namespace NetLoom.Application.Lookup
{
    public sealed class MacIpLookupSearchService
    {
        private readonly IMacIpLookupReader _reader;

        public MacIpLookupSearchService(
            IMacIpLookupReader reader)
        {
            _reader =
                reader ??
                throw new ArgumentNullException(
                    nameof(reader));
        }

        public MacIpLookupResult Search(
            string query,
            int maxCandidates)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException(
                    "Search query is required.",
                    nameof(query));
            }

            var normalizedIp =
                AddressTextNormalizer.NormalizeIp(
                    query);

            if (normalizedIp != null)
            {
                return _reader.FindByIp(
                    normalizedIp,
                    maxCandidates);
            }

            var normalizedMac =
                AddressTextNormalizer
                    .NormalizeMacColon(
                        query);

            if (normalizedMac != null)
            {
                return _reader.FindByMac(
                    normalizedMac,
                    maxCandidates);
            }

            throw new ArgumentException(
                "Search query must be a valid IP or MAC address.",
                nameof(query));
        }
    }
}