using System;
using System.Globalization;
using System.IO;

namespace NetLoom.HostLogging
{
    public sealed class HostLogSettings
    {
        public const long DefaultMaxFileBytes =
            5L * 1024L * 1024L;

        public const int DefaultMaxArchiveFiles = 5;

        public HostLogSettings(
            string logDirectory,
            HostLogLevel minimumLevel,
            long maxFileBytes,
            int maxArchiveFiles)
        {
            if (string.IsNullOrWhiteSpace(
                logDirectory))
            {
                throw new ArgumentException(
                    "Log directory is required.",
                    nameof(logDirectory));
            }

            if (maxFileBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxFileBytes));
            }

            if (maxArchiveFiles < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxArchiveFiles));
            }

            LogDirectory =
                Path.GetFullPath(
                    logDirectory);

            MinimumLevel = minimumLevel;
            MaxFileBytes = maxFileBytes;
            MaxArchiveFiles = maxArchiveFiles;
        }

        public string LogDirectory { get; }

        public HostLogLevel MinimumLevel { get; }

        public long MaxFileBytes { get; }

        public int MaxArchiveFiles { get; }

        public static HostLogSettings FromEnvironment()
        {
            var directory =
                Environment.GetEnvironmentVariable(
                    "NETLOOM_LOG_DIRECTORY");

            if (string.IsNullOrWhiteSpace(
                directory))
            {
                directory =
                    HostLogPathResolver
                        .ResolveDefault();
            }

            return new HostLogSettings(
                directory,
                ParseLevel(
                    Environment.GetEnvironmentVariable(
                        "NETLOOM_LOG_LEVEL")),
                ParsePositiveLong(
                    "NETLOOM_LOG_MAX_BYTES",
                    DefaultMaxFileBytes),
                ParseNonNegativeInt(
                    "NETLOOM_LOG_MAX_ARCHIVES",
                    DefaultMaxArchiveFiles));
        }

        private static HostLogLevel ParseLevel(
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                value))
            {
                return HostLogLevel.Info;
            }

            switch (value.Trim().ToLowerInvariant())
            {
                case "trace":
                    return HostLogLevel.Trace;

                case "debug":
                    return HostLogLevel.Debug;

                case "info":
                case "information":
                    return HostLogLevel.Info;

                case "warn":
                case "warning":
                    return HostLogLevel.Warning;

                case "error":
                    return HostLogLevel.Error;

                case "fatal":
                    return HostLogLevel.Fatal;

                default:
                    throw new ArgumentException(
                        "NETLOOM_LOG_LEVEL is invalid.");
            }
        }

        private static long ParsePositiveLong(
            string variableName,
            long defaultValue)
        {
            var value =
                Environment.GetEnvironmentVariable(
                    variableName);

            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            long parsed;

            if (!long.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out parsed) ||
                parsed <= 0)
            {
                throw new ArgumentException(
                    variableName +
                    " must be a positive integer.");
            }

            return parsed;
        }

        private static int ParseNonNegativeInt(
            string variableName,
            int defaultValue)
        {
            var value =
                Environment.GetEnvironmentVariable(
                    variableName);

            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            int parsed;

            if (!int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out parsed) ||
                parsed < 0)
            {
                throw new ArgumentException(
                    variableName +
                    " must be a non-negative integer.");
            }

            return parsed;
        }
    }
}
