using System;
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
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException(
                    "ENGINE_FILE_NAME_REQUIRED",
                    nameof(fileName));
            }

            FileName = fileName;
            Arguments = arguments ?? string.Empty;
        }

        public string FileName { get; }

        public string Arguments { get; }
    }
}
