using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using NetLoom.Application.DiscoveryInbox;
using NetLoom.Application.Snmp;
using NetLoom.Wpf.Localization;

namespace NetLoom.Wpf.Discovery
{
    internal static class DiscoveryInboxProjection
    {
        private static readonly DiscoveryResultGroup[] Order =
        {
            DiscoveryResultGroup.New, DiscoveryResultGroup.Changed,
            DiscoveryResultGroup.Ambiguous, DiscoveryResultGroup.Missing,
            DiscoveryResultGroup.Excluded, DiscoveryResultGroup.Error
        };

        internal static IReadOnlyList<DiscoveryInboxGroup> Build(
            IReadOnlyList<DiscoveryRunResult> results, DiscoveryRunRecord run,
            Func<Guid?, string> profileName, DateTime nowUtc)
        {
            return Build(results, run, profileName, nowUtc, id => null);
        }

        internal static IReadOnlyList<DiscoveryInboxGroup> Build(
            IReadOnlyList<DiscoveryRunResult> results, DiscoveryRunRecord run,
            Func<Guid?, string> profileName, DateTime nowUtc,
            Func<Guid?, string> deviceName, Func<Guid?, string> placementPath = null)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (profileName == null) throw new ArgumentNullException(nameof(profileName));
            if (deviceName == null) throw new ArgumentNullException(nameof(deviceName));

            var profile = run.AccessProfileName;
            if (string.IsNullOrWhiteSpace(profile)) profile = profileName(run.AccessProfileId);
            if (string.IsNullOrWhiteSpace(profile)) profile = UiText.Get("DiagnosticNotAvailable");

            return Order.Select(group => new DiscoveryInboxGroup(group,
                results.Where(result => result.Group == group)
                    .OrderBy(result => AddressNumber(result.Address))
                    .ThenBy(result => result.Address, StringComparer.Ordinal)
                    .Select(result => Row(result, profile, nowUtc, deviceName, placementPath ?? (id => null))).ToArray()))
                .Where(group => group.Count > 0).ToArray();
        }

