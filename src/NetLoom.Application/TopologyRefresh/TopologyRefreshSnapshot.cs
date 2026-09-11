using System;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.TopologyMap;

namespace NetLoom.Application.TopologyRefresh
{
    public sealed class TopologyRefreshSnapshot
    {
        public TopologyRefreshSnapshot(
            MapSnapshot mapSnapshot,
            TopologyAlertSnapshot alertSnapshot)
        {
            MapSnapshot =
                mapSnapshot ??
                throw new ArgumentNullException(
                    nameof(mapSnapshot));

            AlertSnapshot =
                alertSnapshot ??
                throw new ArgumentNullException(
                    nameof(alertSnapshot));
        }

        public MapSnapshot MapSnapshot { get; }

        public TopologyAlertSnapshot AlertSnapshot { get; }
    }
}
