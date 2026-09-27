using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace NetLoom.Wpf.Shell
{
    public enum UiShellTheme
    {
        Light = 0,
        Dark = 1
    }

    public sealed class UiShellState
    {
        public UiShellState(
            Guid? accessProfileId,
            UiShellTheme theme)
        {
            AccessProfileId =
                accessProfileId;
            Theme =
                theme;
        }

        public Guid? AccessProfileId { get; }

        public UiShellTheme Theme { get; }

        public static UiShellState Default =>
            new UiShellState(
                null,
                UiShellTheme.Light);
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
                    }
                }

                return new UiShellState(
                    profileId,
                    theme);
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

                var lines =
                    new List<string>
                    {
                        ThemeKey +
                        "=" +
                        state.Theme
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
