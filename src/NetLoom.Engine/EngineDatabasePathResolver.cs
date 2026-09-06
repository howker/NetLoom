using System;
using System.IO;

namespace NetLoom.Engine
{
    internal static class EngineDatabasePathResolver
    {
        private const string DatabaseEnvironmentVariable =
            "NETLOOM_DATABASE";

        public static string Resolve(
            string explicitPath)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                return Path.GetFullPath(explicitPath);
            }

            var environmentPath =
                Environment.GetEnvironmentVariable(
                    DatabaseEnvironmentVariable);

            if (!string.IsNullOrWhiteSpace(environmentPath))
            {
                return Path.GetFullPath(environmentPath);
            }

            return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "NetLoom",
                "netloom.db");
        }
    }
}
