using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Domain.Observations.Arp;
using NetLoom.Domain.Observations.Fdb;
using NetLoom.Topology.Fdb;

namespace NetLoom.Topology.Correlation
{
    public sealed class MacCorrelationResolver
    {
        public IReadOnlyList<MacCorrelation> Correlate(
            ArpObservation arp,
            FdbObservation fdb)
        {
            if (arp == null)
            {
                throw new ArgumentNullException(nameof(arp));
            }

            if (fdb == null)
            {
                throw new ArgumentNullException(nameof(fdb));
            }

            var bridgeResolver =
                new BridgePortResolver(
                    fdb.BridgePortMappings);

            var fdbByMac =
                fdb.Entries
                    .Where(
                        entry =>
                            entry.BridgePortIndex.HasValue &&
                            entry.BridgePortIndex.Value > 0)
                    .GroupBy(
                        entry => entry.MacAddress,
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        group => group.Key,
                        group => group.ToArray(),
                        StringComparer.OrdinalIgnoreCase);

            var result =
                new List<MacCorrelation>();

            foreach (var arpEntry in arp.Entries)
            {
                if (!IsUsableArpEntry(arpEntry))
                {
                    continue;
                }

                FdbEntry[] matches;

                if (!fdbByMac.TryGetValue(
                    arpEntry.PhysicalAddress,
                    out matches))
                {
                    continue;
                }

                foreach (var match in matches)
                {
                    var bridgePortIndex =
                        match.BridgePortIndex.Value;

                    result.Add(
                        new MacCorrelation(
                            arpEntry.IpAddress,
                            arpEntry.PhysicalAddress,
                            arp.Observation.SourceAddress,
                            arpEntry.IfIndex,
                            fdb.Observation.SourceAddress,
                            bridgePortIndex,
                            bridgeResolver.ResolveIfIndex(
                                bridgePortIndex)));
                }
            }

            return result;
        }

        private static bool IsUsableArpEntry(
            ArpEntry entry)
        {
            if (entry == null ||
                string.IsNullOrWhiteSpace(
                    entry.PhysicalAddress))
            {
                return false;
            }

            // invalid(2)
            if (entry.Type == 2)
            {
                return false;
            }

            // modern local(5) ? ??? ??????????? ????? ??????????,
            // ? ?? ?????.
            if (entry.TableKind ==
                    ArpTableKind.IpNetToPhysical &&
                entry.Type == 5)
            {
                return false;
            }

            // modern incomplete(7) / invalid(5)
            if (entry.TableKind ==
                    ArpTableKind.IpNetToPhysical &&
                (entry.State == 7 ||
                 entry.State == 5))
            {
                return false;
            }

            return true;
        }
    }
}
