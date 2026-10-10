using System;
using System.Collections.Generic;

namespace NetLoom.Application.Topology
{
    public struct TopologyConflictKey : IEquatable<TopologyConflictKey>
    {
        public TopologyConflictKey(Guid manualLinkId, Guid observedLinkId)
        {
            ManualLinkId = manualLinkId;
            ObservedLinkId = observedLinkId;
        }

        public Guid ManualLinkId { get; }
        public Guid ObservedLinkId { get; }

        public bool Equals(TopologyConflictKey other) =>
            ManualLinkId == other.ManualLinkId && ObservedLinkId == other.ObservedLinkId;
        public override bool Equals(object obj) => obj is TopologyConflictKey && Equals((TopologyConflictKey)obj);
        public override int GetHashCode() => unchecked(ManualLinkId.GetHashCode() * 397 ^ ObservedLinkId.GetHashCode());
    }

    public interface ITopologyConflictAcknowledgementStore
    {
        IReadOnlyCollection<TopologyConflictKey> List();
        void Acknowledge(Guid manualLinkId, Guid observedLinkId, DateTime acknowledgedUtc);
    }
}
