using System;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Locations;
using NetLoom.Persistence.Sqlite.Lookup;
using NetLoom.Persistence.Sqlite.Topology;
using NetLoom.Topology.Map;
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

            var provider =
                new MaterializedMapSnapshotProvider(
                    new SqliteMaterializedTopologyRepository(
                        connectionFactory),
                    new SqliteLocationRepository(
                        connectionFactory),
                    new MaterializedTopologyMapProjector());

            var application =
                new System.Windows.Application();

            application.Run(
                new MainWindow(
                    provider,
                    new SqliteMacIpLookupReader(
                        connectionFactory)));
        }
    }
}
