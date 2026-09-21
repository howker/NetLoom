using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;

namespace NetLoom.Desktop.Monitoring
{
    internal enum EngineMachineMarkerKind
    {
        None = 0,
        ControlReady = 1,
        ScheduleStarted = 2,
        ScheduleStopped = 3,
        PollStarted = 4,
        PollCompleted = 5,
        ScheduleSetStarted = 6,
        ScheduleSetStopped = 7,
        TargetPollStarted = 8,
        TargetPollCompleted = 9,
        TargetPollSkipped = 10
    }

    internal sealed class EngineMachineMarker
    {
        public EngineMachineMarker(
            EngineMachineMarkerKind kind,
            DateTime? completedUtc = null,
            bool? anySucceeded = null,
            Guid? deviceId = null,
            IPAddress targetAddress = null)
        {
            Kind = kind;
            CompletedUtc = completedUtc;
            AnySucceeded = anySucceeded;
            DeviceId = deviceId;
            TargetAddress = targetAddress;
        }

        public EngineMachineMarkerKind Kind { get; }

        public DateTime? CompletedUtc { get; }

        public bool? AnySucceeded { get; }

        public Guid? DeviceId { get; }

        public IPAddress TargetAddress { get; }
    }

    internal static class EngineMachineMarkerParser
    {
        public static bool TryParse(
            string line,
            out EngineMachineMarker marker)
        {
            marker = null;

            if (string.Equals(
                line,
                "NETLOOM_CONTROL state=ready",
                StringComparison.Ordinal))
            {
                marker =
                    new EngineMachineMarker(
                        EngineMachineMarkerKind.ControlReady);
                return true;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            var parts =
                line.Split(
                    new[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                return false;
            }

            var values =
                ParseValues(
                    parts);

            if (string.Equals(
                parts[0],
                "NETLOOM_SCHEDULE",
                StringComparison.Ordinal))
            {
                return TryParseSchedule(
                    values,
                    EngineMachineMarkerKind.ScheduleStarted,
                    EngineMachineMarkerKind.ScheduleStopped,
                    out marker);
            }

            if (string.Equals(
                parts[0],
                "NETLOOM_SCHEDULE_SET",
                StringComparison.Ordinal))
            {
                return TryParseSchedule(
                    values,
                    EngineMachineMarkerKind.ScheduleSetStarted,
                    EngineMachineMarkerKind.ScheduleSetStopped,
                    out marker);
            }

            if (string.Equals(
                parts[0],
                "NETLOOM_POLL",
                StringComparison.Ordinal))
            {
                return TryParsePoll(
                    values,
                    false,
                    out marker);
            }

            if (string.Equals(
                parts[0],
                "NETLOOM_TARGET_POLL",
                StringComparison.Ordinal))
            {
                return TryParsePoll(
                    values,
                    true,
                    out marker);
            }

            return false;
        }

        private static bool TryParseSchedule(
            IReadOnlyDictionary<string, string> values,
            EngineMachineMarkerKind startedKind,
            EngineMachineMarkerKind stoppedKind,
            out EngineMachineMarker marker)
        {
            marker = null;

            string state;

            if (!values.TryGetValue(
                "state",
                out state))
            {
                return false;
            }

            if (string.Equals(
                state,
                "started",
                StringComparison.Ordinal))
            {
                marker =
                    new EngineMachineMarker(
                        startedKind);
                return true;
            }

            if (string.Equals(
                state,
                "stopped",
                StringComparison.Ordinal))
            {
                marker =
                    new EngineMachineMarker(
                        stoppedKind);
                return true;
            }

            return false;
        }

        private static bool TryParsePoll(
            IReadOnlyDictionary<string, string> values,
            bool targetSpecific,
            out EngineMachineMarker marker)
        {
            marker = null;

            string state;

            if (!values.TryGetValue(
                "state",
                out state))
            {
                return false;
            }

            Guid? deviceId = null;
            IPAddress targetAddress = null;

            if (targetSpecific &&
                !TryParseTargetIdentity(
                    values,
                    out deviceId,
                    out targetAddress))
            {
                return false;
            }

            if (string.Equals(
                state,
                "started",
                StringComparison.Ordinal))
            {
                marker =
                    new EngineMachineMarker(
                        targetSpecific
                            ? EngineMachineMarkerKind.TargetPollStarted
                            : EngineMachineMarkerKind.PollStarted,
                        null,
                        null,
                        deviceId,
                        targetAddress);

                return true;
            }

            if (targetSpecific &&
                string.Equals(
                    state,
                    "skipped",
                    StringComparison.Ordinal))
            {
                string reason;

                if (!values.TryGetValue(
                        "reason",
                        out reason) ||
                    !string.Equals(
                        reason,
                        "backpressure",
                        StringComparison.Ordinal))
                {
                    return false;
                }

                marker =
                    new EngineMachineMarker(
                        EngineMachineMarkerKind.TargetPollSkipped,
                        null,
                        null,
                        deviceId,
                        targetAddress);

                return true;
            }

            if (!string.Equals(
                state,
                "completed",
                StringComparison.Ordinal))
            {
                return false;
            }

            DateTime completedUtc;
            bool anySucceeded;

            if (!TryParseCompletion(
                    values,
                    out completedUtc,
                    out anySucceeded))
            {
                return false;
            }

            marker =
                new EngineMachineMarker(
                    targetSpecific
                        ? EngineMachineMarkerKind.TargetPollCompleted
                        : EngineMachineMarkerKind.PollCompleted,
                    completedUtc,
                    anySucceeded,
                    deviceId,
                    targetAddress);

            return true;
        }

        private static bool TryParseTargetIdentity(
            IReadOnlyDictionary<string, string> values,
            out Guid? deviceId,
            out IPAddress targetAddress)
        {
            deviceId = null;
            targetAddress = null;

            string deviceIdText;
            string addressText;
            Guid parsedDeviceId;
            IPAddress parsedAddress;

            if (!values.TryGetValue(
                    "deviceId",
                    out deviceIdText) ||
                !Guid.TryParse(
                    deviceIdText,
                    out parsedDeviceId) ||
                parsedDeviceId == Guid.Empty ||
                !values.TryGetValue(
                    "address",
                    out addressText) ||
                !IPAddress.TryParse(
                    addressText,
                    out parsedAddress))
            {
                return false;
            }

            deviceId = parsedDeviceId;
            targetAddress = parsedAddress;
            return true;
        }

        private static bool TryParseCompletion(
            IReadOnlyDictionary<string, string> values,
            out DateTime completedUtc,
            out bool anySucceeded)
        {
            completedUtc = default(DateTime);
            anySucceeded = false;

            string completedText;
            string successText;
            string failedText;
            string anySucceededText;
            int success;
            int failed;

            if (!values.TryGetValue(
                    "completedUtc",
                    out completedText) ||
                !DateTime.TryParseExact(
                    completedText,
                    "o",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out completedUtc) ||
                completedUtc.Kind != DateTimeKind.Utc ||
                !values.TryGetValue(
                    "success",
                    out successText) ||
                !int.TryParse(
                    successText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out success) ||
                success < 0 ||
                !values.TryGetValue(
                    "failed",
                    out failedText) ||
                !int.TryParse(
                    failedText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out failed) ||
                failed < 0 ||
                !values.TryGetValue(
                    "anySucceeded",
                    out anySucceededText) ||
                !bool.TryParse(
                    anySucceededText,
                    out anySucceeded) ||
                anySucceeded != (success > 0))
            {
                return false;
            }

            return true;
        }

        private static Dictionary<string, string> ParseValues(
            IReadOnlyList<string> parts)
        {
            var values =
                new Dictionary<string, string>(
                    StringComparer.Ordinal);

            for (var index = 1;
                 index < parts.Count;
                 index++)
            {
                var part =
                    parts[index];

                var separator =
                    part.IndexOf('=');

                if (separator <= 0 ||
                    separator == part.Length - 1)
                {
                    continue;
                }

                var key =
                    part.Substring(
                        0,
                        separator);

                if (!values.ContainsKey(key))
                {
                    values.Add(
                        key,
                        part.Substring(
                            separator + 1));
                }
            }

            return values;
        }
    }
}
