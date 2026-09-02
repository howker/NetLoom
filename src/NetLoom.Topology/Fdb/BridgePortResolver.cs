using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Domain.Observations.Fdb;

namespace NetLoom.Topology.Fdb
{
    public sealed class BridgePortResolver
    {
        private readonly IReadOnlyList<BridgePortMapping> _mappings;

        public BridgePortResolver(
            IEnumerable<BridgePortMapping> mappings)
        {
            if (mappings == null)
            {
                throw new ArgumentNullException(nameof(mappings));
            }

            _mappings = mappings.ToArray();
        }

        public int? ResolveIfIndex(
            int bridgePortIndex)
        {
            if (bridgePortIndex < 1)
            {
                return null;
            }

            var candidates =
                _mappings
                    .Where(
                        mapping =>
                            mapping.BridgePortIndex ==
                            bridgePortIndex)
                    .Select(mapping => mapping.IfIndex)
                    .Distinct()
                    .ToArray();

            if (candidates.Length != 1)
            {
                return null;
            }

            return candidates[0];
        }

        public int? ResolveIfIndex(
            FdbEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (!entry.BridgePortIndex.HasValue)
            {
                return null;
            }

            return ResolveIfIndex(
                entry.BridgePortIndex.Value);
        }
    }
}
