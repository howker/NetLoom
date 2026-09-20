using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Desktop.Monitoring;

namespace NetLoom.Desktop.Discovery
{
    internal sealed class DesktopEngineDiscoveryControl :
        IDiscoveryControl,
        IDisposable
    {
        private static readonly TimeSpan StartupTimeout =
            TimeSpan.FromSeconds(10);

        private static readonly TimeSpan StopTimeout =
            TimeSpan.FromSeconds(5);

        private readonly object _gate =
            new object();

        private readonly string _engineExecutablePath;
        private readonly IEngineProcessFactory _processFactory;
        private readonly IDiscoveryProcessEnvironmentProvider
            _environmentProvider;

        private readonly Queue<DiscoveryNotification>
            _pendingNotifications =
                new Queue<DiscoveryNotification>();

        private DiscoveryControlSnapshot _current;
        private DiscoveryControlRequest _activeRequest;
        private IEngineProcessSession _process;
        private Task _processObserver;
        private TaskCompletionSource<bool> _started;
        private bool _stopRequested;
        private bool _terminalReceived;
        private bool _notificationActive;
        private bool _disposed;

        public DesktopEngineDiscoveryControl(
            string engineExecutablePath)
            : this(
                engineExecutablePath,
                new DesktopEngineProcessFactory(),
                InheritedDiscoveryProcessEnvironmentProvider.Instance)
        {
        }

        internal DesktopEngineDiscoveryControl(
            string engineExecutablePath,
            IDiscoveryProcessEnvironmentProvider environmentProvider)
            : this(
                engineExecutablePath,
                new DesktopEngineProcessFactory(),
                environmentProvider)
        {
        }

        internal DesktopEngineDiscoveryControl(
            string engineExecutablePath,
            IEngineProcessFactory processFactory)
            : this(
                engineExecutablePath,
                processFactory,
                InheritedDiscoveryProcessEnvironmentProvider.Instance)
        {
        }

        internal DesktopEngineDiscoveryControl(
            string engineExecutablePath,
            IEngineProcessFactory processFactory,
            IDiscoveryProcessEnvironmentProvider environmentProvider)
        {
            if (string.IsNullOrWhiteSpace(engineExecutablePath))
            {
                throw new ArgumentException(
                    "ENGINE_EXECUTABLE_PATH_REQUIRED",
                    nameof(engineExecutablePath));
            }

            _engineExecutablePath =
                engineExecutablePath;
            _processFactory =
                processFactory ??
                throw new ArgumentNullException(
                    nameof(processFactory));

            _environmentProvider =
                environmentProvider ??
                throw new ArgumentNullException(
                    nameof(environmentProvider));

            _current =
                Snapshot(
                    DiscoveryControlState.Idle,
                    null,
                    null,
                    0,
                    0,
                    0,
                    null,
                    null);
        }

        public DiscoveryControlSnapshot Current
        {
            get
            {
                lock (_gate)
                {
                    return _current;
                }
            }
        }

        public event EventHandler<DiscoveryControlSnapshotChangedEventArgs>
            SnapshotChanged;

        public event EventHandler<DiscoveryCandidateDiscoveredEventArgs>
            CandidateDiscovered;

        public async Task StartAsync(
            DiscoveryControlRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(
                    nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();

            TaskCompletionSource<bool> started;
            IEngineProcessSession process;
            Task observer;

            lock (_gate)
            {
                ThrowIfDisposed();
                EnsureCanStart();

                _activeRequest = request;
                _stopRequested = false;
                _terminalReceived = false;
                _started =
                    NewCompletionSource<bool>();
                started =
                    _started;

                PublishSnapshotLocked(
                    Snapshot(
                        DiscoveryControlState.Starting,
                        request.Cidr,
                        request.AccessProfileId,
                        0,
                        0,
                        0,
                        null,
                        null));
            }

            try
            {
                process =
                    StartProcess(
                        request,
                        EngineDiscoveryCommandBuilder
                            .BuildTokens(
                                request),
                        out observer);
            }
            catch
            {
                SetStartFailure(
                    request);
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
                        DiscoveryControlState.Faulted,
                        request.Cidr,
                        request.AccessProfileId,
                        0,
                        0,
                        0,
                        null,
                        "ENGINE_DISCOVERY_START_TIMEOUT"));

                throw new TimeoutException(
                    "ENGINE_DISCOVERY_START_TIMEOUT");
            }

            await observer
                .ConfigureAwait(false);

            throw new InvalidOperationException(
                Current.FaultMessage ??
                "ENGINE_DISCOVERY_START_FAILED");
        }

