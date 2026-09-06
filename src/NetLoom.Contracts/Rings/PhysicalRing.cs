using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Contracts.Rings
{
    public sealed class PhysicalRing
    {
        public PhysicalRing(
            string ringKey,
            IEnumerable<Guid> deviceIds,
            IEnumerable<Guid> physicalLinkIds)
        {
            if (string.IsNullOrWhiteSpace(ringKey))
            {
                throw new ArgumentException(
                    "Ring key is required.",
                    nameof(ringKey));
            }

            if (deviceIds == null)
            {
                throw new ArgumentNullException(
                    nameof(deviceIds));
            }

            if (physicalLinkIds == null)
            {
                throw new ArgumentNullException(
                    nameof(physicalLinkIds));
            }

            var devices =
                deviceIds
                    .OrderBy(id => id)
                    .ToArray();

            var links =
                physicalLinkIds
                    .OrderBy(id => id)
                    .ToArray();

            if (devices.Length < 2)
            {
                throw new ArgumentException(
                    "Physical ring requires at least two devices.",
                    nameof(deviceIds));
            }

            if (links.Length < 2)
            {
                throw new ArgumentException(
                    "Physical ring requires at least two links.",
                    nameof(physicalLinkIds));
            }

            if (devices.Distinct().Count() !=
                devices.Length)
            {
                throw new ArgumentException(
                    "Physical ring device ids must be unique.",
                    nameof(deviceIds));
            }

            if (links.Distinct().Count() !=
                links.Length)
            {
                throw new ArgumentException(
                    "Physical ring link ids must be unique.",
                    nameof(physicalLinkIds));
            }

            RingKey = ringKey.Trim();
            DeviceIds = devices;
            PhysicalLinkIds = links;
        }

        public string RingKey { get; }

        public IReadOnlyList<Guid> DeviceIds { get; }

        public IReadOnlyList<Guid> PhysicalLinkIds { get; }

        public bool IsParallelLinkCycle
        {
            get
            {
                return
                    DeviceIds.Count == 2 &&
                    PhysicalLinkIds.Count == 2;
            }
        }
    }
}
