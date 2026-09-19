using System;
using System.Collections.Generic;
using System.Globalization;

namespace NetLoom.Desktop.Monitoring
{
    internal enum EngineMachineMarkerKind
    {
        None = 0,
        ControlReady = 1,
        ScheduleStarted = 2,
        ScheduleStopped = 3,
        PollStarted = 4,
        PollCompleted = 5
    }

    internal sealed class EngineMachineMarker
    {
        public EngineMachineMarker(
            EngineMachineMarkerKind kind,
            DateTime? completedUtc = null,
            bool? anySucceeded = null)
        {
            Kind = kind;
            CompletedUtc = completedUtc;
            AnySucceeded = anySucceeded;
        }

        public EngineMachineMarkerKind Kind { get; }

        public DateTime? CompletedUtc { get; }

        public bool? AnySucceeded { get; }
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
                            EngineMachineMarkerKind.ScheduleStarted);
                    return true;
                }

                if (string.Equals(
                    state,
                    "stopped",
                    StringComparison.Ordinal))
                {
                    marker =
                        new EngineMachineMarker(
                            EngineMachineMarkerKind.ScheduleStopped);
                    return true;
                }

                return false;
            }

            if (!string.Equals(
                parts[0],
                "NETLOOM_POLL",
                StringComparison.Ordinal))
            {
                return false;
            }

            string pollState;

            if (!values.TryGetValue(
                "state",
                out pollState))
            {
                return false;
            }

            if (string.Equals(
                pollState,
                "started",
                StringComparison.Ordinal))
            {
                marker =
                    new EngineMachineMarker(
                        EngineMachineMarkerKind.PollStarted);
                return true;
            }

            if (!string.Equals(
                pollState,
                "completed",
                StringComparison.Ordinal))
            {
                return false;
            }

            string completedText;
            string successText;
            string failedText;
            string anySucceededText;
            DateTime completedUtc;
            int success;
            int failed;
            bool anySucceeded;

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

            marker =
                new EngineMachineMarker(
                    EngineMachineMarkerKind.PollCompleted,
                    completedUtc,
                    anySucceeded);

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