        public Task StopAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return StopOwnedProcessAsync(
                cancellationToken,
                true);
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
                _started = null;
                _activeRequest = null;
                _terminalReceived = false;
                _current =
                    Snapshot(
                        DiscoveryControlState.Stopped,
                        null,
                        null,
                        0,
                        0,
                        0,
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

        private IEngineProcessSession StartProcess(
            DiscoveryControlRequest discoveryRequest,
            IReadOnlyList<string> argumentTokens,
            out Task observer)
        {
            var processRequest =
                new EngineProcessStartRequest(
                    _engineExecutablePath,
                    EngineDiscoveryCommandBuilder
                        .FormatArguments(
                            argumentTokens),
                    _environmentProvider
                        .CreateEnvironment(
                            discoveryRequest.AccessProfileId,
                            discoveryRequest.Version));

            var process =
                _processFactory.Start(
                    processRequest);

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
                        nameof(DesktopEngineDiscoveryControl));
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
            DiscoveryControlRequest request;

            lock (_gate)
            {
                ThrowIfDisposed();

                process = _process;
                observer = _processObserver;
                request = _activeRequest;

                if (process == null)
                {
                    return;
                }

                _stopRequested = true;

                if (publishStopping)
                {
                    PublishSnapshotLocked(
                        Snapshot(
                            DiscoveryControlState.Stopping,
                            request == null
                                ? null
                                : request.Cidr,
                            request == null
                                ? (Guid?)null
                                : request.AccessProfileId,
                            _current.ProcessedAddresses,
                            _current.TotalAddresses,
                            _current.FoundCandidates,
                            _current.CurrentAddress,
                            null));
                }
            }

            try
            {
                process.WriteLine(
                    "STOP");
            }
            catch
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

            lock (_gate)
            {
                if (!ReferenceEquals(
                    _process,
                    process))
                {
                    return;
                }

                var request =
                    _activeRequest;
                var stoppedByRequest =
                    _stopRequested;
                var terminalReceived =
                    _terminalReceived;
                var alreadyFaulted =
                    _current.State ==
                    DiscoveryControlState.Faulted;

                _process = null;
                _processObserver = null;
                _started = null;
                _activeRequest = null;
                _stopRequested = false;
                _terminalReceived = false;

                if (!alreadyFaulted &&
                    !terminalReceived)
                {
                    if (stoppedByRequest)
                    {
                        PublishSnapshotLocked(
                            Snapshot(
                                DiscoveryControlState.Stopped,
                                request == null
                                    ? _current.Cidr
                                    : request.Cidr,
                                request == null
                                    ? _current.AccessProfileId
                                    : request.AccessProfileId,
                                _current.ProcessedAddresses,
                                _current.TotalAddresses,
                                _current.FoundCandidates,
                                _current.CurrentAddress,
                                null));
                    }
                    else
                    {
                        PublishSnapshotLocked(
                            Snapshot(
                                DiscoveryControlState.Faulted,
                                request == null
                                    ? _current.Cidr
                                    : request.Cidr,
                                request == null
                                    ? _current.AccessProfileId
                                    : request.AccessProfileId,
                                _current.ProcessedAddresses,
                                _current.TotalAddresses,
                                _current.FoundCandidates,
                                _current.CurrentAddress,
                                exitCode == 0
                                    ? "ENGINE_DISCOVERY_TERMINAL_MISSING"
                                    : "ENGINE_PROCESS_EXITED_" +
                                      exitCode));
                    }
                }
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
            EngineDiscoveryMarker marker;

            if (!EngineDiscoveryMarkerParser.TryParse(
                line,
                out marker))
            {
                return;
            }

            TaskCompletionSource<bool> started = null;
            IEngineProcessSession terminate = null;

            lock (_gate)
            {
                if (_process == null ||
                    _activeRequest == null)
                {
                    return;
                }

                switch (marker.Kind)
                {
                    case EngineDiscoveryMarkerKind.Started:
                        if (marker.AccessProfileId !=
                            _activeRequest.AccessProfileId)
                        {
                            terminate =
                                ProtocolFaultLocked(
                                    "ENGINE_DISCOVERY_PROFILE_MISMATCH");
                            break;
                        }

                        if (!_stopRequested)
                        {
                            PublishSnapshotLocked(
                                Snapshot(
                                    DiscoveryControlState.Running,
                                    _activeRequest.Cidr,
                                    _activeRequest.AccessProfileId,
                                    0,
                                    marker.TotalAddresses,
                                    0,
                                    null,
                                    null));
                        }

                        started =
                            _started;
                        break;

                    case EngineDiscoveryMarkerKind.Progress:
                        if (!_stopRequested)
                        {
                            PublishSnapshotLocked(
                                Snapshot(
                                    DiscoveryControlState.Running,
                                    _activeRequest.Cidr,
                                    _activeRequest.AccessProfileId,
                                    marker.ProcessedAddresses,
                                    marker.TotalAddresses,
                                    marker.FoundCandidates,
                                    marker.Address,
                                    null));
                        }
                        break;

                    case EngineDiscoveryMarkerKind.Candidate:
                        if (marker.Candidate == null)
                        {
                            break;
                        }

                        if (marker.Candidate.SnmpResponded &&
                            marker.Candidate.AccessProfileId !=
                            _activeRequest.AccessProfileId)
                        {
                            terminate =
                                ProtocolFaultLocked(
                                    "ENGINE_DISCOVERY_CANDIDATE_PROFILE_MISMATCH");
                            break;
                        }

                        EnqueueNotificationLocked(
                            DiscoveryNotification.ForCandidate(
                                marker.Candidate));
                        break;

                    case EngineDiscoveryMarkerKind.Completed:
                        _terminalReceived = true;

                        PublishSnapshotLocked(
                            Snapshot(
                                DiscoveryControlState.Completed,
                                _activeRequest.Cidr,
                                _activeRequest.AccessProfileId,
                                marker.ProcessedAddresses,
                                marker.TotalAddresses,
                                marker.FoundCandidates,
                                _current.CurrentAddress,
                                null));
                        break;

                    case EngineDiscoveryMarkerKind.Stopped:
                        _terminalReceived = true;

                        PublishSnapshotLocked(
                            Snapshot(
                                DiscoveryControlState.Stopped,
                                _activeRequest.Cidr,
                                _activeRequest.AccessProfileId,
                                marker.ProcessedAddresses,
                                marker.TotalAddresses,
                                marker.FoundCandidates,
                                _current.CurrentAddress,
                                null));
                        break;
                }
            }

            if (terminate != null)
            {
                try
                {
                    terminate.Terminate();
                }
                catch
                {
                }
            }

            started?.TrySetResult(
                true);
        }

        private IEngineProcessSession ProtocolFaultLocked(
            string faultMessage)
        {
            var process =
                _process;

            PublishSnapshotLocked(
                Snapshot(
                    DiscoveryControlState.Faulted,
                    _activeRequest == null
                        ? _current.Cidr
                        : _activeRequest.Cidr,
                    _activeRequest == null
                        ? _current.AccessProfileId
                        : _activeRequest.AccessProfileId,
                    _current.ProcessedAddresses,
                    _current.TotalAddresses,
                    _current.FoundCandidates,
                    _current.CurrentAddress,
                    faultMessage));

            _started?.TrySetException(
                new InvalidOperationException(
                    faultMessage));

            return process;
        }

        private void OnErrorLineReceived(
            string line)
        {
        }

        private void SetStartFailure(
            DiscoveryControlRequest request)
        {
            lock (_gate)
            {
                _process = null;
                _processObserver = null;
                _started = null;
                _activeRequest = null;
                _stopRequested = false;
                _terminalReceived = false;

                PublishSnapshotLocked(
                    Snapshot(
                        DiscoveryControlState.Faulted,
                        request.Cidr,
                        request.AccessProfileId,
                        0,
                        0,
                        0,
                        null,
                        "ENGINE_PROCESS_START_FAILED"));
            }
        }

        private void EnsureCanStart()
        {
            if (_process != null ||
                _current.State == DiscoveryControlState.Starting ||
                _current.State == DiscoveryControlState.Running ||
                _current.State == DiscoveryControlState.Stopping)
            {
                throw new InvalidOperationException(
                    "DISCOVERY_ALREADY_ACTIVE");
            }
        }

        private DiscoveryControlSnapshot Snapshot(
            DiscoveryControlState state,
            string cidr,
            Guid? accessProfileId,
            int processedAddresses,
            int totalAddresses,
            int foundCandidates,
            System.Net.IPAddress currentAddress,
            string faultMessage)
        {
            return new DiscoveryControlSnapshot(
                state,
                cidr,
                accessProfileId,
                processedAddresses,
                totalAddresses,
                foundCandidates,
                currentAddress,
                faultMessage);
        }

        private void PublishSnapshot(
            DiscoveryControlSnapshot snapshot)
        {
            lock (_gate)
            {
                PublishSnapshotLocked(
                    snapshot);
            }
        }

        private void PublishSnapshotLocked(
            DiscoveryControlSnapshot snapshot)
        {
            _current = snapshot;

            EnqueueNotificationLocked(
                DiscoveryNotification.ForSnapshot(
                    snapshot));
        }

        private void EnqueueNotificationLocked(
            DiscoveryNotification notification)
        {
            _pendingNotifications.Enqueue(
                notification);

            if (_notificationActive)
            {
                return;
            }

            _notificationActive = true;

            ThreadPool.QueueUserWorkItem(
                state => DispatchNotifications());
        }

        private void DispatchNotifications()
        {
            while (true)
            {
                DiscoveryNotification notification;
                EventHandler<DiscoveryControlSnapshotChangedEventArgs>
                    snapshotHandler;
                EventHandler<DiscoveryCandidateDiscoveredEventArgs>
                    candidateHandler;

                lock (_gate)
                {
                    if (_pendingNotifications.Count == 0)
                    {
                        _notificationActive = false;
                        return;
                    }

                    notification =
                        _pendingNotifications.Dequeue();
                    snapshotHandler =
                        SnapshotChanged;
                    candidateHandler =
                        CandidateDiscovered;
                }

                try
                {
                    if (notification.Snapshot != null)
                    {
                        snapshotHandler?.Invoke(
                            this,
                            new DiscoveryControlSnapshotChangedEventArgs(
                                notification.Snapshot));
                    }
                    else if (notification.Candidate != null)
                    {
                        candidateHandler?.Invoke(
                            this,
                            new DiscoveryCandidateDiscoveredEventArgs(
                                notification.Candidate));
                    }
                }
                catch (Exception exception)
                {
                    Trace.TraceError(
                        "DISCOVERY_CONTROL_NOTIFICATION_HANDLER_FAILED type=" +
                        exception.GetType().Name);
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    nameof(DesktopEngineDiscoveryControl));
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

        private sealed class DiscoveryNotification
        {
            private DiscoveryNotification(
                DiscoveryControlSnapshot snapshot,
                DiscoveryCandidateSnapshot candidate)
            {
                Snapshot = snapshot;
                Candidate = candidate;
            }

            public DiscoveryControlSnapshot Snapshot { get; }

            public DiscoveryCandidateSnapshot Candidate { get; }

            public static DiscoveryNotification ForSnapshot(
                DiscoveryControlSnapshot snapshot)
            {
                return new DiscoveryNotification(
                    snapshot,
                    null);
            }

            public static DiscoveryNotification ForCandidate(
                DiscoveryCandidateSnapshot candidate)
            {
                return new DiscoveryNotification(
                    null,
                    candidate);
            }
        }
    }
}
