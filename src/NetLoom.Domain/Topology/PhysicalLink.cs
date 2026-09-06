using System;

namespace NetLoom.Domain.Topology
{
    public sealed class PhysicalLink
    {
        public PhysicalLink(
            Guid id,
            string linkKey,
            Guid deviceAId,
            Guid? interfaceAId,
            Guid deviceBId,
            Guid? interfaceBId,
            PhysicalLinkStrength strength,
            PhysicalLinkFreshness freshness,
            string mediaTypeResolved,
            long? speedBpsResolved,
            string sourceSummary,
            DateTime firstSeenUtc,
            DateTime lastSeenUtc,
            DateTime? lastConfirmedUtc,
            string resolverVersion,
            bool isHidden,
            bool isArchived,
            string notes)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException(
                    "Physical link id is required.",
                    nameof(id));
            }

            if (string.IsNullOrWhiteSpace(linkKey))
            {
                throw new ArgumentException(
                    "Physical link key is required.",
                    nameof(linkKey));
            }

            if (deviceAId == Guid.Empty ||
                deviceBId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Both device ids are required.");
            }

            if (deviceAId == deviceBId)
            {
                throw new ArgumentException(
                    "Physical link devices must differ.");
            }

            RequireUtc(firstSeenUtc, nameof(firstSeenUtc));
            RequireUtc(lastSeenUtc, nameof(lastSeenUtc));
            RequireUtc(lastConfirmedUtc, nameof(lastConfirmedUtc));

            if (lastSeenUtc < firstSeenUtc)
            {
                throw new ArgumentException(
                    "Last seen cannot be before first seen.",
                    nameof(lastSeenUtc));
            }

            if (speedBpsResolved.HasValue &&
                speedBpsResolved.Value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(speedBpsResolved));
            }

            Id = id;
            LinkKey = linkKey.Trim();
            DeviceAId = deviceAId;
            InterfaceAId = interfaceAId;
            DeviceBId = deviceBId;
            InterfaceBId = interfaceBId;
            Strength = strength;
            Freshness = freshness;
            MediaTypeResolved = Normalize(mediaTypeResolved);
            SpeedBpsResolved = speedBpsResolved;
            SourceSummary = Normalize(sourceSummary);
            FirstSeenUtc = firstSeenUtc;
            LastSeenUtc = lastSeenUtc;
            LastConfirmedUtc = lastConfirmedUtc;
            ResolverVersion = Normalize(resolverVersion);
            IsHidden = isHidden;
            IsArchived = isArchived;
            Notes = Normalize(notes);
        }

        public Guid Id { get; }

        public string LinkKey { get; }

        public Guid DeviceAId { get; }

        public Guid? InterfaceAId { get; }

        public Guid DeviceBId { get; }

        public Guid? InterfaceBId { get; }

        public PhysicalLinkStrength Strength { get; }

        public PhysicalLinkFreshness Freshness { get; }

        public string MediaTypeResolved { get; }

        public long? SpeedBpsResolved { get; }

        public string SourceSummary { get; }

        public DateTime FirstSeenUtc { get; }

        public DateTime LastSeenUtc { get; }

        public DateTime? LastConfirmedUtc { get; }

        public string ResolverVersion { get; }

        public bool IsHidden { get; }

        public bool IsArchived { get; }

        public string Notes { get; }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private static void RequireUtc(
            DateTime value,
            string parameterName)
        {
            if (value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Timestamp must be UTC.",
                    parameterName);
            }
        }

        private static void RequireUtc(
            DateTime? value,
            string parameterName)
        {
            if (value.HasValue)
            {
                RequireUtc(value.Value, parameterName);
            }
        }
    }
}
