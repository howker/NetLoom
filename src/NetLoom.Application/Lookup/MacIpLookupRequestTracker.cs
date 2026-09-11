using System;

namespace NetLoom.Application.Lookup
{
    public sealed class MacIpLookupRequest
    {
        internal MacIpLookupRequest(
            long generation,
            string query)
        {
            Generation = generation;
            Query = query;
        }

        public long Generation { get; }

        public string Query { get; }
    }

    public sealed class MacIpLookupRequestTracker
    {
        private long _latestGeneration;

        private bool _closed;

        private bool _workerRunning;

        private MacIpLookupRequest
            _pendingRequest;

        public MacIpLookupRequest Queue(
            string query)
        {
            if (_closed)
            {
                throw new InvalidOperationException(
                    "Lookup request tracker is closed.");
            }

            if (query == null)
            {
                throw new ArgumentNullException(
                    nameof(query));
            }

            _latestGeneration++;

            var request =
                new MacIpLookupRequest(
                    _latestGeneration,
                    query);

            _pendingRequest =
                request;

            return request;
        }

        public bool TryStartWorker(
            out MacIpLookupRequest request)
        {
            if (_closed ||
                _workerRunning ||
                _pendingRequest == null)
            {
                request = null;
                return false;
            }

            _workerRunning = true;

            request =
                _pendingRequest;

            _pendingRequest =
                null;

            return true;
        }

        public bool TryTakePending(
            out MacIpLookupRequest request)
        {
            if (!_workerRunning)
            {
                throw new InvalidOperationException(
                    "Lookup worker is not running.");
            }

            if (_closed ||
                _pendingRequest == null)
            {
                request = null;
                return false;
            }

            request =
                _pendingRequest;

            _pendingRequest =
                null;

            return true;
        }

        public bool IsCurrent(
            MacIpLookupRequest request)
        {
            return request != null &&
                   !_closed &&
                   request.Generation ==
                   _latestGeneration;
        }

        public void CompleteWorker()
        {
            if (!_workerRunning)
            {
                throw new InvalidOperationException(
                    "Lookup worker is not running.");
            }

            _workerRunning = false;
        }

        public void Close()
        {
            _closed = true;
            _latestGeneration++;
            _pendingRequest = null;
        }
    }
}
