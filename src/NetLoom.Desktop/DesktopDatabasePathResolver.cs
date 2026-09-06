using System;
using System.IO;

namespace NetLoom.Desktop
{
    internal static class DesktopDatabasePathResolver
    {
        private const string DatabaseArgument =
            "--database";

        private const string DatabaseEnvironmentVariable =
            "NETLOOM_DATABASE";

        public static string Resolve(
            string[] args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            for (var index = 0;
                 index < args.Length;
                 index++)
            {
                if (!string.Equals(
                    args[index],
                    DatabaseArgument,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (index + 1 >= args.Length ||
                    string.IsNullOrWhiteSpace(
                        args[index + 1]))
                {
                    throw new ArgumentException(
                        "Database path argument requires a value.",
                        nameof(args));
                }

                return Path.GetFullPath(
                    args[index + 1]);
            }

            var environmentPath =
                Environment.GetEnvironmentVariable(
                    DatabaseEnvironmentVariable);

            if (!string.IsNullOrWhiteSpace(
                environmentPath))
            {
                return Path.GetFullPath(
                    environmentPath);
            }

            return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder
                        .LocalApplicationData),
                "NetLoom",
                "netloom.db");
        }
    }
}
