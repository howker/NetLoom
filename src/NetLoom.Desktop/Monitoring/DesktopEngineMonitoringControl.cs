using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NetLoom.Application.MonitoringControl;

namespace NetLoom.Desktop.Monitoring
{
    public sealed class DesktopEngineMonitoringControl :
        IMonitoringControl,
        IDisposable
    {
        private static readonly TimeSpan StartupTimeout =
            TimeSpan.FromSeconds(10);

        private static readonly TimeSpan StopTimeout =
            TimeSpan.FromSeconds(5);

        private readonly object _gate =
            new object();

        private readonly string _engineExecutablePath;
        private readonly string _databasePath;
        private readonly IEngineProcessFactory _processFactory;

        private readonly Queue<MonitoringControlSnapshot>
            _pendingSnapshotNotifications =
                new Queue<MonitoringControlSnapshot>();

        private MonitoringControlSnapshot _current;
        private IEngineProcessSession _process;
        private Task _processObserver;
        private TaskCompletionSource<bool> _scheduleStarted;
        private EngineProcessPurpose _purpose;
        private MonitoringTarget _activeTarget;
        private bool _stopRequested;
        private bool _snapshotNotificationActive;
        private bool _disposed;

        public DesktopEngineMonitoringControl(
            string engineExecutablePath,
            string databasePath)
            : this(
                engineExecutablePath,
                databasePath,
                new DesktopEngineProcessFactory())
        {
        }

        internal DesktopEngineMonitoringControl(
            string engineExecutablePath,
            string databasePath,
            IEngineProcessFactory processFactory)
        {
            if (string.IsNullOrWhiteSpace(engineExecutablePath))
            {
                throw new ArgumentException(
                    "ENGINE_EXECUTABLE_PATH_REQUIRED",
                    nameof(engineExecutablePath));
            }

            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException(
                    "DATABASE_PATH_REQUIRED",
                    nameof(databasePath));
            }

            _engineExecutablePath =
                engineExecutablePath;
            _databasePath =
                databasePath;
            _processFactory =
                processFactory ??
                throw new ArgumentNullException(
                    nameof(processFactory));

            _current =
                Snapshot(
                    MonitoringControlState.Stopped,
                    null,
                    null);
        }

        public MonitoringControlSnapshot Current
        {
            get
            {
                lock (_gate)
                {
                    return _current;
                }
            }
        }

        public event EventHandler<MonitoringControlSnapshotChangedEventArgs>
            SnapshotChanged;

        public async Task StartAsync(
            MonitoringTarget target,
            MonitoringSessionPolicy policy,
            CancellationToken cancellationToken)
        {
            if (target == null)
            {
                throw new ArgumentNullException(
                    nameof(target));
            }

            if (policy == null)
            {
                throw new ArgumentNullException(
                    nameof(policy));
            }

            cancellationToken.ThrowIfCancellationRequested();

            TaskCompletionSource<bool> started;
            IEngineProcessSession process;
            Task observer;

            lock (_gate)
            {
                ThrowIfDisposed();
                EnsureCanStart();

                _activeTarget = target;
                _stopRequested = false;
                _purpose =
                    EngineProcessPurpose.Schedule;
                _scheduleStarted =
                    NewCompletionSource<bool>();
                started =
                    _scheduleStarted;

                PublishSnapshotLocked(
                    Snapshot(
                        MonitoringControlState.Starting,
                        target,
                        null));
            }

            try
            {
                process =
                    StartProcess(
                        EngineMonitoringCommandBuilder
                            .BuildScheduleTokens(
                                target,
                                policy,
                                _databasePath),
                        out observer);
            }
            catch
            {
                SetStartFailure(
                    target);
                throw;
            }

            var cancellationCompletion =
                CancellationTask(
                    cancellationToken);

            var timeout =
                Task.Delay(
                    StartupTimeout);

            var winner =
                await Task.WhenAny(
                    started.Task,
                    observer,
                    cancellationCompletion,
                    timeout)
                .ConfigureAwait(false);

            if (winner == started.Task)
            {
                await started.Task
                    .ConfigureAwait(false);
                return;
            }

            if (winner == cancellationCompletion)
            {
                await StopOwnedProcessAsync(
                        CancellationToken.None,
                        false)
                    .ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();
            }

            if (winner == timeout)
            {
                process.Terminate();

                await observer
                    .ConfigureAwait(false);

                PublishSnapshot(
                    Snapshot(
                        MonitoringControlState.Faulted,
                        target,
                        "ENGINE_START_TIMEOUT"));

                throw new TimeoutException(
                    "ENGINE_START_TIMEOUT");
            }

            await observer
                .ConfigureAwait(false);

            throw new InvalidOperationException(
                Current.FaultMessage ??
                "ENGINE_START_FAILED");
        }

