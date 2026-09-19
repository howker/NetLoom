using System;
using System.IO;

namespace NetLoom.Desktop.Monitoring
{
    internal static class DesktopEngineExecutablePathResolver
    {
        private const string EngineExecutableName =
            "NetLoom.Engine.exe";

        public static string Resolve()
        {
            var baseDirectory =
                AppDomain.CurrentDomain.BaseDirectory;

            var packaged =
                Path.Combine(
                    baseDirectory,
                    EngineExecutableName);

            if (File.Exists(packaged))
            {
                return packaged;
            }

            var targetFrameworkDirectory =
                new DirectoryInfo(
                    baseDirectory);

            var configurationDirectory =
                targetFrameworkDirectory.Parent;

            var platformDirectory =
                configurationDirectory?.Parent;

            var binDirectory =
                platformDirectory?.Parent;

            var desktopProjectDirectory =
                binDirectory?.Parent;

            var srcDirectory =
                desktopProjectDirectory?.Parent;

            if (configurationDirectory != null &&
                platformDirectory != null &&
                srcDirectory != null)
            {
                var development =
                    Path.Combine(
                        srcDirectory.FullName,
                        "NetLoom.Engine",
                        "bin",
                        platformDirectory.Name,
                        configurationDirectory.Name,
                        "net8.0",
                        EngineExecutableName);

                if (File.Exists(development))
                {
                    return development;
                }
            }

            return packaged;
        }
    }
}
