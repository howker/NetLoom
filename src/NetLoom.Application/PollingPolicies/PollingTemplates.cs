using System;
using System.Collections.Generic;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;

namespace NetLoom.Application.PollingPolicies
{
    public enum SnmpSecurityLevel { NoAuthNoPriv, AuthNoPriv, AuthPriv }

    public sealed class AccessProfileTemplate
    {
        public string Key { get; }
        public SnmpVersion Version { get; }
        public SnmpSecurityLevel? SecurityLevel { get; }
        public SnmpAuthenticationProtocol AuthenticationProtocol { get; }
        public SnmpPrivacyProtocol PrivacyProtocol { get; }
        public int TimeoutMilliseconds { get; }
        public int RetryCount { get; }

        public AccessProfileTemplate(string key, SnmpVersion version, SnmpSecurityLevel? securityLevel,
            SnmpAuthenticationProtocol authenticationProtocol, SnmpPrivacyProtocol privacyProtocol,
            int timeoutMilliseconds, int retryCount)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException(nameof(key));
            if ((version == SnmpVersion.V3) != securityLevel.HasValue) throw new ArgumentException(nameof(securityLevel));
            if (timeoutMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));
            if (retryCount < 0) throw new ArgumentOutOfRangeException(nameof(retryCount));
            Key = key;
            Version = version;
            SecurityLevel = securityLevel;
            AuthenticationProtocol = authenticationProtocol;
            PrivacyProtocol = privacyProtocol;
            TimeoutMilliseconds = timeoutMilliseconds;
            RetryCount = retryCount;
        }
    }

    public sealed class PollingPolicyTemplate
    {
        public string Key { get; }
        public bool ActivePolling { get; }
        public PollingSchedule StateSchedule { get; }
        public PollingSchedule TopologySchedule { get; }
        public IReadOnlyList<int> TcpPorts { get; }

        public PollingPolicyTemplate(string key, bool activePolling, PollingSchedule stateSchedule,
            PollingSchedule topologySchedule, IEnumerable<int> tcpPorts)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException(nameof(key));
            Key = key;
            ActivePolling = activePolling;
            StateSchedule = stateSchedule ?? throw new ArgumentNullException(nameof(stateSchedule));
            TopologySchedule = topologySchedule ?? throw new ArgumentNullException(nameof(topologySchedule));
            TcpPorts = Array.AsReadOnly(new List<int>(tcpPorts ?? throw new ArgumentNullException(nameof(tcpPorts))).ToArray());
        }

        public PollingPolicy CreatePolicy(Guid id, string name) =>
            new PollingPolicy(id, name, false, ActivePolling, StateSchedule, TopologySchedule, TcpPorts);
    }

    public static class PollingTemplates
    {
        public static AccessProfileTemplate SecureSnmpV3 { get; } = new AccessProfileTemplate(
            "secure-snmpv3", SnmpVersion.V3, SnmpSecurityLevel.AuthPriv,
            SnmpAuthenticationProtocol.Sha256, SnmpPrivacyProtocol.Aes, 3000, 1);
        public static AccessProfileTemplate IndustrialV2c { get; } = new AccessProfileTemplate(
            "industrial-v2c", SnmpVersion.V2C, null,
            SnmpAuthenticationProtocol.None, SnmpPrivacyProtocol.None, 5000, 2);
        public static PollingPolicyTemplate OneTimeAudit { get; } = new PollingPolicyTemplate(
            "one-time-audit", true, PollingSchedule.Once, PollingSchedule.Once, new[] { 22, 23, 80, 443 });
        public static PollingPolicyTemplate ScheduledTopology { get; } = new PollingPolicyTemplate(
            "scheduled-topology", true, PollingSchedule.General, PollingSchedule.Every(3600), new[] { 22, 80, 443 });
        public static PollingPolicyTemplate NoActivePolling { get; } = new PollingPolicyTemplate(
            "no-active-polling", false, PollingSchedule.Off, PollingSchedule.Off, Array.Empty<int>());

        public static IReadOnlyList<AccessProfileTemplate> AccessProfileTemplates { get; } =
            Array.AsReadOnly(new[] { SecureSnmpV3, IndustrialV2c });
        public static IReadOnlyList<PollingPolicyTemplate> PollingPolicyTemplates { get; } =
            Array.AsReadOnly(new[] { OneTimeAudit, ScheduledTopology, NoActivePolling });
    }
}
