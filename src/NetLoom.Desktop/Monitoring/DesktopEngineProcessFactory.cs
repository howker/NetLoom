using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace NetLoom.Desktop.Monitoring
{
    internal sealed class DesktopEngineProcessFactory :
        IEngineProcessFactory
    {
        private WindowsKillOnCloseJob _job;
        private bool _disposed;

        public IEngineProcessSession Start(
            EngineProcessStartRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(
                    nameof(request));
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(
                    nameof(DesktopEngineProcessFactory));
            }

            var process =
                new Process
                {
                    StartInfo =
                        new ProcessStartInfo
                        {
                            FileName = request.FileName,
                            Arguments = request.Arguments,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardInput = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        },
                    EnableRaisingEvents = true
                };

            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException(
                        "ENGINE_PROCESS_START_FAILED");
                }

                try
                {
                    if (_job == null)
                    {
                        _job =
                            new WindowsKillOnCloseJob();
                    }

                    _job.Assign(
                        process);
                }
                catch
                {
                    TryKill(
                        process);
                    throw;
                }

                return new DesktopEngineProcessSession(
                    process);
            }
            catch
            {
                process.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _job?.Dispose();
            _job = null;
        }

        private static void TryKill(
            Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
            }
        }

        private sealed class DesktopEngineProcessSession :
            IEngineProcessSession
        {
            private readonly Process _process;
            private readonly TaskCompletionSource<int>
                _completion;

            private bool _readStarted;
            private bool _disposed;

            public DesktopEngineProcessSession(
                Process process)
            {
                _process =
                    process ??
                    throw new ArgumentNullException(
                        nameof(process));

                _completion =
                    new TaskCompletionSource<int>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public event Action<string> OutputLineReceived;

            public event Action<string> ErrorLineReceived;

            public Task<int> Completion =>
                _completion.Task;

            public void BeginRead()
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(
                        nameof(DesktopEngineProcessSession));
                }

                if (_readStarted)
                {
                    throw new InvalidOperationException(
                        "ENGINE_PROCESS_READ_ALREADY_STARTED");
                }

                _readStarted = true;

                _process.OutputDataReceived +=
                    OnOutputDataReceived;

                _process.ErrorDataReceived +=
                    OnErrorDataReceived;

                _process.Exited +=
                    OnExited;

                try
                {
                    _process.BeginOutputReadLine();
                    _process.BeginErrorReadLine();

                    if (_process.HasExited)
                    {
                        CompleteExit();
                    }
                }
                catch
                {
                    _process.OutputDataReceived -=
                        OnOutputDataReceived;

                    _process.ErrorDataReceived -=
                        OnErrorDataReceived;

                    _process.Exited -=
                        OnExited;

                    _readStarted = false;
                    throw;
                }
            }

            public void WriteLine(
                string line)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(
                        nameof(DesktopEngineProcessSession));
                }

                _process.StandardInput.WriteLine(
                    line ?? string.Empty);
                _process.StandardInput.Flush();
            }

            public void Terminate()
            {
                if (_disposed)
                {
                    return;
                }

                TryKill(
                    _process);
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                if (_readStarted)
                {
                    _process.OutputDataReceived -=
                        OnOutputDataReceived;

                    _process.ErrorDataReceived -=
                        OnErrorDataReceived;

                    _process.Exited -=
                        OnExited;
                }

                try
                {
                    _process.StandardInput.Dispose();
                }
                catch
                {
                }

                _process.Dispose();
            }

            private void OnOutputDataReceived(
                object sender,
                DataReceivedEventArgs eventArgs)
            {
                if (eventArgs.Data != null)
                {
                    OutputLineReceived?.Invoke(
                        eventArgs.Data);
                }
            }

            private void OnErrorDataReceived(
                object sender,
                DataReceivedEventArgs eventArgs)
            {
                if (eventArgs.Data != null)
                {
                    ErrorLineReceived?.Invoke(
                        eventArgs.Data);
                }
            }

            private void OnExited(
                object sender,
                EventArgs eventArgs)
            {
                CompleteExit();
            }

            private void CompleteExit()
            {
                Task.Run(
                    () =>
                    {
                        try
                        {
                            _process.WaitForExit();
                            _completion.TrySetResult(
                                _process.ExitCode);
                        }
                        catch (Exception exception)
                        {
                            _completion.TrySetException(
                                exception);
                        }
                    });
            }
        }
    }
}
