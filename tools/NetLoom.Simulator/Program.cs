using System;
using System.IO;
using System.Linq;
using NetLoom.Simulator.Replay;

namespace NetLoom.Simulator
{
    internal static class Program
    {
        private static int Main(
            string[] args)
        {
            try
            {
                if (args == null ||
                    args.Length != 2 ||
                    !string.Equals(
                        args[0],
                        "replay",
                        StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine(
                        "ERROR: usage: NetLoom.Simulator replay <snapshot-or-directory>");

                    return 2;
                }

                var input =
                    Path.GetFullPath(
                        args[1]);

                var files =
                    ResolveFiles(input);

                var codec =
                    new RawSnmpSnapshotCodec();

                var replayer =
                    new SnmpSnapshotReplayer(
                        codec);

                foreach (var file in files)
                {
                    var snapshot =
                        codec.Load(file);

                    var result =
                        replayer.Replay(
                            snapshot);

                    Console.WriteLine(
                        "SUCCESS: " +
                        Path.GetFileName(file) +
                        " kind=" +
                        result.Kind +
                        " variables=" +
                        result.RawObservation.Variables.Count +
                        " parsed=" +
                        result.ParsedItemCount);
                }

                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    "ERROR: " +
                    exception.Message);

                return 1;
            }
        }

        private static string[] ResolveFiles(
            string input)
        {
            if (File.Exists(input))
            {
                return new[]
                {
                    input
                };
            }

            if (!Directory.Exists(input))
            {
                throw new FileNotFoundException(
                    "Snapshot path does not exist.",
                    input);
            }

            var files =
                Directory
                    .GetFiles(
                        input,
                        "*.json",
                        SearchOption.TopDirectoryOnly)
                    .OrderBy(
                        path => path,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            if (files.Length == 0)
            {
                throw new InvalidOperationException(
                    "Snapshot directory contains no JSON files.");
            }

            return files;
        }
    }
}
