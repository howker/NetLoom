using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Topology.Lifecycle;
using NetLoom.Topology.Resolution;

namespace NetLoom.Topology.Map
{
    public sealed class TopologyMapProjector
    {
        public MapSnapshot Project(
            IEnumerable<MapProjectionItem> items,
            DateTime generatedUtc)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (generatedUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Generated time must be UTC.",
                    nameof(generatedUtc));
            }

            var sourceItems = items.ToArray();
            var nodeClaims =
                new Dictionary<string, LinkEndpointClaim>(
                    StringComparer.Ordinal);

            var links =
                new Dictionary<string, LinkAccumulator>(
                    StringComparer.Ordinal);

            foreach (var item in sourceItems)
            {
                var candidate = item.Candidate;

                var localKey =
                    BuildNodeKey(
                        candidate.LocalEndpoint);

                var remoteKey =
                    BuildNodeKey(
                        candidate.RemoteEndpoint);

                if (string.Equals(
                    localKey,
                    remoteKey,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                if (!nodeClaims.ContainsKey(localKey))
                {
                    nodeClaims.Add(
                        localKey,
                        candidate.LocalEndpoint);
                }

                if (!nodeClaims.ContainsKey(remoteKey))
                {
                    nodeClaims.Add(
                        remoteKey,
                        candidate.RemoteEndpoint);
                }

                AddLink(
                    links,
                    localKey,
                    candidate.LocalEndpoint,
                    remoteKey,
                    candidate.RemoteEndpoint,
                    candidate,
                    item.Freshness);
            }

            var orderedClaims =
                nodeClaims
                    .Select(
                        pair => new NodeClaim(
                            pair.Key,
                            pair.Value))
                    .OrderBy(
                        item => BuildLabel(item.Claim),
                        StringComparer.OrdinalIgnoreCase)
                    .ThenBy(
                        item => item.Key,
                        StringComparer.Ordinal)
                    .ToArray();

            var nodes =
                Layout(orderedClaims);

            var projectedLinks =
                links.Values
                    .OrderBy(
                        item => item.Key,
                        StringComparer.Ordinal)
                    .Select(item => item.ToMapLink())
                    .ToArray();

            return new MapSnapshot(
                generatedUtc,
                nodes,
                projectedLinks);
        }

        private static IReadOnlyList<MapNode> Layout(
            IReadOnlyList<NodeClaim> claims)
        {
            if (claims.Count == 0)
            {
                return new MapNode[0];
            }

            var columns =
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        Math.Sqrt(claims.Count)));

            var result =
                new List<MapNode>(claims.Count);

            for (var index = 0;
                 index < claims.Count;
                 index++)
            {
                var row = index / columns;
                var column = index % columns;

                result.Add(
                    new MapNode(
                        claims[index].Key,
                        BuildLabel(
                            claims[index].Claim) ??
                        claims[index].Key,
                        BuildSecondaryText(
                            claims[index].Claim),
                        60.0 + (column * 240.0),
                        60.0 + (row * 150.0)));
            }

