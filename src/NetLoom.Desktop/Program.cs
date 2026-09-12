using System;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Locations;
using NetLoom.Persistence.Sqlite.Lookup;
using NetLoom.Persistence.Sqlite.Stp;
using NetLoom.Persistence.Sqlite.Topology;
using NetLoom.Topology.Alerts;
using NetLoom.Topology.Map;
using NetLoom.Topology.Refresh;
using NetLoom.Wpf;

namespace NetLoom.Desktop
{
    internal static class Program
    {
        [STAThread]
        private static void Main(
            string[] args)
        {
            var databasePath =
                DesktopDatabasePathResolver.Resolve(
                    args);

            var connectionFactory =
                new SqliteConnectionFactory(
                    databasePath);

            new DatabaseInitializer(
                connectionFactory)
                .Initialize();

            var topologyRepository =
                new SqliteMaterializedTopologyRepository(
                    connectionFactory);

            var stpStore =
                new SqliteStpObservationStore(
                    connectionFactory);

            var mapProvider =
                new MaterializedMapSnapshotProvider(
                    topologyRepository,
                    new SqliteLocationRepository(
                        connectionFactory),
                    new MaterializedTopologyMapProjector());

            var alertProvider =
                new MaterializedTopologyAlertSnapshotProvider(
                    topologyRepository,
                    stpStore);

            var refreshProvider =
                new MaterializedTopologyRefreshSnapshotProvider(
                    new SqliteMaterializedTopologyReadSetReader(
                        connectionFactory),
                    mapProvider,
                    alertProvider);

            var application =
                new System.Windows.Application();

            application.Run(
                new MainWindow(
                    refreshProvider,
                    new SqliteMacIpLookupReader(
                        connectionFactory)));
        }
    }
}
