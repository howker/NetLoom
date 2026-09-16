using System;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.TopologyMap;

namespace NetLoom.Application.TopologyRefresh
{
    public sealed class TopologyRefreshSnapshot
    {
        public TopologyRefreshSnapshot(
            MapSnapshot mapSnapshot,
            TopologyAlertSnapshot alertSnapshot,
            NetworkDiagnosticSnapshot diagnosticSnapshot = null)
        {
            MapSnapshot =
                mapSnapshot ??
                throw new ArgumentNullException(
                    nameof(mapSnapshot));

            AlertSnapshot =
                alertSnapshot ??
                throw new ArgumentNullException(
                    nameof(alertSnapshot));

            DiagnosticSnapshot =
                diagnosticSnapshot ??
                new NetworkDiagnosticSnapshot(
                    MapSnapshot.GeneratedUtc,
                    new DeviceDiagnostic[0],
                    new PhysicalLinkDiagnostic[0]);
        }

        public MapSnapshot MapSnapshot { get; }

        public TopologyAlertSnapshot AlertSnapshot { get; }

        public NetworkDiagnosticSnapshot DiagnosticSnapshot { get; }
    }
}
