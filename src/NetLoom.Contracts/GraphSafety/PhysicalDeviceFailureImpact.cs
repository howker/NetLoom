using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Contracts.GraphSafety
{
    public sealed class PhysicalDeviceFailureImpact
    {
        public PhysicalDeviceFailureImpact(
            Guid deviceId,
            IEnumerable<IEnumerable<Guid>> partDeviceIds)
        {
            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException("Device id is required.", nameof(deviceId));
            }

            if (partDeviceIds == null)
            {
                throw new ArgumentNullException(nameof(partDeviceIds));
            }

            var parts = partDeviceIds
                .Select(part => part == null
                    ? throw new ArgumentException("Parts cannot contain null.", nameof(partDeviceIds))
                    : part.OrderBy(id => id).ToArray())
                .ToArray();

            var all = parts.SelectMany(part => part).ToArray();
            if (parts.Any(part => part.Length == 0) ||
                all.Any(id => id == Guid.Empty || id == deviceId) ||
                all.Distinct().Count() != all.Length)
            {
                throw new ArgumentException("Failure parts must contain distinct devices other than the failed device.", nameof(partDeviceIds));
            }

            DeviceId = deviceId;
            PartDeviceIds = parts
                .OrderByDescending(part => part.Length)
                .ThenBy(part => part[0])
                .Select(part => (IReadOnlyList<Guid>)Array.AsReadOnly(part))
                .ToArray();

            long pairs = 0;
            long preceding = 0;
            foreach (var part in PartDeviceIds)
            {
                pairs += preceding * part.Count;
                preceding += part.Count;
            }
            SeparatedDevicePairCount = pairs;
        }

        public Guid DeviceId { get; }
        public IReadOnlyList<IReadOnlyList<Guid>> PartDeviceIds { get; }
        public bool IsArticulationPoint => PartDeviceIds.Count >= 2;
        public long SeparatedDevicePairCount { get; }
    }
}
