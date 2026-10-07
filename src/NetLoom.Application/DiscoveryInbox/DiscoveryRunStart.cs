using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Application.DiscoveryInbox
{
    public sealed class DiscoveryRunStart
    {
        public DiscoveryRunStart(
            Guid runId,
            IReadOnlyList<string> excludedAddresses)
        {
            if (runId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Run id is required.",
                    nameof(runId));
            }

            if (excludedAddresses == null)
            {
                throw new ArgumentNullException(
                    nameof(excludedAddresses));
            }

            var addresses = excludedAddresses.ToArray();

            if (addresses.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException(
                    "Excluded addresses must not be empty.",
                    nameof(excludedAddresses));
            }

            RunId = runId;
            ExcludedAddresses = Array.AsReadOnly(addresses);
        }

        public Guid RunId { get; }

        public IReadOnlyList<string> ExcludedAddresses { get; }
    }
}
