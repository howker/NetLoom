using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetLoom.Application.TopologyRefresh
{
    public enum TopologyRefreshExecutionKind
    {
        Skipped = 0,
        Succeeded = 1,
        Discarded = 2
    }

    public sealed class TopologyRefreshExecutionResult
    {
        internal TopologyRefreshExecutionResult(
            TopologyRefreshExecutionKind kind,
            TopologyRefreshSnapshot snapshot)
        {
            Kind = kind;
            Snapshot = snapshot;
        }

        public TopologyRefreshExecutionKind Kind { get; }

        public TopologyRefreshSnapshot Snapshot { get; }
    }

    public sealed class TopologyRefreshCoordinator
    {
        private readonly ITopologyRefreshSnapshotProvider _provider;
        private readonly object _sync = new object();
        private long _latestGeneration;
        private long _activeGeneration;
        private bool _closed;
        private bool _refreshRunning;

        public TopologyRefreshCoordinator(
            ITopologyRefreshSnapshotProvider provider)
        {
            _provider =
                provider ??
                throw new ArgumentNullException(
                    nameof(provider));
        }

        public async Task<TopologyRefreshExecutionResult>
            RefreshAsync(
                string stpInstanceId,
                CancellationToken cancellationToken)
        {
            long generation;

            if (!TryBegin(out generation))
            {
                return Result(
                    TopologyRefreshExecutionKind.Skipped,
                    null);
            }

            try
            {
                TopologyRefreshSnapshot snapshot;

                try
                {
                    snapshot =
                        await Task.Run(
                                () =>
                                    _provider.GetSnapshot(
                                        stpInstanceId),
                                cancellationToken)
                            .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested ||
                          !IsCurrent(generation))
                {
                    return Result(
                        TopologyRefreshExecutionKind.Discarded,
                        null);
                }
                catch
                {
                    if (cancellationToken.IsCancellationRequested ||
                        !IsCurrent(generation))
                    {
                        return Result(
                            TopologyRefreshExecutionKind.Discarded,
                            null);
                    }

                    throw;
                }

                if (cancellationToken.IsCancellationRequested ||
                    !IsCurrent(generation))
                {
                    return Result(
                        TopologyRefreshExecutionKind.Discarded,
                        null);
                }

                if (snapshot == null)
                {
                    throw new InvalidOperationException(
                        "Topology refresh provider returned null.");
                }

                return Result(
                    TopologyRefreshExecutionKind.Succeeded,
                    snapshot);
            }
            finally
            {
                Complete(generation);
            }
        }

        public void Close()
        {
            lock (_sync)
            {
                if (_closed)
                {
                    return;
                }

                _closed = true;
                _latestGeneration++;
            }
        }

        private bool TryBegin(
            out long generation)
        {
            lock (_sync)
            {
                if (_closed ||
                    _refreshRunning)
                {
                    generation = 0L;
                    return false;
                }

                _latestGeneration++;
                _activeGeneration =
                    _latestGeneration;
                _refreshRunning = true;

                generation =
                    _activeGeneration;

                return true;
            }
        }

        private bool IsCurrent(
            long generation)
        {
            lock (_sync)
            {
                return !_closed &&
                       _refreshRunning &&
                       _activeGeneration == generation &&
                       _latestGeneration == generation;
            }
        }

        private void Complete(
            long generation)
        {
            lock (_sync)
            {
                if (!_refreshRunning ||
                    _activeGeneration != generation)
                {
                    throw new InvalidOperationException(
                        "Topology refresh generation is not active.");
                }

                _refreshRunning = false;
                _activeGeneration = 0L;
            }
        }

        private static TopologyRefreshExecutionResult
            Result(
                TopologyRefreshExecutionKind kind,
                TopologyRefreshSnapshot snapshot)
        {
            return new TopologyRefreshExecutionResult(
                kind,
                snapshot);
        }
    }
}
