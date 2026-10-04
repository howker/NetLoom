using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Wpf.Shell
{
    public enum UiShellTheme
    {
        Light = 0,
        Dark = 1
    }

    public sealed class UiPollingSettings
    {
        public UiPollingSettings(
            int intervalSeconds,
            int timeoutMilliseconds,
            int retryCount,
            int maxRepetitions,
            bool lldp,
            bool cdp,
            bool fdb,
            bool arp,
            bool health,
            bool interfaces,
            bool stp)
        {
            IntervalSeconds = intervalSeconds;
            TimeoutMilliseconds = timeoutMilliseconds;
            RetryCount = retryCount;
            MaxRepetitions = maxRepetitions;
            Lldp = lldp;
            Cdp = cdp;
            Fdb = fdb;
            Arp = arp;
            Health = health;
            Interfaces = interfaces;
            Stp = stp;
        }

        public int IntervalSeconds { get; }

        public int TimeoutMilliseconds { get; }

        public int RetryCount { get; }

        public int MaxRepetitions { get; }

        public bool Lldp { get; }

        public bool Cdp { get; }

        public bool Fdb { get; }

        public bool Arp { get; }

        public bool Health { get; }

        public bool Interfaces { get; }

        public bool Stp { get; }

        public static UiPollingSettings Default =>
            new UiPollingSettings(
                60,
                2000,
                1,
                25,
                true,
                true,
                true,
                true,
                true,
                true,
                true);
    }

    public sealed class UiShellState
    {
        public UiShellState(
            Guid? accessProfileId,
            UiShellTheme theme)
            : this(
                accessProfileId,
                theme,
                UiPollingSettings.Default,
                MapMotionMode.Normal)
        {
        }

        public UiShellState(
            Guid? accessProfileId,
            UiShellTheme theme,
            UiPollingSettings pollingSettings,
            MapMotionMode motionMode)
        {
            AccessProfileId =
                accessProfileId;
            Theme =
                theme;
            PollingSettings =
                pollingSettings ??
                UiPollingSettings.Default;
            MotionMode =
                motionMode;
        }

        public Guid? AccessProfileId { get; }

        public UiShellTheme Theme { get; }

        public UiPollingSettings PollingSettings { get; }

        public MapMotionMode MotionMode { get; }

        public static UiShellState Default =>
            new UiShellState(
                null,
                UiShellTheme.Light,
                UiPollingSettings.Default,
                MapMotionMode.Normal);
    }

    public interface IUiShellStateStore
    {
        UiShellState Load();

        void Save(
            UiShellState state);
    }

    public sealed class FileUiShellStateStore :
        IUiShellStateStore
    {
        private const string ProfileKey =
            "accessProfileId";
        private const string ThemeKey =
            "theme";
        private const string MotionKey =
            "motion";
        private const string IntervalKey =
            "pollIntervalSeconds";
        private const string TimeoutKey =
            "pollTimeoutMilliseconds";
        private const string RetryKey =
            "pollRetryCount";
        private const string MaxRepetitionsKey =
            "pollMaxRepetitions";
        private const string LldpKey =
            "pollLldp";
        private const string CdpKey =
            "pollCdp";
        private const string FdbKey =
            "pollFdb";
        private const string ArpKey =
            "pollArp";
        private const string HealthKey =
            "pollHealth";
        private const string InterfacesKey =
            "pollInterfaces";
        private const string StpKey =
            "pollStp";

        private readonly string _path;

        public FileUiShellStateStore(
            string path)
        {
            if (string.IsNullOrWhiteSpace(
                    path))
            {
                throw new ArgumentException(
                    "Shell state path is required.",
                    nameof(path));
            }

            _path = path;
        }

        public static FileUiShellStateStore
            CreateDefault()
        {
            var root =
                Environment.GetFolderPath(
                    Environment.SpecialFolder
                        .LocalApplicationData);

            return new FileUiShellStateStore(
                Path.Combine(
                    root,
                    "NetLoom",
                    "ui-shell-state.txt"));
        }

        public UiShellState Load()
        {
            if (!File.Exists(
                    _path))
            {
                return UiShellState.Default;
            }

            try
            {
                Guid? profileId = null;
                var theme =
                    UiShellTheme.Light;
                var motion =
                    MapMotionMode.Normal;
                var polling =
                    UiPollingSettings.Default;

                var intervalSeconds =
                    polling.IntervalSeconds;
                var timeoutMilliseconds =
                    polling.TimeoutMilliseconds;
                var retryCount =
                    polling.RetryCount;
                var maxRepetitions =
                    polling.MaxRepetitions;
                var lldp = polling.Lldp;
                var cdp = polling.Cdp;
                var fdb = polling.Fdb;
                var arp = polling.Arp;
                var health = polling.Health;
                var interfaces = polling.Interfaces;
                var stp = polling.Stp;

                foreach (var rawLine in
                    File.ReadAllLines(
                        _path,
                        Encoding.UTF8))
                {
                    var line =
                        (rawLine ??
                         string.Empty)
                        .Trim();

                    if (line.Length == 0)
                    {
                        continue;
                    }

                    var separator =
                        line.IndexOf('=');

                    if (separator <= 0)
                    {
                        continue;
                    }

                    var key =
                        line.Substring(
                                0,
                                separator)
                            .Trim();

                    var value =
                        line.Substring(
                                separator + 1)
                            .Trim();

                    if (string.Equals(
                            key,
                            ProfileKey,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        Guid parsed;

                        if (Guid.TryParse(
                                value,
                                out parsed) &&
                            parsed != Guid.Empty)
                        {
                            profileId =
                                parsed;
                        }

                        continue;
                    }

                    if (string.Equals(
                            key,
                            ThemeKey,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        UiShellTheme parsedTheme;

                        if (Enum.TryParse(
                                value,
                                true,
                                out parsedTheme) &&
                            Enum.IsDefined(
                                typeof(UiShellTheme),
                                parsedTheme))
                        {
                            theme =
                                parsedTheme;
                        }

                        continue;
                    }

                    if (string.Equals(
                            key,
                            MotionKey,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        MapMotionMode parsedMotion;

                        if (Enum.TryParse(
                                value,
                                true,
                                out parsedMotion) &&
                            Enum.IsDefined(
                                typeof(MapMotionMode),
                                parsedMotion))
                        {
                            motion =
                                parsedMotion;
                        }

                        continue;
                    }

                    if (string.Equals(
                            key,
                            IntervalKey,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        TryReadPositiveInteger(
                            value,
                            ref intervalSeconds);
                        continue;
                    }

                    if (string.Equals(
                            key,
                            TimeoutKey,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        TryReadPositiveInteger(
                            value,
                            ref timeoutMilliseconds);
                        continue;
                    }

                    if (string.Equals(
                            key,
                            RetryKey,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        TryReadNonNegativeInteger(
                            value,
                            ref retryCount);
                        continue;
                    }

                    if (string.Equals(
                            key,
                            MaxRepetitionsKey,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        TryReadPositiveInteger(
                            value,
                            ref maxRepetitions);
                        continue;
                    }

                    if (string.Equals(
                            key,
                            LldpKey,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        TryReadBoolean(
                            value,
                            ref lldp);
                        continue;
                    }

                    if (string.Equals(
                            key,
                            CdpKey,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        TryReadBoolean(
                            value,
                            ref cdp);
                        continue;
                    }

                    if (string.Equals(
                            key,
                            FdbKey,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        TryReadBoolean(
                            value,
                            ref fdb);
                        continue;
                    }

                    if (string.Equals(
                            key,
                            ArpKey,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        TryReadBoolean(
                            value,
                            ref arp);
                        continue;
                    }

                    if (string.Equals(
                            key,
                            HealthKey,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        TryReadBoolean(
                            value,
                            ref health);
                        continue;
                    }

                    if (string.Equals(
                            key,
                            InterfacesKey,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        TryReadBoolean(
                            value,
                            ref interfaces);
                        continue;
                    }

                    if (string.Equals(
                            key,
                            StpKey,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        TryReadBoolean(
                            value,
                            ref stp);
                    }
                }

                return new UiShellState(
                    profileId,
                    theme,
                    new UiPollingSettings(
                        intervalSeconds,
                        timeoutMilliseconds,
                        retryCount,
                        maxRepetitions,
                        lldp,
                        cdp,
                        fdb,
                        arp,
                        health,
                        interfaces,
                        stp),
                    motion);
            }
            catch (IOException error)
            {
                Trace.TraceWarning(
                    "UI_SHELL_STATE_LOAD_FAILED " +
                    error.Message);

                return UiShellState.Default;
            }
            catch (UnauthorizedAccessException error)
            {
                Trace.TraceWarning(
                    "UI_SHELL_STATE_LOAD_FAILED " +
                    error.Message);

                return UiShellState.Default;
            }
        }

        private static void TryReadPositiveInteger(
            string text,
            ref int value)
        {
            int parsed;

            if (int.TryParse(
                    text,
                    out parsed) &&
                parsed > 0)
            {
                value = parsed;
            }
        }

        private static void TryReadNonNegativeInteger(
            string text,
            ref int value)
        {
            int parsed;

            if (int.TryParse(
                    text,
                    out parsed) &&
                parsed >= 0)
            {
                value = parsed;
            }
        }

        private static void TryReadBoolean(
            string text,
            ref bool value)
        {
            bool parsed;

            if (bool.TryParse(
                    text,
                    out parsed))
            {
                value = parsed;
            }
        }

        public void Save(
            UiShellState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(
                    nameof(state));
            }

            try
            {
                var directory =
                    Path.GetDirectoryName(
                        _path);

                if (!string.IsNullOrWhiteSpace(
                        directory))
                {
                    Directory.CreateDirectory(
                        directory);
                }

                var polling =
                    state.PollingSettings ??
                    UiPollingSettings.Default;

                var lines =
                    new List<string>
                    {
                        ThemeKey +
                        "=" +
                        state.Theme
                            .ToString(),
                        MotionKey +
                        "=" +
                        state.MotionMode
                            .ToString(),
                        IntervalKey +
                        "=" +
                        polling.IntervalSeconds
                            .ToString(),
                        TimeoutKey +
                        "=" +
                        polling.TimeoutMilliseconds
                            .ToString(),
                        RetryKey +
                        "=" +
                        polling.RetryCount
                            .ToString(),
                        MaxRepetitionsKey +
                        "=" +
                        polling.MaxRepetitions
                            .ToString(),
                        LldpKey +
                        "=" +
                        polling.Lldp
                            .ToString(),
                        CdpKey +
                        "=" +
                        polling.Cdp
                            .ToString(),
                        FdbKey +
                        "=" +
                        polling.Fdb
                            .ToString(),
                        ArpKey +
                        "=" +
                        polling.Arp
                            .ToString(),
                        HealthKey +
                        "=" +
                        polling.Health
                            .ToString(),
                        InterfacesKey +
                        "=" +
                        polling.Interfaces
                            .ToString(),
                        StpKey +
                        "=" +
                        polling.Stp
                            .ToString()
                    };

                if (state.AccessProfileId
                    .HasValue)
                {
                    lines.Add(
                        ProfileKey +
                        "=" +
                        state.AccessProfileId
                            .Value
                            .ToString(
                                "D"));
                }

                File.WriteAllLines(
                    _path,
                    lines,
                    new UTF8Encoding(
                        false));
            }
            catch (IOException error)
            {
                Trace.TraceWarning(
                    "UI_SHELL_STATE_SAVE_FAILED " +
                    error.Message);
            }
            catch (UnauthorizedAccessException error)
            {
                Trace.TraceWarning(
                    "UI_SHELL_STATE_SAVE_FAILED " +
                    error.Message);
            }
        }
    }

    internal sealed class NullUiShellStateStore :
        IUiShellStateStore
    {
        public UiShellState Load()
        {
            return UiShellState.Default;
        }

        public void Save(
            UiShellState state)
        {
        }
    }
}
