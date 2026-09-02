using System;
using NetLoom.Domain.Access;

namespace NetLoom.Application.Discovery
{
    public sealed class DiscoveryTarget
    {
        public DiscoveryTarget(
            AccessTargetKind kind,
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Discovery target value is required.",
                    nameof(value));
            }

            Kind = kind;
            Value = value.Trim();
        }

        public AccessTargetKind Kind { get; }

        public string Value { get; }
    }
}
