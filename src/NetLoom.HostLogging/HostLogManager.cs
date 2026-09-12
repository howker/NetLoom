using System;
using System.IO;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace NetLoom.HostLogging
{
    public sealed class HostLogManager : IDisposable
    {
        private readonly LogFactory _logFactory;
        private readonly Logger _logger;
        private bool _disposed;

        private HostLogManager(
            string logFilePath,
            LogFactory logFactory,
            Logger logger)
        {
            LogFilePath = logFilePath;
            _logFactory = logFactory;
            _logger = logger;
        }

        public string LogFilePath { get; }

        public static HostLogManager Create(
            string hostName)
        {
            return Create(
                hostName,
                HostLogSettings.FromEnvironment());
        }

        public static HostLogManager Create(
            string hostName,
            HostLogSettings settings)
        {
            if (string.IsNullOrWhiteSpace(
                hostName))
            {
                throw new ArgumentException(
                    "Host name is required.",
                    nameof(hostName));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(
                    nameof(settings));
            }

            var normalizedHostName =
                NormalizeHostName(
                    hostName);

            Directory.CreateDirectory(
                settings.LogDirectory);

            var logFilePath =
                Path.Combine(
                    settings.LogDirectory,
                    normalizedHostName +
                    ".log");

            var logFactory =
                new LogFactory();

            var configuration =
                new LoggingConfiguration(
                    logFactory);

            var fileTarget =
                new FileTarget(
                    "host-file")
                {
                    FileName =
                        logFilePath,
                    Layout =
                        "${longdate}|${level:uppercase=true}|${message}|${exception:format=tostring}",
                    ArchiveAboveSize =
                        settings.MaxFileBytes,
                    MaxArchiveFiles =
                        settings.MaxArchiveFiles,
                    AutoFlush = true,
                    CreateDirs = true
                };

            configuration.AddRule(
                ToNLogLevel(
                    settings.MinimumLevel),
                LogLevel.Fatal,
                fileTarget);

            logFactory.Configuration =
                configuration;

            var logger =
                logFactory.GetLogger(
                    "NetLoom." +
                    normalizedHostName);

            return new HostLogManager(
                logFilePath,
                logFactory,
                logger);
        }

        public void Info(
            string message)
        {
            ThrowIfDisposed();

            _logger.Info(
                message ?? string.Empty);
        }

        public void Warning(
            string message)
        {
            ThrowIfDisposed();

            _logger.Warn(
                message ?? string.Empty);
        }

        public void Error(
            string message)
        {
            ThrowIfDisposed();

            _logger.Error(
                message ?? string.Empty);
        }

        public void Error(
            Exception exception,
            string message)
        {
            ThrowIfDisposed();

            if (exception == null)
            {
                throw new ArgumentNullException(
                    nameof(exception));
            }

            _logger.Error(
                exception,
                message ?? string.Empty);
        }

        public void Flush()
        {
            ThrowIfDisposed();

            _logFactory.Flush();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _logFactory.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    nameof(HostLogManager));
            }
        }

        private static string NormalizeHostName(
            string hostName)
        {
            var value =
                hostName.Trim()
                    .ToLowerInvariant();

            if (value.IndexOfAny(
                    Path.GetInvalidFileNameChars()) >= 0 ||
                value.IndexOf(
                    Path.DirectorySeparatorChar) >= 0 ||
                value.IndexOf(
                    Path.AltDirectorySeparatorChar) >= 0)
            {
                throw new ArgumentException(
                    "Host name contains invalid file-name characters.",
                    nameof(hostName));
            }

            return value;
        }

        private static LogLevel ToNLogLevel(
            HostLogLevel level)
        {
            switch (level)
            {
                case HostLogLevel.Trace:
                    return LogLevel.Trace;

                case HostLogLevel.Debug:
                    return LogLevel.Debug;

                case HostLogLevel.Info:
                    return LogLevel.Info;

                case HostLogLevel.Warning:
                    return LogLevel.Warn;

                case HostLogLevel.Error:
                    return LogLevel.Error;

                case HostLogLevel.Fatal:
                    return LogLevel.Fatal;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(level));
            }
        }
    }
}
