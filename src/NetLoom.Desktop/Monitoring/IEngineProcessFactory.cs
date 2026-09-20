using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NetLoom.Desktop.Monitoring
{
    internal interface IEngineProcessFactory :
        IDisposable
    {
        IEngineProcessSession Start(
            EngineProcessStartRequest request);
    }

    internal interface IEngineProcessSession :
        IDisposable
    {
        event Action<string> OutputLineReceived;

        event Action<string> ErrorLineReceived;

        Task<int> Completion { get; }

        void BeginRead();

        void WriteLine(
            string line);

        void Terminate();
    }

    internal sealed class EngineProcessStartRequest
    {
        public EngineProcessStartRequest(
            string fileName,
            string arguments)
            : this(
                fileName,
                arguments,
                null)
        {
        }

        public EngineProcessStartRequest(
            string fileName,
            string arguments,
            IReadOnlyDictionary<string, string> environmentVariables)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException(
                    "ENGINE_FILE_NAME_REQUIRED",
                    nameof(fileName));
            }

            FileName = fileName;
            Arguments = arguments ?? string.Empty;

            var copied =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

            if (environmentVariables != null)
            {
                foreach (var pair in environmentVariables)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                    {
                        throw new ArgumentException(
                            "ENGINE_ENVIRONMENT_NAME_REQUIRED",
                            nameof(environmentVariables));
                    }

                    copied[pair.Key] =
                        pair.Value ?? string.Empty;
                }
            }

            EnvironmentVariables = copied;
        }

        public string FileName { get; }

        public string Arguments { get; }

        public IReadOnlyDictionary<string, string> EnvironmentVariables
        {
            get;
        }
    }
}