            return result;
        }

        private static void AddLink(
            IDictionary<string, LinkAccumulator> links,
            string localKey,
            LinkEndpointClaim local,
            string remoteKey,
            LinkEndpointClaim remote,
            PhysicalLinkCandidate candidate,
            TopologyFreshness freshness)
        {
            var sourceKey = localKey;
            var targetKey = remoteKey;
            var sourceEndpoint = local;
            var targetEndpoint = remote;

            if (string.CompareOrdinal(
                    sourceKey,
                    targetKey) > 0)
            {
                sourceKey = remoteKey;
                targetKey = localKey;
                sourceEndpoint = remote;
                targetEndpoint = local;
            }

            var sourcePort =
                BuildPortLabel(sourceEndpoint);

            var targetPort =
                BuildPortLabel(targetEndpoint);

            var key =
                BuildLinkKey(
                    sourceKey,
                    sourcePort,
                    targetKey,
                    targetPort);

            LinkAccumulator accumulator;

            if (!links.TryGetValue(
                key,
                out accumulator))
            {
                accumulator =
                    new LinkAccumulator(
                        key,
                        sourceKey,
                        targetKey,
                        sourcePort,
                        targetPort,
                        MapConfidence.Low,
                        MapFreshness.Stale);

                links.Add(
                    key,
                    accumulator);
            }

            accumulator.Merge(
                candidate,
                freshness);
        }

        private static string BuildNodeKey(
            LinkEndpointClaim endpoint)
        {
            string kind;
            string value;

            if (!string.IsNullOrWhiteSpace(
                endpoint.ChassisId))
            {
                kind = "chassis";
                value = endpoint.ChassisId;
            }
            else if (!string.IsNullOrWhiteSpace(
                endpoint.DeviceIdClaim))
            {
                kind = "device-claim";
                value = endpoint.DeviceIdClaim;
            }
            else if (!string.IsNullOrWhiteSpace(
                endpoint.ManagementAddress))
            {
                kind = "management-claim";
                value = endpoint.ManagementAddress;
            }
            else if (!string.IsNullOrWhiteSpace(
                endpoint.SystemName))
            {
                kind = "name-claim";
                value = endpoint.SystemName;
            }
            else
            {
                kind = "anonymous-endpoint";
                value =
                    BuildPortLabel(endpoint) ??
                    "unknown";
            }

            return Hash(
                kind + ":" +
                NormalizeIdentity(value));
        }

        private static string BuildLinkKey(
            string sourceKey,
            string sourcePort,
            string targetKey,
            string targetPort)
        {
            return Hash(
                "link|" +
                sourceKey + "|" +
                (sourcePort ?? string.Empty) + "|" +
                targetKey + "|" +
                (targetPort ?? string.Empty));
        }

        private static string BuildLabel(
            LinkEndpointClaim endpoint)
        {
            if (!string.IsNullOrWhiteSpace(
                endpoint.SystemName))
            {
                return endpoint.SystemName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(
                endpoint.DeviceIdClaim))
            {
                return endpoint.DeviceIdClaim.Trim();
            }

            if (!string.IsNullOrWhiteSpace(
                endpoint.ChassisId))
            {
                return endpoint.ChassisId.Trim();
            }

            if (!string.IsNullOrWhiteSpace(
                endpoint.ManagementAddress))
            {
                return endpoint.ManagementAddress.Trim();
            }

            return null;
        }

        private static string BuildSecondaryText(
            LinkEndpointClaim endpoint)
        {
            var label =
                BuildLabel(endpoint);

            if (!string.IsNullOrWhiteSpace(
                    endpoint.ManagementAddress) &&
                !string.Equals(
                    label,
                    endpoint.ManagementAddress.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return endpoint.ManagementAddress.Trim();
            }

            if (!string.IsNullOrWhiteSpace(
                    endpoint.ChassisId) &&
                !string.Equals(
                    label,
                    endpoint.ChassisId.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return endpoint.ChassisId.Trim();
            }

            return null;
        }

        private static string BuildPortLabel(
            LinkEndpointClaim endpoint)
        {
            if (!string.IsNullOrWhiteSpace(
                endpoint.PortId))
            {
                return endpoint.PortId.Trim();
            }

            if (!string.IsNullOrWhiteSpace(
                endpoint.PortDescription))
            {
                return endpoint.PortDescription.Trim();
            }

            if (endpoint.PortIndex.HasValue)
            {
                return "#" +
                    endpoint.PortIndex.Value.ToString(
                        CultureInfo.InvariantCulture);
            }

            return null;
        }

        private static string NormalizeIdentity(
            string value)
        {
            var trimmed =
                value.Trim();

            var hex =
                trimmed
                    .Where(
                        character =>
                            (character >= '0' &&
                             character <= '9') ||
                            (character >= 'a' &&
                             character <= 'f') ||
                            (character >= 'A' &&
                             character <= 'F'))
                    .ToArray();

            if (hex.Length == 12)
            {
                return new string(hex)
                    .ToUpperInvariant();
            }

            return trimmed.ToUpperInvariant();
        }

        private static string Hash(
            string material)
        {
            using (var sha = SHA256.Create())
            {
                var bytes =
                    Encoding.UTF8.GetBytes(material);

                var hash =
                    sha.ComputeHash(bytes);

                var result =
                    new StringBuilder("map-");

                for (var index = 0;
                     index < 12;
                     index++)
                {
                    result.Append(
                        hash[index].ToString(
                            "x2",
                            CultureInfo.InvariantCulture));
                }

                return result.ToString();
            }
        }

        private static MapConfidence ToMapConfidence(
            TopologyConfidence value)
        {
            switch (value)
            {
                case TopologyConfidence.High:
                    return MapConfidence.High;

                case TopologyConfidence.Medium:
                    return MapConfidence.Medium;

                default:
                    return MapConfidence.Low;
            }
        }

        private static MapFreshness ToMapFreshness(
            TopologyFreshness value)
        {
            switch (value)
            {
                case TopologyFreshness.Fresh:
                    return MapFreshness.Fresh;

                case TopologyFreshness.Aging:
                    return MapFreshness.Aging;

                default:
                    return MapFreshness.Stale;
            }
        }

        private static MapEvidenceItem ToMapEvidence(
            TopologyEvidence evidence)
        {
            return new MapEvidenceItem(
                ToMapEvidenceKind(evidence.Kind),
                evidence.Strength ==
                    TopologyEvidenceStrength.Strong
                        ? MapEvidenceStrength.Strong
                        : MapEvidenceStrength.Weak,
                evidence.ObservationId,
                evidence.CapturedUtc,
                evidence.SourceAddress,
                evidence.Detail);
        }

        private static MapEvidenceKind ToMapEvidenceKind(
            TopologyEvidenceKind value)
        {
            switch (value)
            {
                case TopologyEvidenceKind.Lldp:
                    return MapEvidenceKind.Lldp;

                case TopologyEvidenceKind.Cdp:
                    return MapEvidenceKind.Cdp;

                default:
                    return MapEvidenceKind.ArpFdbCorrelation;
            }
        }

        private sealed class NodeClaim
        {
            public NodeClaim(
                string key,
                LinkEndpointClaim claim)
            {
                Key = key;
                Claim = claim;
            }

            public string Key { get; }

            public LinkEndpointClaim Claim { get; }
        }

        private sealed class LinkAccumulator
        {
            private readonly Dictionary<string, MapEvidenceItem>
                _evidence =
                    new Dictionary<string, MapEvidenceItem>(
                        StringComparer.Ordinal);

            public LinkAccumulator(
                string key,
                string sourceNodeKey,
                string targetNodeKey,
                string sourcePortLabel,
                string targetPortLabel,
                MapConfidence confidence,
                MapFreshness freshness)
            {
                Key = key;
                SourceNodeKey = sourceNodeKey;
                TargetNodeKey = targetNodeKey;
                SourcePortLabel = sourcePortLabel;
                TargetPortLabel = targetPortLabel;
                Confidence = confidence;
                Freshness = freshness;
            }

            public string Key { get; }

            public string SourceNodeKey { get; }

            public string TargetNodeKey { get; }

            public string SourcePortLabel { get; }

            public string TargetPortLabel { get; }

            public MapConfidence Confidence { get; private set; }

            public MapFreshness Freshness { get; private set; }

            public void Merge(
                PhysicalLinkCandidate candidate,
                TopologyFreshness freshness)
            {
                var confidence =
                    ToMapConfidence(
                        candidate.Confidence);

                if ((int)confidence >
                    (int)Confidence)
                {
                    Confidence = confidence;
                }

                var mapFreshness =
                    ToMapFreshness(freshness);

                if ((int)mapFreshness <
                    (int)Freshness)
                {
                    Freshness = mapFreshness;
                }

                foreach (var evidence in
                    candidate.Evidence)
                {
                    var mapped =
                        ToMapEvidence(evidence);

                    var evidenceKey =
                        mapped.Kind + "|" +
                        mapped.Strength + "|" +
                        mapped.ObservationId + "|" +
                        mapped.SourceAddress + "|" +
                        mapped.Detail;

                    if (!_evidence.ContainsKey(
                        evidenceKey))
                    {
                        _evidence.Add(
                            evidenceKey,
                            mapped);
                    }
                }
            }

            public MapLink ToMapLink()
            {
                return new MapLink(
                    Key,
                    SourceNodeKey,
                    TargetNodeKey,
                    SourcePortLabel,
                    TargetPortLabel,
                    Confidence,
                    Freshness,
                    _evidence
                        .OrderBy(
                            pair => pair.Key,
                            StringComparer.Ordinal)
                        .Select(pair => pair.Value)
                        .ToArray());
            }
        }
    }
}
