using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Contracts.GraphSafety
{
    public sealed class PhysicalLinkFailureImpact
    {
        public PhysicalLinkFailureImpact(
            Guid physicalLinkId,
            Guid deviceAId,
            Guid deviceBId,
            IEnumerable<Guid> sideADeviceIds,
            IEnumerable<Guid> sideBDeviceIds)
        {
            if (physicalLinkId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Physical link id is required.",
                    nameof(physicalLinkId));
            }

            if (deviceAId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Device A id is required.",
                    nameof(deviceAId));
            }

            if (deviceBId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Device B id is required.",
                    nameof(deviceBId));
            }

            if (deviceAId == deviceBId)
            {
                throw new ArgumentException(
                    "Physical link endpoints must be different.");
            }

            if (sideADeviceIds == null)
            {
                throw new ArgumentNullException(
                    nameof(sideADeviceIds));
            }

            if (sideBDeviceIds == null)
            {
                throw new ArgumentNullException(
                    nameof(sideBDeviceIds));
            }

            var sideA =
                sideADeviceIds
                    .Distinct()
                    .OrderBy(id => id)
                    .ToArray();

            var sideB =
                sideBDeviceIds
                    .Distinct()
                    .OrderBy(id => id)
                    .ToArray();

            if (sideA.Any(id => id == Guid.Empty) ||
                sideB.Any(id => id == Guid.Empty))
            {
                throw new ArgumentException(
                    "Blast-radius device ids cannot be empty.");
            }

            var hasA =
                sideA.Length > 0;

            var hasB =
                sideB.Length > 0;

            if (hasA != hasB)
            {
                throw new ArgumentException(
                    "Bridge partitions must both be present or both be empty.");
            }

            if (hasA)
            {
                if (!sideA.Contains(deviceAId) ||
                    !sideB.Contains(deviceBId))
                {
                    throw new ArgumentException(
                        "Bridge partitions must contain their physical-link endpoints.");
                }

                var overlap =
                    new HashSet<Guid>(
                        sideA);

                overlap.IntersectWith(
                    sideB);

                if (overlap.Count > 0)
                {
                    throw new ArgumentException(
                        "Bridge partitions cannot overlap.");
                }
            }

            PhysicalLinkId =
                physicalLinkId;

            DeviceAId =
                deviceAId;

            DeviceBId =
                deviceBId;

            SideADeviceIds =
                sideA;

            SideBDeviceIds =
                sideB;
        }

        public Guid PhysicalLinkId { get; }

        public Guid DeviceAId { get; }

        public Guid DeviceBId { get; }

        public IReadOnlyList<Guid> SideADeviceIds { get; }

        public IReadOnlyList<Guid> SideBDeviceIds { get; }

        public bool IsBridge =>
            SideADeviceIds.Count > 0;

        public long SeparatedDevicePairCount =>
            (long)SideADeviceIds.Count *
            SideBDeviceIds.Count;
    }
}