        internal static string BuildTitle(DiscoveryRunRecord run)
        {
            return UiText.Format("DiscoveryInboxTitle",
                run.StartedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture));
        }

        internal static string BuildSummary(
            IReadOnlyList<DiscoveryInboxGroup> groups, DiscoveryRunRecord run)
        {
            var parts = groups.Select(group => UiText.Format(
                "DiscoveryInboxSummary" + group.Group, group.Count)).ToList();
            if (run.KnownUnchangedCount > 0)
            {
                parts.Add(UiText.Format("DiscoveryInboxSummaryUnchanged", run.KnownUnchangedCount));
            }
            return string.Join(UiText.Get("DiscoveryInboxSeparator"), parts);
        }

        private static DiscoveryInboxRow Row(DiscoveryRunResult result, string profile,
            DateTime nowUtc, Func<Guid?, string> deviceName, Func<Guid?, string> placementPath)
        {
            var name = result.SysName;
            if (string.IsNullOrWhiteSpace(name) && result.DeviceId.HasValue)
                name = deviceName(result.DeviceId);
            if (string.IsNullOrWhiteSpace(name)) name = UiText.Get("DiscoveryUnnamedCandidate");
            var reason = Reason(result, profile, nowUtc, false);
            return new DiscoveryInboxRow(result, name, reason,
                Reason(result, profile, nowUtc, true), Completeness(result), Resolution(result, placementPath));
        }

        private static string Resolution(DiscoveryRunResult result, Func<Guid?, string> placementPath)
        {
            switch (result.Resolution)
            {
                case DiscoveryResultResolution.Accepted:
                    return UiText.Format("DiscoveryInboxResolutionAccepted",
                        (result.ResolvedUtc ?? result.ObservedUtc).ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture));
                case DiscoveryResultResolution.Ignored:
                    return UiText.Format("DiscoveryInboxResolutionIgnored",
                        (result.ResolvedUtc ?? result.ObservedUtc).ToLocalTime().ToString("d", CultureInfo.CurrentCulture));
                case DiscoveryResultResolution.Unmanaged:
                    return UiText.Get("DiscoveryInboxResolutionUnmanaged");
                case DiscoveryResultResolution.Placed:
                    return UiText.Format("DiscoveryInboxResolutionPlaced",
                        placementPath(result.DeviceId) ?? UiText.Get("DiagnosticNotAvailable"));
                default:
                    return string.Empty;
            }
        }

        private static string Reason(DiscoveryRunResult result, string profile,
            DateTime nowUtc, bool full)
        {
            switch (result.Group)
            {
                case DiscoveryResultGroup.New:
                    var evidence = new List<string>();
                    if (result.IcmpReachable) evidence.Add(UiText.Get("DiscoveryInboxIcmp"));
                    if (result.OpenTcpPorts.Count > 0)
                    {
                        evidence.Add(UiText.Format("DiscoveryInboxTcp", string.Join(
                            UiText.Get("DiscoveryInboxListSeparator"), result.OpenTcpPorts.OrderBy(port => port))));
                    }
                    if (result.SnmpResponded) evidence.Add(UiText.Get("DiscoveryInboxSnmp"));
                    if (result.InterfaceCount > 0)
                    {
                        evidence.Add(UiText.FormatCount("DiscoveryInboxInterfaces", result.InterfaceCount));
                    }
                    return string.Join(UiText.Get("DiscoveryInboxSeparator"), evidence);
                case DiscoveryResultGroup.Changed:
                    return UiText.Format("DiscoveryInboxChanged", string.Join(
                        UiText.Get("DiscoveryInboxChangeSeparator"), result.Changes.Select(change =>
                        {
                            var key = FieldKey(change.Field);
                            var oldValue = change.OldValue ?? UiText.Get("DiagnosticNotAvailable");
                            var newValue = change.NewValue ?? UiText.Get("DiagnosticNotAvailable");
                            if (!full && change.Field == "sysDescription")
                            {
                                oldValue = ShortDescription(oldValue);
                                newValue = ShortDescription(newValue);
                            }
                            return UiText.Format(change.Field == "interfaces"
                                ? "DiscoveryInboxNumberChange" : "DiscoveryInboxTextChange",
                                UiText.Get(key), oldValue, newValue);
                        })));
                case DiscoveryResultGroup.Ambiguous:
                    return UiText.Format(result.Reason == DiscoveryResultReason.DuplicateManagementAddress
                        ? "DiscoveryInboxDuplicateAddress" : "DiscoveryInboxMatchingName", result.ReasonDetail);
                case DiscoveryResultGroup.Missing:
                    DateTime lastSeen;
                    return DateTime.TryParse(result.ReasonDetail, CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out lastSeen)
                        ? UiText.Format("DiscoveryInboxMissingLastSeen", RelativeTime(lastSeen.ToUniversalTime(), nowUtc))
                        : UiText.Get("DiscoveryInboxMissingNoData");
                case DiscoveryResultGroup.Excluded:
                    if (result.Reason == DiscoveryResultReason.OperatorIgnored)
                    {
                        DateTime ignored;
                        var date = result.ResolvedUtc ??
                            (DateTime.TryParse(result.ReasonDetail, CultureInfo.InvariantCulture,
                                DateTimeStyles.RoundtripKind, out ignored) ? ignored : result.ObservedUtc);
                        return UiText.Format("DiscoveryInboxIgnored",
                            date.ToLocalTime().ToString("d", CultureInfo.CurrentCulture));
                    }
                    var detail = result.ReasonDetail ?? string.Empty;
                    var colon = detail.IndexOf(':');
                    return UiText.Format("DiscoveryInboxProfileExclusion", profile,
                        colon < 0 ? detail : detail.Substring(colon + 1).Trim());
                case DiscoveryResultGroup.Error:
                    return UiText.Format("DiscoveryCandidateErrorSummary",
                        result.SysName, UiText.Get(ErrorKey(result.SnmpError)), profile,
                        result.ObservedUtc.ToLocalTime().ToString("t", CultureInfo.CurrentCulture));
                default:
                    return string.Empty;
            }
        }

        private static string FieldKey(string field)
        {
            switch (field)
            {
                case "sysName": return "DiscoveryInboxFieldName";
                case "sysDescription": return "DiscoveryInboxFieldDescription";
                case "sysObjectId": return "DiscoveryInboxFieldObjectId";
                case "interfaces": return "DiscoveryInboxFieldInterfaces";
                default: throw new ArgumentOutOfRangeException(nameof(field));
            }
        }

        private static string ErrorKey(SnmpTransportFailure? failure)
        {
            switch (failure)
            {
                case SnmpTransportFailure.Authentication: return "DiscoveryErrorSnmpAuthentication";
                case SnmpTransportFailure.Timeout: return "DiscoveryErrorSnmpTimeout";
                case SnmpTransportFailure.Protocol: return "DiscoveryErrorSnmpProtocol";
                case SnmpTransportFailure.Socket: return "DiscoveryErrorSnmpSocket";
                case SnmpTransportFailure.UnsupportedCredentials: return "DiscoveryErrorSnmpUnsupported";
                default: return "DiagnosticNotAvailable";
            }
        }

        private static string Completeness(DiscoveryRunResult result)
        {
            if (result.Group != DiscoveryResultGroup.New && result.Group != DiscoveryResultGroup.Changed)
                return string.Empty;
            if (result.Completeness == DiscoveryResultCompleteness.Ready)
                return UiText.Get("DiscoveryInboxReady");
            if (result.Completeness != DiscoveryResultCompleteness.Partial) return string.Empty;
            switch (result.PartialReason)
            {
                case DiscoveryPartialReason.SnmpNoResponse: return UiText.Get("DiscoveryInboxPartialSnmp");
                case DiscoveryPartialReason.NoSysName: return UiText.Get("DiscoveryInboxPartialName");
                case DiscoveryPartialReason.NoInterfaces: return UiText.Get("DiscoveryInboxPartialInterfaces");
                default: return string.Empty;
            }
        }

        private static string ShortDescription(string value)
        {
            var line = value.Split(new[] { '\r', '\n' }, 2)[0];
            return line.Length > 60 || line.Length != value.Length
                ? line.Substring(0, Math.Min(60, line.Length)) + UiText.Get("DiscoveryInboxEllipsis")
                : line;
        }

        private static string RelativeTime(DateTime valueUtc, DateTime nowUtc)
        {
            var age = nowUtc - valueUtc;
            if (age < TimeSpan.FromMinutes(1)) return UiText.Get("InspectorRelativeNow");
            if (age < TimeSpan.FromHours(1))
                return UiText.FormatCount("InspectorRelativeMinutes", (int)Math.Floor(age.TotalMinutes));
            if (age < TimeSpan.FromDays(1))
                return UiText.FormatCount("InspectorRelativeHours", (int)Math.Floor(age.TotalHours));
            return UiText.FormatCount("InspectorRelativeDays", (int)Math.Floor(age.TotalDays));
        }

        private static ulong AddressNumber(string address)
        {
            IPAddress parsed;
            if (!IPAddress.TryParse(address, out parsed) || parsed.AddressFamily != AddressFamily.InterNetwork)
                return ulong.MaxValue;
            return parsed.GetAddressBytes().Aggregate(0UL, (number, octet) => (number << 8) | octet);
        }
    }

    internal sealed class DiscoveryInboxGroup : INotifyPropertyChanged
    {
        internal DiscoveryInboxGroup(DiscoveryResultGroup group, IReadOnlyList<DiscoveryInboxRow> rows)
        {
            Group = group;
            Rows = rows;
            foreach (var row in rows) row.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(DiscoveryInboxRow.IsSelected))
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAllSelected)));
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public bool CanSelect => Rows.Any(row => row.CanSelect);
        public string SelectAllText => UiText.Get("DiscoveryInboxSelectAll");
        public string SelectAllAutomationName => UiText.Format("DiscoveryInboxSelectAllAutomationName", Title);
        public bool? IsAllSelected
        {
            get
            {
                var selectable = Rows.Where(row => row.CanSelect).ToArray();
                if (selectable.Length == 0 || selectable.All(row => !row.IsSelected)) return false;
                return selectable.All(row => row.IsSelected) ? (bool?)true : null;
            }
            set
            {
                foreach (var row in Rows.Where(row => row.CanSelect)) row.IsSelected = value == true;
            }
        }

        public DiscoveryResultGroup Group { get; }
        public string Title => UiText.Get("DiscoveryInboxGroup" + Group);
        public int Count => Rows.Count;
        public bool IsExpanded { get; set; } = true;
        public string Header => UiText.Format("DiscoveryInboxGroupHeader", Title, Count);
        public IReadOnlyList<DiscoveryInboxRow> Rows { get; }
    }

    internal sealed class DiscoveryInboxRow : INotifyPropertyChanged
    {
        internal DiscoveryInboxRow(DiscoveryRunResult result, string name,
            string reason, string fullReason, string completeness, string resolutionText = "")
        {
            Result = result;
            ResolutionText = resolutionText;
            RunId = result.RunId;
            Address = result.Address;
            DeviceId = result.DeviceId;
            Group = result.Group;
            Name = name;
            Reason = reason;
            FullReason = fullReason;
            Completeness = completeness;
        }

        private bool _isSelected;
        internal DiscoveryRunResult Result { get; }
        public event PropertyChangedEventHandler PropertyChanged;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (!CanSelect || _isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
        public bool CanSelect => Enum.GetValues(typeof(DiscoveryInboxAction)).Cast<DiscoveryInboxAction>()
            .Any(action => DiscoveryInboxActions.CanApply(action, Result));
        public string SelectAutomationName => UiText.Format("DiscoveryInboxSelectAutomationName", Address);
        public string ResolutionText { get; }
        public bool CanUndoIgnore => DeviceId.HasValue &&
            (Result.Resolution == DiscoveryResultResolution.Ignored ||
             (Group == DiscoveryResultGroup.Excluded && Result.Reason == DiscoveryResultReason.OperatorIgnored));
        public bool HasRowAction => CanRetry || CanUndoIgnore;

        internal bool HasSameContent(DiscoveryInboxRow other)
        {
            return RunId == other.RunId && Address == other.Address && Name == other.Name &&
                Reason == other.Reason && FullReason == other.FullReason &&
                Completeness == other.Completeness && DeviceId == other.DeviceId && Group == other.Group &&
                ResolutionText == other.ResolutionText && Result.Resolution == other.Result.Resolution &&
                Result.ResolvedUtc == other.Result.ResolvedUtc && CanUndoIgnore == other.CanUndoIgnore;
        }

        public Guid RunId { get; }
        public string Address { get; }
        public string Name { get; }
        public string Reason { get; }
        public string FullReason { get; }
        public string Completeness { get; }
        public Guid? DeviceId { get; }
        public DiscoveryResultGroup Group { get; }
        public bool CanRetry => Group != DiscoveryResultGroup.Excluded && !CanUndoIgnore;
        public string RetryText => UiText.Get(CanUndoIgnore ? "DiscoveryInboxUndoIgnore" : "DiscoveryInboxRetry");
        public string RetryAutomationName => UiText.Format(CanUndoIgnore ? "DiscoveryInboxUndoIgnoreAutomationName" : "DiscoveryInboxRetryAutomationName", Address);
    }
}
