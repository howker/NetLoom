using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NetLoom.Engine
{
    internal sealed class EngineMultiTargetScheduleStdinControl
    {
        private readonly CancellationTokenSource
            _scheduleCancellation;

        private readonly TextReader _reader;
        private readonly TextWriter _writer;
        private readonly object _delayGate;

        private readonly HashSet<CancellationTokenSource>
            _activeDelayCancellations;

        private int _readerStarted;

        public EngineMultiTargetScheduleStdinControl(
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

            _activeDelayCancellations =
                new HashSet<CancellationTokenSource>();
        }

        internal int ActiveDelayCount
        {
            get
            {
                lock (_delayGate)
                {
                    return _activeDelayCancellations.Count;
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

        public async Task WaitAsync(
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
                    _activeDelayCancellations.Add(
                        delayCancellation);
                }

                try
                {
                    await Task.Delay(
                            interval,
                            delayCancellation.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken
                        .IsCancellationRequested)
                    {
                        throw;
                    }
                }
                finally
                {
                    lock (_delayGate)
                    {
                        _activeDelayCancellations.Remove(
                            delayCancellation);
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
                CancellationTokenSource[] active;

                lock (_delayGate)
                {
                    active =
                        new List<CancellationTokenSource>(
                            _activeDelayCancellations)
                            .ToArray();
                }

                foreach (var cancellation in active)
                {
                    cancellation.Cancel();
                }

                if (active.Length > 0)
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
