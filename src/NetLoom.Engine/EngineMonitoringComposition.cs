using System;
using NetLoom.Application.Monitoring;
using NetLoom.Application.Monitoring.Interfaces;
using NetLoom.Application.Observations;
using NetLoom.Persistence.Sqlite.Arp;
using NetLoom.Persistence.Sqlite.Cdp;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Fdb;
using NetLoom.Persistence.Sqlite.Lldp;
using NetLoom.Persistence.Sqlite.Monitoring;
using NetLoom.Persistence.Sqlite.Observations;
using NetLoom.Persistence.Sqlite.Stp;
using NetLoom.Persistence.Sqlite.Topology;
using NetLoom.Protocols.Snmp.Arp;
using NetLoom.Protocols.Snmp.Cdp;
using NetLoom.Protocols.Snmp.Fdb;
using NetLoom.Protocols.Snmp.Health;
using NetLoom.Protocols.Snmp.Interfaces;
using NetLoom.Protocols.Snmp.Lldp;
using NetLoom.Protocols.Snmp.Stp;
using NetLoom.Protocols.Snmp.Transport;
using NetLoom.Topology.Materialization;

namespace NetLoom.Engine
{
    internal static class EngineMonitoringComposition
    {
        public static MonitoringRuntime Create(
            string databasePath,
            InterfaceDegradationPolicy
                interfaceDegradationPolicy = null)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException(
                    "DATABASE_PATH_REQUIRED",
                    nameof(databasePath));
            }

            var connectionFactory =
                new SqliteConnectionFactory(
                    databasePath);

            new DatabaseInitializer(
                connectionFactory)
                .Initialize();

            IObservationStore rawStore =
                new SqliteObservationStore(
                    connectionFactory);

            var transport =
                new SharpSnmpTransport();

            var stpCollector =
                new StpCollector(
                    transport,
                    rawStore,
                    new SqliteStpObservationStore(
                        connectionFactory),
                    new StpObservationParser());

            var topologyRepository =
                new SqliteMaterializedTopologyRepository(
                    connectionFactory);

            return new MonitoringRuntime(
                new LldpCollector(
                    transport,
                    rawStore,
                    new SqliteLldpObservationStore(
                        connectionFactory),
                    new LldpObservationParser()),
                new CdpCollector(
                    transport,
                    rawStore,
                    new SqliteCdpObservationStore(
                        connectionFactory),
                    new CdpObservationParser()),
                new FdbCollector(
                    transport,
                    rawStore,
                    new SqliteFdbObservationStore(
                        connectionFactory),
                    new FdbObservationParser()),
                new ArpCollector(
                    transport,
                    rawStore,
                    new SqliteArpObservationStore(
                        connectionFactory),
                    new ArpObservationParser()),
                new SnmpHealthCollector(
                    transport),
                new SnmpInterfaceStatusCollector(
                    transport),
                stpCollector: stpCollector,
                observationDeviceBindingStore:
                    new SqliteObservationDeviceBindingStore(
                        connectionFactory),
                topologyMaterializer:
                    new MonitoringTopologyMaterializer(
                        topologyRepository),
                interfaceCounterBaselineStore:
                    new SqliteInterfaceCounterBaselineStore(
                        connectionFactory),
                interfaceDegradationPolicy:
                    interfaceDegradationPolicy,
                interfaceDegradationTransitionTracker:
                    new InterfaceDegradationTransitionTracker(
                        new SqliteInterfaceDegradationStateStore(
                            connectionFactory)));
        }
    }
}
