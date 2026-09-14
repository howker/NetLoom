using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Contracts.TopologyMap
{
    public sealed class MapLink
    {
        public MapLink(
            string key,
            string sourceNodeKey,
            string targetNodeKey,
            string sourcePortLabel,
            string targetPortLabel,
            MapConfidence confidence,
            MapFreshness freshness,
            IEnumerable<MapEvidenceItem> evidence,
            Guid? physicalLinkId = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(
                    "Map link key is required.",
                    nameof(key));
            }

            if (string.IsNullOrWhiteSpace(sourceNodeKey))
            {
                throw new ArgumentException(
                    "Source node key is required.",
                    nameof(sourceNodeKey));
            }

            if (string.IsNullOrWhiteSpace(targetNodeKey))
            {
                throw new ArgumentException(
                    "Target node key is required.",
                    nameof(targetNodeKey));
            }

            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            Key = key;
            SourceNodeKey = sourceNodeKey;
            TargetNodeKey = targetNodeKey;
            SourcePortLabel = sourcePortLabel;
            TargetPortLabel = targetPortLabel;
            Confidence = confidence;
            Freshness = freshness;
            Evidence = evidence.ToArray();
            PhysicalLinkId = physicalLinkId;
        }

        public string Key { get; }

        public string SourceNodeKey { get; }

        public string TargetNodeKey { get; }

        public string SourcePortLabel { get; }

        public string TargetPortLabel { get; }

        public MapConfidence Confidence { get; }

        public MapFreshness Freshness { get; }

        public IReadOnlyList<MapEvidenceItem> Evidence { get; }

        public Guid? PhysicalLinkId { get; }
    }
}
