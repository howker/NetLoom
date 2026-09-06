using System;

namespace NetLoom.Domain.Topology
{
    public sealed class TopologyDevice
    {
        public TopologyDevice(
            Guid id,
            Guid? locationId,
            string customName,
            DeviceCategory category,
            DeviceDiscoveryOrigin discoveryOrigin,
            MonitoringCapability monitoringCapability,
            string vendorOverride,
            string modelOverride,
            string notes,
            bool isHidden,
            bool isArchived,
            DateTime? firstSeenUtc,
            DateTime? lastSeenUtc,
            DateTime? lastResolvedUtc)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException(
                    "Device id is required.",
                    nameof(id));
            }

            RequireUtc(firstSeenUtc, nameof(firstSeenUtc));
            RequireUtc(lastSeenUtc, nameof(lastSeenUtc));
            RequireUtc(lastResolvedUtc, nameof(lastResolvedUtc));

            if (firstSeenUtc.HasValue &&
                lastSeenUtc.HasValue &&
                lastSeenUtc.Value < firstSeenUtc.Value)
            {
                throw new ArgumentException(
                    "Last seen cannot be before first seen.",
                    nameof(lastSeenUtc));
            }

            Id = id;
            LocationId = locationId;
            CustomName = Normalize(customName);
            Category = category;
            DiscoveryOrigin = discoveryOrigin;
            MonitoringCapability = monitoringCapability;
            VendorOverride = Normalize(vendorOverride);
            ModelOverride = Normalize(modelOverride);
            Notes = Normalize(notes);
            IsHidden = isHidden;
            IsArchived = isArchived;
            FirstSeenUtc = firstSeenUtc;
            LastSeenUtc = lastSeenUtc;
            LastResolvedUtc = lastResolvedUtc;
        }

        public Guid Id { get; }

        public Guid? LocationId { get; }

        public string CustomName { get; }

        public DeviceCategory Category { get; }

        public DeviceDiscoveryOrigin DiscoveryOrigin { get; }

        public MonitoringCapability MonitoringCapability { get; }

        public string VendorOverride { get; }

        public string ModelOverride { get; }

        public string Notes { get; }

        public bool IsHidden { get; }

        public bool IsArchived { get; }

        public DateTime? FirstSeenUtc { get; }

        public DateTime? LastSeenUtc { get; }

        public DateTime? LastResolvedUtc { get; }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private static void RequireUtc(
            DateTime? value,
            string parameterName)
        {
            if (value.HasValue &&
                value.Value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Timestamp must be UTC.",
                    parameterName);
            }
        }
    }
}
