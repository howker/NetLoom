using System;
using NetLoom.Application.DiscoveryControl;

namespace NetLoom.Application.DiscoveryInbox
{
    public sealed class DiscoveryRunRecord
    {
        public DiscoveryRunRecord(
            Guid id,
            DateTime startedUtc,
            DateTime? finishedUtc,
            DiscoveryControlState state,
            Guid? accessProfileId,
            string accessProfileName,
            string scopeText,
            int totalAddresses,
            int processedAddresses,
            int foundCandidates,
            int snmpResponded,
            int errorCount,
            int knownUnchangedCount,
            string faultMessage)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException(
                    "Run id is required.",
                    nameof(id));
            }

            if (startedUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Timestamp must be UTC.",
                    nameof(startedUtc));
            }

            if (finishedUtc.HasValue &&
                finishedUtc.Value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Timestamp must be UTC.",
                    nameof(finishedUtc));
            }

            if (!Enum.IsDefined(typeof(DiscoveryControlState), state))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(state));
            }

            if (accessProfileId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Access profile id must not be empty.",
                    nameof(accessProfileId));
            }

            if (string.IsNullOrWhiteSpace(scopeText))
            {
                throw new ArgumentException(
                    "Scope is required.",
                    nameof(scopeText));
            }

            RequireNonNegative(totalAddresses, nameof(totalAddresses));
            RequireNonNegative(processedAddresses, nameof(processedAddresses));
            RequireNonNegative(foundCandidates, nameof(foundCandidates));
            RequireNonNegative(snmpResponded, nameof(snmpResponded));
            RequireNonNegative(errorCount, nameof(errorCount));
            RequireNonNegative(knownUnchangedCount, nameof(knownUnchangedCount));

            Id = id;
            StartedUtc = startedUtc;
            FinishedUtc = finishedUtc;
            State = state;
            AccessProfileId = accessProfileId;
            AccessProfileName = accessProfileName;
            ScopeText = scopeText;
            TotalAddresses = totalAddresses;
            ProcessedAddresses = processedAddresses;
            FoundCandidates = foundCandidates;
            SnmpResponded = snmpResponded;
            ErrorCount = errorCount;
            KnownUnchangedCount = knownUnchangedCount;
            FaultMessage = faultMessage;
        }

        public Guid Id { get; }

        public DateTime StartedUtc { get; }

        public DateTime? FinishedUtc { get; }

        public DiscoveryControlState State { get; }

        public Guid? AccessProfileId { get; }

        public string AccessProfileName { get; }

        public string ScopeText { get; }

        public int TotalAddresses { get; }

        public int ProcessedAddresses { get; }

        public int FoundCandidates { get; }

        public int SnmpResponded { get; }

        public int ErrorCount { get; }

        public int KnownUnchangedCount { get; }

        public string FaultMessage { get; }

        private static void RequireNonNegative(
            int value,
            string parameterName)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName);
            }
        }
    }
}
