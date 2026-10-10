using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Application.Monitoring;

namespace NetLoom.Application.PollingPolicies
{
    public enum PollingScheduleMode { General = 0, Interval = 1, Once = 2, Off = 3 }

    public sealed class PollingSchedule : IEquatable<PollingSchedule>
    {
        public PollingScheduleMode Mode { get; }
        public int? IntervalSeconds { get; }

        private PollingSchedule(PollingScheduleMode mode, int? seconds)
        {
            if (mode == PollingScheduleMode.Interval && (!seconds.HasValue || seconds < 10 || seconds > 604800))
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (mode != PollingScheduleMode.Interval && seconds.HasValue)
                throw new ArgumentException("Only interval schedules have seconds.", nameof(seconds));
            Mode = mode;
            IntervalSeconds = seconds;
        }

        public static PollingSchedule General { get; } = new PollingSchedule(PollingScheduleMode.General, null);
        public static PollingSchedule Once { get; } = new PollingSchedule(PollingScheduleMode.Once, null);
        public static PollingSchedule Off { get; } = new PollingSchedule(PollingScheduleMode.Off, null);
        public static PollingSchedule Every(int seconds) => new PollingSchedule(PollingScheduleMode.Interval, seconds);
        public bool Equals(PollingSchedule other) => other != null && Mode == other.Mode && IntervalSeconds == other.IntervalSeconds;
        public override bool Equals(object obj) => Equals(obj as PollingSchedule);
        public override int GetHashCode() => ((int)Mode * 397) ^ IntervalSeconds.GetHashCode();
    }

    public sealed class PollingPolicy
    {
        public static readonly Guid DefaultPolicyId = new Guid("5d0f5051-0000-4000-8000-000000000051");
        public static IReadOnlyList<MonitoringPollKind> StateKinds { get; } = Array.AsReadOnly(new[] { MonitoringPollKind.Health, MonitoringPollKind.Interface });
        public static IReadOnlyList<MonitoringPollKind> TopologyKinds { get; } = Array.AsReadOnly(new[] { MonitoringPollKind.Lldp, MonitoringPollKind.Cdp, MonitoringPollKind.Fdb, MonitoringPollKind.Arp, MonitoringPollKind.Stp });

        public Guid Id { get; }
        public string Name { get; }
        public bool IsDefault { get; }
        public bool ActivePolling { get; }
        public PollingSchedule StateSchedule { get; }
        public PollingSchedule TopologySchedule { get; }
        public IReadOnlyList<int> TcpPorts { get; }

        public PollingPolicy(Guid id, string name, bool isDefault, bool activePolling,
            PollingSchedule stateSchedule, PollingSchedule topologySchedule, IEnumerable<int> tcpPorts)
        {
            if (id == Guid.Empty) throw new ArgumentException("Policy id is required.", nameof(id));
            if (name == null) throw new ArgumentNullException(nameof(name));
            name = name.Trim();
            if (name.Length == 0 || name.Length > 100) throw new ArgumentException("Invalid policy name.", nameof(name));
            StateSchedule = stateSchedule ?? throw new ArgumentNullException(nameof(stateSchedule));
            TopologySchedule = topologySchedule ?? throw new ArgumentNullException(nameof(topologySchedule));
            if (activePolling && stateSchedule.Mode == PollingScheduleMode.Off && topologySchedule.Mode == PollingScheduleMode.Off)
                throw new ArgumentException("POLLING_POLICY_NOTHING_TO_POLL");
            if (tcpPorts == null) throw new ArgumentNullException(nameof(tcpPorts));
            var ports = tcpPorts.ToArray();
            if (ports.Any(port => port < 1 || port > 65535)) throw new ArgumentOutOfRangeException(nameof(tcpPorts));
            Id = id;
            Name = name;
            IsDefault = isDefault;
            ActivePolling = activePolling;
            TcpPorts = Array.AsReadOnly(ports.Distinct().OrderBy(port => port).ToArray());
        }

        public static PollingPolicy CreateDefault()
        {
            // Политика по умолчанию в точности повторяет поведение до Sprint 51: общий интервал «Настроек» для всех видов опроса, TCP 22/80/443.
            return new PollingPolicy(DefaultPolicyId, "Default", true, true, PollingSchedule.General, PollingSchedule.General, new[] { 22, 80, 443 });
        }
    }

    public enum PollingPolicySubjectKind { Device = 0, Location = 1 }

    public sealed class PollingPolicyAssignment
    {
        public PollingPolicySubjectKind SubjectKind { get; }
        public Guid SubjectId { get; }
        public Guid PolicyId { get; }
        public PollingPolicyAssignment(PollingPolicySubjectKind subjectKind, Guid subjectId, Guid policyId)
        {
            if (subjectId == Guid.Empty) throw new ArgumentException("Subject id is required.", nameof(subjectId));
            if (policyId == Guid.Empty) throw new ArgumentException("Policy id is required.", nameof(policyId));
            SubjectKind = subjectKind;
            SubjectId = subjectId;
            PolicyId = policyId;
        }
    }

    public enum PollingPolicySource { Default = 0, Device = 1, Location = 2 }

    public sealed class EffectivePollingPolicy
    {
        public PollingPolicy Policy { get; }
        public PollingPolicySource Source { get; }
        public Guid? SourceLocationId { get; }
        public EffectivePollingPolicy(PollingPolicy policy, PollingPolicySource source, Guid? sourceLocationId)
        {
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            if (source == PollingPolicySource.Location && (!sourceLocationId.HasValue || sourceLocationId == Guid.Empty))
                throw new ArgumentException("Location source id is required.", nameof(sourceLocationId));
            if (source != PollingPolicySource.Location && sourceLocationId.HasValue)
                throw new ArgumentException("Location source id is not applicable.", nameof(sourceLocationId));
            Source = source;
            SourceLocationId = sourceLocationId;
        }
    }

    public enum PollingPolicyDeleteOutcome { Deleted, NotFound, DefaultPolicy, InUse }

    public sealed class PollingPolicyUsage
    {
        public int DeviceCount { get; }
        public int LocationCount { get; }
        public bool IsUsed => DeviceCount != 0 || LocationCount != 0;
        public PollingPolicyUsage(int deviceCount, int locationCount)
        {
            if (deviceCount < 0) throw new ArgumentOutOfRangeException(nameof(deviceCount));
            if (locationCount < 0) throw new ArgumentOutOfRangeException(nameof(locationCount));
            DeviceCount = deviceCount;
            LocationCount = locationCount;
        }
    }
}
