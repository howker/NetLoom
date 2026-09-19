using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NetLoom.Application.Discovery;

namespace NetLoom.Engine
{
    internal static class EngineDiscoveryRunner
    {
        public static bool Run(
            DiscoveryEngine engine,
            DiscoveryRequest request,
            CancellationToken cancellationToken,
            TextWriter writer)
        {
            if (engine == null)
            {
                throw new ArgumentNullException(nameof(engine));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            var totalAddresses =
                CountIncludedAddresses(request);

            var processedAddresses = 0;
            var foundCandidates = 0;

            EngineMachineOutput
                .WriteDiscoveryStarted(
                    writer,
                    request.SnmpProfile.AccessProfileId,
                    totalAddresses,
                    request.InterAddressDelayMilliseconds);

            try
            {
                foreach (var progress in
                    engine.Discover(
                        request,
                        cancellationToken))
                {
                    processedAddresses =
                        progress.ProcessedAddresses;

                    if (progress.CandidateFound)
                    {
                        foundCandidates++;
                    }

                    EngineMachineOutput
                        .WriteDiscoveryProgress(
                            writer,
                            progress,
                            foundCandidates);

                    if (progress.CandidateFound)
                    {
                        EngineMachineOutput
                            .WriteDiscoveryCandidate(
                                writer,
                                progress.Candidate);
                    }
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                EngineMachineOutput
                    .WriteDiscoveryStopped(
                        writer,
                        processedAddresses,
                        totalAddresses,
                        foundCandidates);

                return false;
            }

            EngineMachineOutput
                .WriteDiscoveryCompleted(
                    writer,
                    processedAddresses,
                    totalAddresses,
                    foundCandidates);

            return true;
        }

        private static int CountIncludedAddresses(
            DiscoveryRequest request)
        {
            var exclusions =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (var exclusion in request.Exclusions)
            {
                exclusions.Add(
                    exclusion.ToString());
            }

            var count = 0;

            foreach (var address in request.Addresses)
            {
                if (!exclusions.Contains(
                    address.ToString()))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
