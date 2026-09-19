using System;
using System.Diagnostics;
using NetLoom.Application.Locations;
using NetLoom.Application.Topology;
using NetLoom.Desktop.Monitoring;
using NetLoom.HostLogging;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Locations;
using NetLoom.Persistence.Sqlite.Lookup;
using NetLoom.Persistence.Sqlite.MapLayout;
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
        private static int Main(
            string[] args)
        {
            HostLogManager hostLog = null;
            HostLogTraceListener traceListener = null;

            try
            {
                hostLog =
                    HostLogManager.Create(
                        "desktop");

                hostLog.Info(
                    "HOST_STARTED");

                var uiCulture =
                    DesktopUiCultureSelector.Apply();

                hostLog.Info(
                    "UI_CULTURE name=" +
                    uiCulture.Name);

                traceListener =
                    new HostLogTraceListener(
                        hostLog);

                Trace.Listeners.Add(
                    traceListener);

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

                var locationRepository =
                    new SqliteLocationRepository(
                        connectionFactory);

                var mapLayoutStore =
                    new SqliteMapLayoutStore(
                        connectionFactory);

                var mapProvider =
                    new MaterializedMapSnapshotProvider(
                        topologyRepository,
                        locationRepository,
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

                var monitoringControl =
                    new DesktopEngineMonitoringControl(
                        DesktopEngineExecutablePathResolver
                            .Resolve(),
                        databasePath);

                var application =
                    new System.Windows.Application();

                var exitCode =
                    application.Run(
                        new MainWindow(
                            refreshProvider,
                            new SqliteMacIpLookupReader(
                                connectionFactory),
                            mapLayoutStore,
                            new ManualTopologyService(
                                topologyRepository,
                                new SqliteManualTopologyAuditStore(
                                    connectionFactory)),
                            new LocationTopologyService(
                                locationRepository,
                                topologyRepository),
                            mapLayoutStore,
                            monitoringControl));

                hostLog.Info(
                    "HOST_STOPPED exitCode=" +
                    exitCode);

                return exitCode;
            }
            catch (Exception exception)
            {
                if (hostLog != null)
                {
                    hostLog.Error(
                        exception,
                        "HOST_FATAL");

                    hostLog.Flush();
                }

                return 2;
            }
            finally
            {
                if (traceListener != null)
                {
                    Trace.Listeners.Remove(
                        traceListener);

                    traceListener.Dispose();
                }

                if (hostLog != null)
                {
                    hostLog.Dispose();
                }
            }
        }
    }
}
