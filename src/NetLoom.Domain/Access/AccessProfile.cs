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
            string snmpUsername,
            string snmpAuthenticationProtocol = null,
            string snmpPrivacyProtocol = null,
            int? snmpTimeoutMilliseconds = null,
            int? snmpRetryCount = null)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Access profile id is required.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Access profile name is required.", nameof(name));
            }

            if (snmpTimeoutMilliseconds.HasValue && snmpTimeoutMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(snmpTimeoutMilliseconds));
            if (snmpRetryCount.HasValue && snmpRetryCount < 0)
                throw new ArgumentOutOfRangeException(nameof(snmpRetryCount));

            Id = id;
            Name = name;
            IsEnabled = isEnabled;
            SnmpVersion = snmpVersion;
            SnmpUsername = snmpUsername;
            SnmpAuthenticationProtocol = snmpAuthenticationProtocol;
            SnmpPrivacyProtocol = snmpPrivacyProtocol;
            SnmpTimeoutMilliseconds = snmpTimeoutMilliseconds;
            SnmpRetryCount = snmpRetryCount;
        }

        public Guid Id { get; }

        public string Name { get; }

        public bool IsEnabled { get; }

        public SnmpVersion SnmpVersion { get; }

        public string SnmpUsername { get; }
        public string SnmpAuthenticationProtocol { get; }
        public string SnmpPrivacyProtocol { get; }
        public int? SnmpTimeoutMilliseconds { get; }
        public int? SnmpRetryCount { get; }
    }
}
