using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NetLoom.Engine
{
    internal sealed class EngineScheduleStdinControl
    {
        private readonly CancellationTokenSource
            _scheduleCancellation;

        private readonly TextReader _reader;
        private readonly TextWriter _writer;
        private readonly object _delayGate;

        private CancellationTokenSource
            _activeDelayCancellation;

        private int _readerStarted;

        public EngineScheduleStdinControl(
            CancellationTokenSource scheduleCancellation,
            TextReader reader,
            TextWriter writer)
        {
            _scheduleCancellation =
                scheduleCancellation ??
                throw new ArgumentNullException(
                    nameof(scheduleCancellation));

            _reader =
                reader ??
                throw new ArgumentNullException(
                    nameof(reader));

            _writer =
                writer ??
                throw new ArgumentNullException(
                    nameof(writer));

            _delayGate =
                new object();
        }

        internal bool HasActiveDelay
        {
            get
            {
                lock (_delayGate)
                {
                    return _activeDelayCancellation != null;
                }
            }
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

        public void Wait(
            TimeSpan interval,
            CancellationToken cancellationToken)
        {
            if (interval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(interval));
            }

            using (var delayCancellation =
                CancellationTokenSource
                    .CreateLinkedTokenSource(
                        cancellationToken))
            {
                lock (_delayGate)
                {
                    _activeDelayCancellation =
                        delayCancellation;
                }

                try
                {
                    Task.Delay(
                            interval,
                            delayCancellation.Token)
                        .GetAwaiter()
                        .GetResult();
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken
                        .IsCancellationRequested)
                    {
                        throw new OperationCanceledException(
                            cancellationToken);
                    }
                }
                finally
                {
                    lock (_delayGate)
                    {
                        if (ReferenceEquals(
                            _activeDelayCancellation,
                            delayCancellation))
                        {
                            _activeDelayCancellation =
                                null;
                        }
                    }
                }
            }
        }

        internal void AcceptLine(
            string line)
        {
            var command =
                (line ?? string.Empty)
                    .Trim();

            if (string.Equals(
                command,
                "POLL_NOW",
                StringComparison.Ordinal))
            {
                CancellationTokenSource activeDelay;

                lock (_delayGate)
                {
                    activeDelay =
                        _activeDelayCancellation;

                    if (activeDelay != null)
                    {
                        activeDelay.Cancel();
                    }
                }

                if (activeDelay != null)
                {
                    EngineMachineOutput
                        .WriteControlPollNowAccepted(
                            _writer);
                }
                else
                {
                    EngineMachineOutput
                        .WriteControlPollNowNoActiveDelay(
                            _writer);
                }

                return;
            }

            if (string.Equals(
                command,
                "STOP",
                StringComparison.Ordinal))
            {
                EngineMachineOutput
                    .WriteControlStopAccepted(
                        _writer);

                _scheduleCancellation.Cancel();
                return;
            }

            EngineMachineOutput
                .WriteControlIgnored(
                    _writer);
        }

        internal void AcceptEndOfInput()
        {
            EngineMachineOutput
                .WriteControlEndOfInput(
                    _writer);

            _scheduleCancellation.Cancel();
        }

        private void ReadLoop()
        {
            try
            {
                while (!_scheduleCancellation
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
                    .WriteControlReadFailed(
                        _writer);

                _scheduleCancellation.Cancel();
            }
        }
    }
}
