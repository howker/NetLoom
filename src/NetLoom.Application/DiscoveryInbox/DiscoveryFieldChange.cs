using System;

namespace NetLoom.Application.DiscoveryInbox
{
    public sealed class DiscoveryFieldChange
    {
        public DiscoveryFieldChange(
            string field,
            string oldValue,
            string newValue)
        {
            if (string.IsNullOrWhiteSpace(field))
            {
                throw new ArgumentException(
                    "Field is required.",
                    nameof(field));
            }

            Field = field;
            OldValue = oldValue;
            NewValue = newValue;
        }

        public string Field { get; }

        public string OldValue { get; }

        public string NewValue { get; }
    }
}
