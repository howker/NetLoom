using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NetLoom.Engine
{
    internal sealed class EngineDiscoveryStdinControl
    {
        private readonly CancellationTokenSource
            _discoveryCancellation;

        private readonly TextReader _reader;
        private readonly TextWriter _writer;

        private int _readerStarted;

        public EngineDiscoveryStdinControl(
            CancellationTokenSource discoveryCancellation,
            TextReader reader,
            TextWriter writer)
        {
            _discoveryCancellation =
                discoveryCancellation ??
                    throw new ArgumentNullException(
                        nameof(discoveryCancellation));

            _reader =
                reader ??
                    throw new ArgumentNullException(
                        nameof(reader));

            _writer =
                writer ??
                    throw new ArgumentNullException(
                        nameof(writer));
        }

        public void StartReading()
        {
            if (Interlocked.Exchange(
                    ref _readerStarted,
                    1) != 0)
            {
                throw new InvalidOperationException(
                    "STDIN_CONTROL_ALREADY_STARTED");
            }

            Task.Run(
                ReadLoop);
        }

        internal void AcceptLine(
            string line)
        {
            var command =
                (line ?? string.Empty)
                    .Trim();

            if (string.Equals(
                command,
                "STOP",
                StringComparison.Ordinal))
            {
                EngineMachineOutput
                    .WriteDiscoveryControlStopAccepted(
                        _writer);

                _discoveryCancellation.Cancel();
                return;
            }

            EngineMachineOutput
                .WriteDiscoveryControlIgnored(
                    _writer);
        }

        internal void AcceptEndOfInput()
        {
            EngineMachineOutput
                .WriteDiscoveryControlEndOfInput(
                    _writer);

            _discoveryCancellation.Cancel();
        }

        private void ReadLoop()
        {
            try
            {
                while (!_discoveryCancellation
                    .IsCancellationRequested)
                {
                    var line =
                        _reader.ReadLine();

                    if (line == null)
                    {
                        AcceptEndOfInput();
                        return;
                    }

                    AcceptLine(
                        line);
                }
            }
            catch (Exception)
            {
                EngineMachineOutput
                    .WriteDiscoveryControlReadFailed(
                        _writer);

                _discoveryCancellation.Cancel();
            }
        }
    }
}
