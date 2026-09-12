using System;
using System.Diagnostics;
using System.Globalization;

namespace NetLoom.HostLogging
{
    public sealed class HostLogTraceListener :
        TraceListener
    {
        private readonly HostLogManager
            _hostLog;

        public HostLogTraceListener(
            HostLogManager hostLog)
        {
            _hostLog =
                hostLog ??
                throw new ArgumentNullException(
                    nameof(hostLog));
        }

        public override void Write(
            string message)
        {
            _hostLog.Info(
                BuildMessage(
                    TraceEventType.Information,
                    null,
                    0,
                    message));
        }

        public override void WriteLine(
            string message)
        {
            Write(
                message);
        }

        public override void TraceEvent(
            TraceEventCache eventCache,
            string source,
            TraceEventType eventType,
            int id,
            string message)
        {
            var text =
                BuildMessage(
                    eventType,
                    source,
                    id,
                    message);

            switch (eventType)
            {
                case TraceEventType.Critical:
                case TraceEventType.Error:
                    _hostLog.Error(
                        text);
                    break;

                case TraceEventType.Warning:
                    _hostLog.Warning(
                        text);
                    break;

                default:
                    _hostLog.Info(
                        text);
                    break;
            }
        }

        public override void TraceEvent(
            TraceEventCache eventCache,
            string source,
            TraceEventType eventType,
            int id,
            string format,
            params object[] args)
        {
            var message =
                args == null ||
                args.Length == 0
                    ? format
                    : string.Format(
                        CultureInfo.InvariantCulture,
                        format,
                        args);

            TraceEvent(
                eventCache,
                source,
                eventType,
                id,
                message);
        }

        private static string BuildMessage(
            TraceEventType eventType,
            string source,
            int id,
            string message)
        {
            var sourceText =
                string.IsNullOrWhiteSpace(
                    source)
                    ? "Trace"
                    : source;

            return
                "TRACE eventType=" +
                eventType +
                " source=" +
                sourceText +
                " id=" +
                id.ToString(
                    CultureInfo.InvariantCulture) +
                " message=" +
                (message ?? string.Empty);
        }
    }
}