        public Task StopAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return StopOwnedProcessAsync(
                cancellationToken,
                true);
        }

        public Task PollNowAsync(
            MonitoringTarget targetWhenStopped,
            MonitoringSessionPolicy policyWhenStopped,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IEngineProcessSession runningProcess;
            MonitoringControlState state;

            lock (_gate)
            {
                ThrowIfDisposed();
                runningProcess =
                    _process;
                state =
                    _current.State;
            }

            if (runningProcess != null &&
                (state == MonitoringControlState.Running ||
                 state == MonitoringControlState.Polling))
            {
                runningProcess.WriteLine(
                    "POLL_NOW");

                return Task.CompletedTask;
            }

            if (state == MonitoringControlState.Starting ||
                state == MonitoringControlState.Stopping)
            {
                throw new InvalidOperationException(
                    "MONITORING_CONTROL_BUSY");
            }

            return PollOnceAsync(
                targetWhenStopped,
                policyWhenStopped,
                cancellationToken);
        }

        public void Dispose()
        {
            IEngineProcessSession process;

            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _stopRequested = true;
                process = _process;
                _process = null;
                _processObserver = null;
                _scheduleStarted = null;
                _purpose =
                    EngineProcessPurpose.None;
                _activeTarget = null;
                _current =
                    Snapshot(
                        MonitoringControlState.Stopped,
                        null,
                        null);
            }

            if (process != null)
            {
                try
                {
                    process.WriteLine(
                        "STOP");
                }
                catch
                {
                }

                process.Dispose();
            }

            _processFactory.Dispose();
        }

        private async Task PollOnceAsync(
            MonitoringTarget target,
            MonitoringSessionPolicy policy,
            CancellationToken cancellationToken)
        {
            if (target == null)
            {
                throw new ArgumentNullException(
                    nameof(target));
            }

            if (policy == null)
            {
                throw new ArgumentNullException(
                    nameof(policy));
            }

            lock (_gate)
            {
                ThrowIfDisposed();
                EnsureCanStart();

                _activeTarget = target;
                _stopRequested = false;
                _purpose =
                    EngineProcessPurpose.PollOnce;

                PublishSnapshotLocked(
                    Snapshot(
                        MonitoringControlState.Polling,
                        target,
                        null));
            }

            Task observer;

            try
            {
                StartProcess(
                    EngineMonitoringCommandBuilder
                        .BuildPollOnceTokens(
                            target,
                            policy,
                            _databasePath),
                    out observer);
            }
            catch
            {
                SetStartFailure(
                    target);
                throw;
            }

            var cancellationCompletion =
                CancellationTask(
                    cancellationToken);

            var winner =
                await Task.WhenAny(
                    observer,
                    cancellationCompletion)
                .ConfigureAwait(false);

            if (winner == cancellationCompletion)
            {
                IEngineProcessSession process;

                lock (_gate)
                {
                    process = _process;
                    _stopRequested = true;
                }

                process?.Terminate();

                cancellationToken.ThrowIfCancellationRequested();
            }

            await observer
                .ConfigureAwait(false);
        }

        private IEngineProcessSession StartProcess(
            IReadOnlyList<string> argumentTokens,
            out Task observer)
        {
            var request =
                new EngineProcessStartRequest(
                    _engineExecutablePath,
                    EngineMonitoringCommandBuilder
                        .FormatArguments(
                            argumentTokens));

            var process =
                _processFactory.Start(
                    request);

            process.OutputLineReceived +=
                OnOutputLineReceived;

            process.ErrorLineReceived +=
                OnErrorLineReceived;

            lock (_gate)
            {
                if (_disposed)
                {
                    process.Dispose();
                    throw new ObjectDisposedException(
                        nameof(DesktopEngineMonitoringControl));
                }

                _process = process;
            }

            try
            {
                process.BeginRead();
            }
            catch
            {
                lock (_gate)
                {
                    if (ReferenceEquals(
                        _process,
                        process))
                    {
                        _process = null;
                    }
                }

                try
                {
                    process.Terminate();
                }
                catch
                {
                }

                process.OutputLineReceived -=
                    OnOutputLineReceived;
                process.ErrorLineReceived -=
                    OnErrorLineReceived;
                process.Dispose();
                throw;
            }

            observer =
                ObserveProcessExitAsync(
                    process);

            lock (_gate)
            {
                if (ReferenceEquals(
                    _process,
                    process))
                {
                    _processObserver = observer;
                }
            }

            return process;
        }

        private async Task StopOwnedProcessAsync(
            CancellationToken cancellationToken,
            bool publishStopping)
        {
            IEngineProcessSession process;
            Task observer;
            EngineProcessPurpose purpose;
            MonitoringTarget target;

            lock (_gate)
            {
                ThrowIfDisposed();

                process = _process;
                observer = _processObserver;
                purpose = _purpose;
                target = _activeTarget;

                if (process == null)
                {
                    _stopRequested = false;
                    _purpose =
                        EngineProcessPurpose.None;
                    _activeTarget = null;

                    PublishSnapshotLocked(
                        Snapshot(
                            MonitoringControlState.Stopped,
                            null,
                            null));
                    return;
                }

                _stopRequested = true;

                if (publishStopping)
                {
                    PublishSnapshotLocked(
                        Snapshot(
                            MonitoringControlState.Stopping,
                            target,
                            null));
                }
            }

            if (purpose == EngineProcessPurpose.Schedule)
            {
                try
                {
                    process.WriteLine(
                        "STOP");
                }
                catch
                {
                    process.Terminate();
                }
            }
            else
            {
                process.Terminate();
            }

            var cancellationCompletion =
                CancellationTask(
                    cancellationToken);

            var timeout =
                Task.Delay(
                    StopTimeout);

            var winner =
                await Task.WhenAny(
                    observer,
                    cancellationCompletion,
                    timeout)
                .ConfigureAwait(false);

            if (winner == cancellationCompletion)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (winner == timeout)
            {
                process.Terminate();
            }

            await observer
                .ConfigureAwait(false);
        }

        private async Task ObserveProcessExitAsync(
            IEngineProcessSession process)
        {
            int exitCode;

            try
            {
                exitCode =
                    await process.Completion
                        .ConfigureAwait(false);
            }
            catch
            {
                exitCode = -1;
            }

            MonitoringControlSnapshot next = null;

            lock (_gate)
            {
                if (!ReferenceEquals(
                    _process,
                    process))
                {
                    return;
                }

                var purpose =
                    _purpose;
                var target =
                    _activeTarget;
                var stoppedByRequest =
                    _stopRequested;

                _process = null;
                _processObserver = null;
                _scheduleStarted = null;
                _purpose =
                    EngineProcessPurpose.None;
                _activeTarget = null;
                _stopRequested = false;

                if (stoppedByRequest ||
                    (purpose == EngineProcessPurpose.PollOnce &&
                     exitCode == 0))
                {
                    next =
                        Snapshot(
                            MonitoringControlState.Stopped,
                            null,
                            null);
                }
                else
                {
                    next =
                        Snapshot(
                            MonitoringControlState.Faulted,
                            target,
                            "ENGINE_PROCESS_EXITED_" +
                            exitCode);
                }

                PublishSnapshotLocked(
                    next);
            }

            process.OutputLineReceived -=
                OnOutputLineReceived;
            process.ErrorLineReceived -=
                OnErrorLineReceived;
            process.Dispose();
        }

        private void OnOutputLineReceived(
            string line)
        {
            EngineMachineMarker marker;

            if (!EngineMachineMarkerParser.TryParse(
                line,
                out marker))
            {
                return;
            }

            TaskCompletionSource<bool> scheduleStarted = null;
            MonitoringControlSnapshot next = null;

            lock (_gate)
            {
                if (_process == null)
                {
                    return;
                }

                switch (marker.Kind)
                {
                    case EngineMachineMarkerKind.ScheduleStarted:
                        if (!_stopRequested)
                        {
                            next =
                                Snapshot(
                                    MonitoringControlState.Running,
                                    _activeTarget,
                                    null);
                        }

                        scheduleStarted =
                            _scheduleStarted;
                        break;

                    case EngineMachineMarkerKind.PollStarted:
                        if (!_stopRequested)
                        {
                            next =
                                Snapshot(
                                    MonitoringControlState.Polling,
                                    _activeTarget,
                                    null);
                        }
                        break;

                    case EngineMachineMarkerKind.PollCompleted:
                        var lastSuccessful =
                            _current.LastSuccessfulPollUtc;

                        if (marker.AnySucceeded == true)
                        {
                            lastSuccessful =
                                marker.CompletedUtc;
                        }

                        if (!_stopRequested)
                        {
                            next =
                                Snapshot(
                                    _purpose ==
                                        EngineProcessPurpose.Schedule
                                        ? MonitoringControlState.Running
                                        : MonitoringControlState.Polling,
                                    _activeTarget,
                                    null,
                                    lastSuccessful);
                        }
                        break;
                }

                if (next != null)
                {
                    PublishSnapshotLocked(
                        next);
                }
            }

            scheduleStarted?.TrySetResult(
                true);
        }

        private void OnErrorLineReceived(
            string line)
        {
        }

        private void SetStartFailure(
            MonitoringTarget target)
        {
            lock (_gate)
            {
                _process = null;
                _processObserver = null;
                _scheduleStarted = null;
                _purpose =
                    EngineProcessPurpose.None;
                _activeTarget = null;
                _stopRequested = false;

                PublishSnapshotLocked(
                    Snapshot(
                        MonitoringControlState.Faulted,
                        target,
                        "ENGINE_PROCESS_START_FAILED"));
            }
        }

        private void EnsureCanStart()
        {
            if (_process != null ||
                _current.State == MonitoringControlState.Starting ||
                _current.State == MonitoringControlState.Running ||
                _current.State == MonitoringControlState.Polling ||
                _current.State == MonitoringControlState.Stopping)
            {
                throw new InvalidOperationException(
                    "MONITORING_ALREADY_ACTIVE");
            }
        }

        private MonitoringControlSnapshot Snapshot(
            MonitoringControlState state,
            MonitoringTarget target,
            string faultMessage,
            DateTime? lastSuccessfulPollUtc = null)
        {
            return new MonitoringControlSnapshot(
                state,
                target,
                lastSuccessfulPollUtc ??
                    _current?.LastSuccessfulPollUtc,
                faultMessage);
        }

        private void PublishSnapshot(
            MonitoringControlSnapshot snapshot)
        {
            lock (_gate)
            {
                PublishSnapshotLocked(
                    snapshot);
            }
        }

        private void PublishSnapshotLocked(
            MonitoringControlSnapshot snapshot)
        {
            _current = snapshot;
            _pendingSnapshotNotifications.Enqueue(
                snapshot);

            if (_snapshotNotificationActive)
            {
                return;
            }

            _snapshotNotificationActive = true;

            ThreadPool.QueueUserWorkItem(
                state => DispatchSnapshotNotifications());
        }

        private void DispatchSnapshotNotifications()
        {
            while (true)
            {
                MonitoringControlSnapshot snapshot;
                EventHandler<MonitoringControlSnapshotChangedEventArgs>
                    handler;

                lock (_gate)
                {
                    if (_pendingSnapshotNotifications.Count == 0)
                    {
                        _snapshotNotificationActive = false;
                        return;
                    }

                    snapshot =
                        _pendingSnapshotNotifications.Dequeue();
                    handler =
                        SnapshotChanged;
                }

                if (handler == null)
                {
                    continue;
                }

                try
                {
                    handler(
                        this,
                        new MonitoringControlSnapshotChangedEventArgs(
                            snapshot));
                }
                catch (Exception exception)
                {
                    Trace.TraceError(
                        "MONITORING_CONTROL_SNAPSHOT_HANDLER_FAILED type=" +
                        exception.GetType().Name);
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    nameof(DesktopEngineMonitoringControl));
            }
        }

        private static TaskCompletionSource<T>
            NewCompletionSource<T>()
        {
            return new TaskCompletionSource<T>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static Task CancellationTask(
            CancellationToken cancellationToken)
        {
            return Task.Delay(
                Timeout.Infinite,
                cancellationToken);
        }

        private enum EngineProcessPurpose
        {
            None = 0,
            Schedule = 1,
            PollOnce = 2
        }
    }
}
