using System;

namespace NetLoom.Domain.Access
{
    public sealed class AccessProfile
    {
        public AccessProfile(
            Guid id,
            string name,
            bool isEnabled,
            SnmpVersion snmpVersion,
            string snmpUsername)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Access profile id is required.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Access profile name is required.", nameof(name));
            }

            Id = id;
            Name = name;
            IsEnabled = isEnabled;
            SnmpVersion = snmpVersion;
            SnmpUsername = snmpUsername;
        }

        public Guid Id { get; }

        public string Name { get; }

        public bool IsEnabled { get; }

        public SnmpVersion SnmpVersion { get; }

        public string SnmpUsername { get; }
    }
}
