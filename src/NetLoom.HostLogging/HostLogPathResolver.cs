using System;
using System.IO;
using System.Runtime.InteropServices;

namespace NetLoom.HostLogging
{
    internal static class HostLogPathResolver
    {
        public static string ResolveDefault()
        {
            var isWindows =
                RuntimeInformation.IsOSPlatform(
                    OSPlatform.Windows);

            return ResolveDefault(
                isWindows,
                Environment.GetFolderPath(
                    Environment.SpecialFolder
                        .CommonApplicationData),
                Environment.GetEnvironmentVariable(
                    "XDG_STATE_HOME"),
                ResolveHomeDirectory());
        }

        internal static string ResolveDefault(
            bool isWindows,
            string commonApplicationData,
            string xdgStateHome,
            string homeDirectory)
        {
            if (isWindows)
            {
                if (string.IsNullOrWhiteSpace(
                    commonApplicationData))
                {
                    throw new InvalidOperationException(
                        "Windows ProgramData path is unavailable.");
                }

                return Path.Combine(
                    commonApplicationData,
                    "NetLoom",
                    "logs");
            }

            if (!string.IsNullOrWhiteSpace(
                xdgStateHome))
            {
                return Path.Combine(
                    xdgStateHome,
                    "netloom",
                    "logs");
            }

            if (string.IsNullOrWhiteSpace(
                homeDirectory))
            {
                throw new InvalidOperationException(
                    "Portable log home directory is unavailable.");
            }

            return Path.Combine(
                homeDirectory,
                ".local",
                "state",
                "netloom",
                "logs");
        }

        private static string ResolveHomeDirectory()
        {
            var environmentHome =
                Environment.GetEnvironmentVariable(
                    "HOME");

            if (!string.IsNullOrWhiteSpace(
                environmentHome))
            {
                return environmentHome;
            }

            return Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);
        }
    }
}
