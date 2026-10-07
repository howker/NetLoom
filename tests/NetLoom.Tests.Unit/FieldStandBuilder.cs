using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Locations;
using NetLoom.Application.Monitoring;
using NetLoom.Application.Snmp;
using NetLoom.Application.Topology;
using NetLoom.Domain.Access;
using NetLoom.Persistence.Sqlite.Arp;
using NetLoom.Persistence.Sqlite.Cdp;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Fdb;
using NetLoom.Persistence.Sqlite.Lldp;
using NetLoom.Persistence.Sqlite.Locations;
using NetLoom.Persistence.Sqlite.Monitoring;
using NetLoom.Persistence.Sqlite.Observations;
using NetLoom.Persistence.Sqlite.Repositories;
using NetLoom.Persistence.Sqlite.Security;
using NetLoom.Persistence.Sqlite.Stp;
using NetLoom.Persistence.Sqlite.Topology;
using NetLoom.Protocols.Snmp.Arp;
using NetLoom.Protocols.Snmp.Cdp;
using NetLoom.Protocols.Snmp.Fdb;
using NetLoom.Protocols.Snmp.Health;
using NetLoom.Protocols.Snmp.Interfaces;
using NetLoom.Protocols.Snmp.Lldp;
using NetLoom.Protocols.Snmp.Stp;
using NetLoom.Topology.Materialization;

namespace NetLoom.Tests.Unit
{
    // Полевой стенд Sprint 46: синтетическая сеть объекта (~55 устройств, оптика, кольца, ручные элементы,
    // Оконечные устройства в FDB/ARP), построенная тем же конвейером, что и на объекте:
    // Настоящие сборщики SNMP, разборщики, хранилища и сборка топологии; подменён только сетевой транспорт.
    // Имена и адреса синтетические (TEST-NET, локально администрируемые MAC) — реальные данные сети запрещены.
    // Запуск вручную: dotnet test ... --filter "TestCategory=StandBuilder".
    // Результат: artifacts/realistic-stand/field-s46.db (в git не попадает).
    [TestClass]
    public sealed class FieldStandBuilder
    {
        private const string Community = "stand-community";

        [TestMethod]
        [TestCategory("StandBuilder")]
        public void BuildFieldStandDatabase()
        {
            var root =
                FindRepositoryRoot();

            var path =
                Path.Combine(
                    root,
                    "artifacts",
                    "realistic-stand",
                    "field-s46.db");

            Directory.CreateDirectory(
                Path.GetDirectoryName(path));

            foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
            {
                if (File.Exists(path + suffix))
                {
                    File.Delete(path + suffix);
                }
            }

            var network =
                SimNetwork.Create();

            var summary =
                Build(
                    path,
                    network);

            System.Data.SQLite.SQLiteConnection.ClearAllPools();

            File.WriteAllText(
                Path.Combine(
                    root,
                    "artifacts",
                    "realistic-stand",
                    "field-s46-summary.txt"),
                summary,
                new UTF8Encoding(false));
        }

        private static string Build(
            string path,
            SimNetwork network)
        {
            var factory =
                new SqliteConnectionFactory(path);

            new DatabaseInitializer(factory)
                .Initialize();

            var topology =
                new SqliteMaterializedTopologyRepository(factory);
            var locations =
                new SqliteLocationRepository(factory);
            var locationService =
                new LocationTopologyService(
                    locations,
                    topology);

            // 1. Профиль доступа — как оператор заводит его в «Настройках».
            new AccessProfileProvisioningService(
                    new AccessProfileRepository(factory),
                    new SecretRepository(
                        factory,
                        new DpapiSecretProtector()))
                .CreateCommunityProfile(
                    "Площадка А",
                    SnmpVersion.V2C,
                    Encoding.UTF8.GetBytes(Community));

            // 2. Размещения.
            var locationIds =
                new Dictionary<string, Guid>(StringComparer.Ordinal);

            foreach (var place in network.Places)
            {
                locationIds[place.Key] =
                    locationService.CreateLocation(
                        place.ParentKey == null
                            ? (Guid?)null
                            : locationIds[place.ParentKey],
                        place.Name,
                        place.Description);
            }

            var transport =
                new SimSnmpTransport(network);

            var now =
                DateTime.UtcNow;

            // 2a. Обнаружение неделю назад — как на объекте: «Начать обнаружение» по диапазонам площадки.
            // Устройство получает имя из sysName и свой DeviceId; опрос дальше идёт по этим устройствам.
            var discovery =
                new DiscoveryCandidateTopologyMaterializer(topology);

            foreach (var device in network.Devices.Where(item => item.Managed))
            {
                device.Id =
                    discovery.Materialize(
                        new NetLoom.Application.DiscoveryControl.DiscoveryCandidateSnapshot(
                            IPAddress.Parse(device.Address),
                            null,
                            true,
                            true,
                            new[] { 22, 80 },
                            device.Name,
                            device.SysDescr,
                            "1.3.6.1.4.1.8691",
                            "Площадка А",
                            device.Ports.Count),
                        now.AddDays(-7));
            }

            // 3. Опрос, который было бы видно три дня назад: устройства, недоступные сейчас.
            var stale =
                network.Devices
                    .Where(item => item.Scenario == DeviceScenario.UnreachableNow)
                    .ToList();

            var pastRuntime =
                CreateRuntime(
                    factory,
                    topology,
                    transport,
                    () => now.AddDays(-3));

            foreach (var device in stale)
            {
                device.Reachable = true;

                pastRuntime.PollOnce(
                    Request(
                        device,
                        MonitoringPollKind.Health,
                        MonitoringPollKind.Interface));

                device.Reachable = false;
            }

            // 4. Два цикла опроса: первый с исправным кольцом ПС-2, второй — после обрыва участка.
            var runtime =
                CreateRuntime(
                    factory,
                    topology,
                    transport,
                    () => DateTime.UtcNow);

            var allKinds =
                new[]
                {
                    MonitoringPollKind.Health,
                    MonitoringPollKind.Interface,
                    MonitoringPollKind.Lldp,
                    MonitoringPollKind.Cdp,
                    MonitoringPollKind.Fdb,
                    MonitoringPollKind.Arp,
                    MonitoringPollKind.Stp
                };

            var failures =
                new List<string>();

            for (var cycle = 0; cycle < 2; cycle++)
            {
                if (cycle == 1)
                {
                    network.BreakRingSegment();
                }

                foreach (var device in network.Devices.Where(item => item.Managed))
                {
                    var result =
                        runtime.PollOnce(
                            Request(
                                device,
                                allKinds));

                    foreach (var step in result.Steps.Where(item => !item.Succeeded))
                    {
                        failures.Add(
                            "цикл " + (cycle + 1) + ": " + device.Name + " " + step.Kind + " — " + step.ErrorType + " " + step.ErrorMessage);
                    }
                }
            }

            // 5. Привязка к размещениям — как оператор делает это в редакторе размещений.
            foreach (var device in network.Devices.Where(item => item.Managed && item.PlaceKey != null))
            {
                if (topology.GetDevice(device.Id) != null)
                {
                    locationService.AssignDevice(
                        device.Id,
                        locationIds[device.PlaceKey]);
                }
            }

            // 6. Ручные элементы — медиаконвертеры и неуправляемые коммутаторы на оптической трассе.
            var manual =
                new ManualTopologyService(
                    topology,
                    new SqliteManualTopologyAuditStore(factory));

            var manualIds =
                new Dictionary<string, Guid>(StringComparer.Ordinal);

            foreach (var item in network.ManualDevices)
            {
                var id =
                    manual.CreateDevice(
                        item.Name,
                        item.Category,
                        item.Notes);

                manualIds[item.Name] = id;

                locationService.AssignDevice(
                    id,
                    locationIds[item.PlaceKey]);
            }

            foreach (var link in network.ManualLinks)
            {
                var deviceA =
                    ResolveDevice(network, manualIds, link.DeviceA);
                var deviceB =
                    ResolveDevice(network, manualIds, link.DeviceB);

                var portA =
                    ResolveOrCreatePort(manual, topology, deviceA, link.PortA, link.MediaType, manualIds.ContainsValue(deviceA));
                var portB =
                    ResolveOrCreatePort(manual, topology, deviceB, link.PortB, link.MediaType, manualIds.ContainsValue(deviceB));

                manual.CreateLink(
                    deviceA,
                    portA,
                    deviceB,
                    portB,
                    link.MediaType,
                    link.Notes);
            }

            var builder =
                new StringBuilder();

            builder.AppendLine("Полевой стенд Sprint 46 — " + now.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
            builder.AppendLine("Устройств в топологии: " + topology.GetDevices().Count);
            builder.AppendLine("Портов: " + topology.GetInterfaces().Count);
            builder.AppendLine("Физических связей: " + topology.GetPhysicalLinks().Count);
            builder.AppendLine("Оснований связей: " + topology.GetPhysicalLinkEvidence().Count);
            builder.AppendLine("Оконечных устройств в FDB/ARP: " + network.Endpoints.Count);
            builder.AppendLine("Сценарии:");

            foreach (var line in network.ScenarioNotes)
            {
                builder.AppendLine("  - " + line);
            }

            builder.AppendLine("Ошибки шагов опроса (ожидаемые — у недоступных устройств):");

            foreach (var line in failures)
            {
                builder.AppendLine("  - " + line);
            }

            return builder.ToString();
        }

        private static Guid ResolveDevice(
            SimNetwork network,
            Dictionary<string, Guid> manualIds,
            string name)
        {
            Guid id;

            if (manualIds.TryGetValue(name, out id))
            {
                return id;
            }

            return network.Devices.Single(item => item.Name == name).Id;
        }

        private static Guid? ResolveOrCreatePort(
            ManualTopologyService manual,
            SqliteMaterializedTopologyRepository topology,
            Guid deviceId,
            string portName,
            string mediaType,
            bool isManualDevice)
        {
            if (string.IsNullOrEmpty(portName))
            {
                return null;
            }

            if (isManualDevice)
            {
                return manual.CreatePort(
                    deviceId,
                    portName,
                    mediaType);
            }

            var existing =
                topology.GetInterfaces()
                    .FirstOrDefault(
                        item =>
                            item.DeviceId == deviceId &&
                            string.Equals(item.IfName, portName, StringComparison.Ordinal));

            return existing == null
                ? (Guid?)null
                : existing.Id;
        }

        private static MonitoringRuntime CreateRuntime(
            SqliteConnectionFactory factory,
            SqliteMaterializedTopologyRepository topology,
            ISnmpTransport transport,
            Func<DateTime> utcNow)
        {
            var rawStore =
                new SqliteObservationStore(factory);

            return new MonitoringRuntime(
                new LldpCollector(
                    transport,
                    rawStore,
                    new SqliteLldpObservationStore(factory),
                    new LldpObservationParser()),
                new CdpCollector(
                    transport,
                    rawStore,
                    new SqliteCdpObservationStore(factory),
                    new CdpObservationParser()),
                new FdbCollector(
                    transport,
                    rawStore,
                    new SqliteFdbObservationStore(factory),
                    new FdbObservationParser()),
                new ArpCollector(
                    transport,
                    rawStore,
                    new SqliteArpObservationStore(factory),
                    new ArpObservationParser()),
                new SnmpHealthCollector(
                    transport,
                    utcNow),
                new SnmpInterfaceStatusCollector(
                    transport,
                    utcNow),
                utcNow,
                stpCollector:
                    new StpCollector(
                        transport,
                        rawStore,
                        new SqliteStpObservationStore(factory),
                        new StpObservationParser(),
                        utcNow),
                observationDeviceBindingStore:
                    new SqliteObservationDeviceBindingStore(factory),
                topologyMaterializer:
                    new MonitoringTopologyMaterializer(topology),
                interfaceCounterBaselineStore:
                    new SqliteInterfaceCounterBaselineStore(factory),
                interfaceDegradationTransitionProcessor:
                    new SqliteInterfaceDegradationTransitionProcessor(factory));
        }

        private static MonitoringPollRequest Request(
            SimDevice device,
            params MonitoringPollKind[] kinds)
        {
            return new MonitoringPollRequest(
                IPAddress.Parse(device.Address),
                161,
                SnmpVersion.V2C,
                new SnmpCommunityCredentials(
                    Encoding.UTF8.GetBytes(Community)),
                500,
                0,
                20,
                kinds,
                device.Id);
        }

        private static string FindRepositoryRoot()
        {
            var directory =
                new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

            while (directory != null &&
                   !File.Exists(Path.Combine(directory.FullName, "NetLoom.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ??
                   throw new InvalidOperationException("Repository root not found.");
        }

        // ---------- модель сети ----------

        internal enum DeviceScenario
        {
            Normal,
            UnreachableNow,
            LldpDisabled
        }

        internal sealed class SimPlace
        {
            public string Key;
            public string ParentKey;
            public string Name;
            public string Description;
        }

        internal sealed class SimPort
        {
            public int IfIndex;
            public string Name;
            public string Description;
            public string Alias;
            public bool Fiber;
            public bool Up = true;
            public bool AdminUp = true;
            public int StpState = 5;
            public SimDevice PeerDevice;
            public SimPort PeerPort;
            public readonly List<string> LearnedMacs = new List<string>();
        }

        internal sealed class SimDevice
        {
            public Guid Id;
            public string Name;
            public string Address;
            public byte[] Mac;
            public string SysDescr;
            public string PlaceKey;
            public bool Managed = true;
            public bool Reachable = true;
            public bool IsSwitch;
            public bool IsRouter;
            public int BridgePriority = 32768;
            public DeviceScenario Scenario;
            public readonly List<SimPort> Ports = new List<SimPort>();
            public readonly List<SimEndpoint> Arp = new List<SimEndpoint>();

            public SimPort Port(int ifIndex)
            {
                return Ports.Single(item => item.IfIndex == ifIndex);
            }
        }

        internal sealed class SimEndpoint
        {
            public string Name;
            public string Mac;
            public string Ip;
        }

        internal sealed class SimManualDevice
        {
            public string Name;
            public ManualTopologyDeviceCategory Category;
            public string Notes;
            public string PlaceKey;
        }

        internal sealed class SimManualLink
        {
            public string DeviceA;
            public string PortA;
            public string DeviceB;
            public string PortB;
            public string MediaType;
            public string Notes;
        }

        internal sealed class SimNetwork
        {
            public readonly List<SimPlace> Places = new List<SimPlace>();
            public readonly List<SimDevice> Devices = new List<SimDevice>();
            public readonly List<SimEndpoint> Endpoints = new List<SimEndpoint>();
            public readonly List<SimManualDevice> ManualDevices = new List<SimManualDevice>();
            public readonly List<SimManualLink> ManualLinks = new List<SimManualLink>();
            public readonly List<string> ScenarioNotes = new List<string>();

            private SimPort _ring2Blocked;
            private SimPort _breakA;
            private SimPort _breakB;
            private int _macSeed = 0x10;
            private int _guidSeed = 1;

            public SimDevice Find(string name)
            {
                return Devices.Single(item => item.Name == name);
            }

            public static SimNetwork Create()
            {
                var n = new SimNetwork();

                n.Place("site", null, "Площадка А", "Синтетический объект полевого стенда");
                n.Place("ps1", "site", "ПС-1", "Подстанция 1");
                n.Place("ps1-srv", "ps1", "Серверная", "Ядро сети и серверы");
                n.Place("ps1-sh", "ps1", "Щитовая", "Кольцо ПС-1");
                n.Place("ps2", "site", "ПС-2", "Подстанция 2");
                n.Place("ps2-app", "ps2", "Аппаратная", "Кольцо ПС-2");
                n.Place("kb", "site", "Корпус Б", "Административный корпус");
                n.Place("kb-uz", "kb", "Узел связи", "Доступ корпуса Б");

                // Ядро: два коммутатора, маршрутизатор, серверы и ИБП.
                var core1 = n.Switch("core-sw-01", "192.0.2.11", "MOXA PT-7728 Layer 3 Ethernet switch, firmware V3.9", "ps1-srv", 28, 4, 4096);
                SimSnmpTransport.SetRoot(core1.Mac);
                var core2 = n.Switch("core-sw-02", "192.0.2.12", "MOXA PT-7728 Layer 3 Ethernet switch, firmware V3.9", "ps1-srv", 28, 4, 8192);
                var gw = n.Router("gw-01", "192.0.2.1", "RouterOS RB4011iGS+ 7.14", "ps1-srv");

                n.Cable(core1, 25, core2, 25);
                n.Cable(core1, 26, core2, 26);
                core2.Port(26).StpState = 2;
                n.Cable(gw, 1, core1, 1);

                var servers = new[]
                {
                    n.Host("srv-scada-01", "203.0.113.21", "Hardware: Intel64 Family 6 - Software: Windows Server 2019", "ps1-srv"),
                    n.Host("srv-hist-01", "203.0.113.22", "Hardware: Intel64 Family 6 - Software: Windows Server 2019", "ps1-srv"),
                    n.Host("srv-nms-01", "203.0.113.23", "Linux srv-nms-01 5.15.0 x86_64", "ps1-srv")
                };

                for (var i = 0; i < servers.Length; i++)
                {
                    // Серверы без LLDP: связь не наблюдается, только MAC в FDB коммутатора ядра.
                    core2.Port(2 + i).LearnedMacs.Add(MacText(servers[i].Mac));
                    gw.Arp.Add(new SimEndpoint { Name = servers[i].Name, Mac = MacText(servers[i].Mac), Ip = servers[i].Address });
                }

                for (var i = 1; i <= 6; i++)
                {
                    var ups = n.Host("ups-" + (i <= 3 ? "ps1" : "ps2") + "-0" + ((i - 1) % 3 + 1), "198.51.100." + (200 + i), "APC Smart-UPS SRT 3000 Network Management Card AOS v7", i <= 3 ? "ps1-sh" : "ps2-app");
                    gw.Arp.Add(new SimEndpoint { Name = ups.Name, Mac = MacText(ups.Mac), Ip = ups.Address });
                }

                // Кольцо ПС-1: core-sw-01 G1/1 → 8 коммутаторов → core-sw-02 G1/1; STP блокирует один порт.
                var ring1 = n.Ring("ps1-sw", "198.51.100.", 10, 8, "MOXA EDS-408A-MM-SC, firmware V3.8", "ps1-sh", core1, 27, core2, 27);
                ring1[4].Port(8).StpState = 2;
                n.ScenarioNotes.Add("Ядро: два параллельных кабеля core-sw-01 ↔ core-sw-02, STP штатно блокирует порт 26 core-sw-02");
                n.ScenarioNotes.Add("Кольцо ПС-1 замкнуто на два ядра (общий регион с параллельными кабелями ядра)");
                n.ScenarioNotes.Add("Кольцо ПС-1 исправно, STP штатно блокирует ps1-sw-05 F2 (порт 8)");

                // Кольцо ПС-2: замкнуто на один core-sw-01 (G1/4 и Port 24) — простое кольцо; во втором цикле обрывается участок 03–04.
                var ring2 = n.Ring("ps2-sw", "198.51.100.", 40, 8, "MOXA EDS-408A-SS-SC, firmware V3.8", "ps2-app", core1, 28, core1, 24);
                ring2[5].Port(8).StpState = 2;
                n._ring2Blocked = ring2[5].Port(8);
                n._breakA = ring2[2].Port(8);
                n._breakB = ring2[3].Port(7);
                n.ScenarioNotes.Add("Кольцо ПС-2: в первом цикле исправно, во втором — оборван участок ps2-sw-03 F2 ↔ ps2-sw-04 F1");

                // Корпус Б: цепочка коммутаторов доступа через медиаконвертеры на оптике; один без LLDP, один недоступен.
                var kb = new List<SimDevice>();

                for (var i = 1; i <= 6; i++)
                {
                    kb.Add(n.Switch("kb-sw-0" + i, "198.51.100." + (70 + i), "MOXA EDS-518A Managed Ethernet switch, firmware V3.6", "kb-uz", 18, 2, 32768));
                }

                for (var i = 0; i < kb.Count - 1; i++)
                {
                    n.Cable(kb[i], 17, kb[i + 1], 18);
                }

                kb[3].Scenario = DeviceScenario.LldpDisabled;
                kb[5].Scenario = DeviceScenario.UnreachableNow;
                n.ScenarioNotes.Add("kb-sw-04 с отключённым LLDP: kb-sw-03 видит его, обратной стороны нет");
                n.ScenarioNotes.Add("kb-sw-06 недоступен: последние данные три дня назад, сейчас таймаут");

                // Оптическая трасса ядро → корпус Б через ручные медиаконвертеры.
                n.ManualDevices.Add(new SimManualDevice { Name = "МК-1 Серверная", Category = ManualTopologyDeviceCategory.MediaConverter, Notes = "Медиаконвертер 1000BASE-LX, кросс ШКОС-1", PlaceKey = "ps1-srv" });
                n.ManualDevices.Add(new SimManualDevice { Name = "МК-2 Корпус Б", Category = ManualTopologyDeviceCategory.MediaConverter, Notes = "Медиаконвертер 1000BASE-LX, кросс ШКОС-7", PlaceKey = "kb-uz" });
                n.ManualDevices.Add(new SimManualDevice { Name = "МК-3 Корпус Б", Category = ManualTopologyDeviceCategory.MediaConverter, Notes = "Резервный, не подключён", PlaceKey = "kb-uz" });
                n.ManualDevices.Add(new SimManualDevice { Name = "МК-4 ПС-2", Category = ManualTopologyDeviceCategory.OpticalConverter, Notes = "Оптический конвертер для видеонаблюдения", PlaceKey = "ps2-app" });
                n.ManualDevices.Add(new SimManualDevice { Name = "Неуправляемый 8 портов, пост охраны", Category = ManualTopologyDeviceCategory.UnmanagedSwitch, Notes = "D-Link DES-1008, камеры КПП", PlaceKey = "kb-uz" });
                n.ManualDevices.Add(new SimManualDevice { Name = "Неуправляемый 5 портов, щитовая", Category = ManualTopologyDeviceCategory.UnmanagedSwitch, Notes = "Временный, демонтировать", PlaceKey = "ps1-sh" });

                n.ManualLinks.Add(new SimManualLink { DeviceA = "core-sw-02", PortA = "Port 24", DeviceB = "МК-1 Серверная", PortB = "TX", MediaType = "Copper", Notes = "Оптика ядро → корпус Б" });
                n.ManualLinks.Add(new SimManualLink { DeviceA = "МК-1 Серверная", PortA = "LX", DeviceB = "МК-2 Корпус Б", PortB = "LX", MediaType = "Fiber", Notes = "Магистраль 1.2 км" });
                n.ManualLinks.Add(new SimManualLink { DeviceA = "МК-2 Корпус Б", PortA = "TX", DeviceB = "kb-sw-01", PortB = "Port 18", MediaType = "Copper", Notes = null });
                n.ManualLinks.Add(new SimManualLink { DeviceA = "kb-sw-02", PortA = "Port 1", DeviceB = "Неуправляемый 8 портов, пост охраны", PortB = "Uplink", MediaType = "Copper", Notes = "Камеры КПП" });
                n.ManualLinks.Add(new SimManualLink { DeviceA = "ps2-sw-08", PortA = "Port 1", DeviceB = "МК-4 ПС-2", PortB = "TX", MediaType = "Copper", Notes = null });
                n.ScenarioNotes.Add("Ручные элементы: 4 медиаконвертера и 2 неуправляемых коммутатора, 5 ручных связей");

                // Оконечные устройства: камеры и ПЛК на портах доступа; MAC в FDB, IP в ARP маршрутизатора.
                var access =
                    ring1.Concat(ring2).Concat(kb.Take(5)).ToList();

                var endpointIndex = 0;

                foreach (var sw in access)
                {
                    for (var port = 2; port <= 5; port++)
                    {
                        if (endpointIndex >= 64)
                        {
                            break;
                        }

                        endpointIndex++;

                        var isCamera = endpointIndex % 3 != 0;
                        var mac = n.NextMac();
                        var ip = "198.51.100." + (100 + endpointIndex);

                        var endpoint =
                            new SimEndpoint
                            {
                                Name = (isCamera ? "Камера " : "ПЛК ") + endpointIndex,
                                Mac = MacText(mac),
                                Ip = ip
                            };

                        n.Endpoints.Add(endpoint);
                        sw.Port(port).LearnedMacs.Add(endpoint.Mac);
                        gw.Arp.Add(endpoint);
                    }
                }

                n.ScenarioNotes.Add("Оконечных устройств: " + n.Endpoints.Count + " (MAC в FDB коммутаторов доступа, IP в ARP gw-01)");
                n.ScenarioNotes.Add("Профиль доступа: «Площадка А», SNMP v2c");

                return n;
            }

            public void BreakRingSegment()
            {
                _breakA.Up = false;
                _breakB.Up = false;
                _ring2Blocked.StpState = 5;
            }

            private void Place(string key, string parent, string name, string description)
            {
                Places.Add(new SimPlace { Key = key, ParentKey = parent, Name = name, Description = description });
            }

            private SimDevice Switch(string name, string address, string sysDescr, string place, int ports, int fiberPorts, int priority)
            {
                var device = Device(name, address, sysDescr, place);
                device.IsSwitch = true;
                device.BridgePriority = priority;

                for (var i = 1; i <= ports; i++)
                {
                    var fiber = i > ports - fiberPorts;

                    device.Ports.Add(
                        new SimPort
                        {
                            IfIndex = i,
                            Name = fiber
                                ? (ports >= 24 ? "G1/" + (i - (ports - fiberPorts)) : "F" + (i - (ports - fiberPorts)))
                                : (ports >= 24 ? "Port " + i : "Port " + i),
                            Description = fiber ? "1000BASE-LX SFP" : "10/100/1000BASE-TX",
                            Fiber = fiber
                        });
                }

                return device;
            }

            private SimDevice Router(string name, string address, string sysDescr, string place)
            {
                var device = Device(name, address, sysDescr, place);
                device.IsRouter = true;

                for (var i = 1; i <= 10; i++)
                {
                    device.Ports.Add(new SimPort { IfIndex = i, Name = i <= 8 ? "ether" + i : "sfp-sfpplus" + (i - 8), Description = i <= 8 ? "ether" + i : "sfp-sfpplus" + (i - 8), Fiber = i > 8 });
                }

                return device;
            }

            private SimDevice Host(string name, string address, string sysDescr, string place)
            {
                var device = Device(name, address, sysDescr, place);
                device.Ports.Add(new SimPort { IfIndex = 1, Name = "Ethernet0", Description = "Ethernet adapter", Alias = "LAN" });
                return device;
            }

            private SimDevice Device(string name, string address, string sysDescr, string place)
            {
                var device =
                    new SimDevice
                    {
                        Id = new Guid("5e46f1e1-0000-4000-8000-" + (_guidSeed++).ToString("x12", CultureInfo.InvariantCulture)),
                        Name = name,
                        Address = address,
                        Mac = NextMac(),
                        SysDescr = sysDescr,
                        PlaceKey = place
                    };

                Devices.Add(device);
                return device;
            }

            private List<SimDevice> Ring(string prefix, string net, int firstHost, int count, string sysDescr, string place, SimDevice left, int leftPort, SimDevice right, int rightPort)
            {
                var ring = new List<SimDevice>();

                for (var i = 1; i <= count; i++)
                {
                    // EDS-408A: порты 1–6 медь, 7–8 оптика (F1, F2).
                    ring.Add(Switch(prefix + "-0" + i, net + (firstHost + i), sysDescr, place, 8, 2, 32768));
                }

                Cable(left, leftPort, ring[0], 7);

                for (var i = 0; i < ring.Count - 1; i++)
                {
                    Cable(ring[i], 8, ring[i + 1], 7);
                }

                Cable(ring[ring.Count - 1], 8, right, rightPort);
                return ring;
            }

            private void Cable(SimDevice a, int portA, SimDevice b, int portB)
            {
                var pa = a.Port(portA);
                var pb = b.Port(portB);
                pa.PeerDevice = b;
                pa.PeerPort = pb;
                pb.PeerDevice = a;
                pb.PeerPort = pa;
            }

            public byte[] NextMac()
            {
                var seed = _macSeed++;
                return new byte[] { 0x02, 0x4e, 0x4c, (byte)(seed >> 16), (byte)(seed >> 8), (byte)seed };
            }
        }

        internal static string MacText(byte[] mac)
        {
            return string.Join(":", mac.Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
        }

        // ---------- поддельный транспорт SNMP: отвечает по модели сети ----------

        internal sealed class SimSnmpTransport : ISnmpTransport
        {
            private readonly SimNetwork _network;

            public SimSnmpTransport(SimNetwork network)
            {
                _network = network;
            }

            public IReadOnlyList<SnmpVariable> Get(SnmpGetRequest request)
            {
                var table = Table(request.Address.ToString());
                return request.Oids
                    .Where(table.ContainsKey)
                    .Select(oid => table[oid])
                    .ToList();
            }

            public IReadOnlyList<SnmpVariable> Walk(SnmpWalkRequest request)
            {
                var table = Table(request.Address.ToString());
                var prefix = request.RootOid + ".";

                return table
                    .Where(item => item.Key.StartsWith(prefix, StringComparison.Ordinal))
                    .OrderBy(item => item.Key, OidComparer.Instance)
                    .Select(item => item.Value)
                    .ToList();
            }

            private Dictionary<string, SnmpVariable> Table(string address)
            {
                var device =
                    _network.Devices.SingleOrDefault(item => item.Address == address);

                if (device == null || !device.Reachable)
                {
                    throw new SnmpTransportException(
                        SnmpTransportFailure.Timeout,
                        "Simulated timeout for " + address,
                        null);
                }

                var t = new Dictionary<string, SnmpVariable>(StringComparer.Ordinal);

                Str(t, "1.3.6.1.2.1.1.1.0", device.SysDescr);
                Num(t, "1.3.6.1.2.1.1.3.0", 67, "8640000");
                Str(t, "1.3.6.1.2.1.1.5.0", device.Name);
                Str(t, "1.3.6.1.2.1.1.6.0", "Площадка А");

                foreach (var port in device.Ports)
                {
                    var i = port.IfIndex.ToString(CultureInfo.InvariantCulture);
                    var up = port.PeerPort == null ? port.Up : LinkUp(port);
                    Num(t, "1.3.6.1.2.1.2.2.1.1." + i, 2, i);
                    Str(t, "1.3.6.1.2.1.2.2.1.2." + i, port.Description + " " + port.Name);
                    Num(t, "1.3.6.1.2.1.2.2.1.3." + i, 2, "6");
                    Bytes(t, "1.3.6.1.2.1.2.2.1.6." + i, PortMac(device, port));
                    Num(t, "1.3.6.1.2.1.2.2.1.7." + i, 2, port.AdminUp ? "1" : "2");
                    Num(t, "1.3.6.1.2.1.2.2.1.8." + i, 2, up && (port.PeerPort != null || port.LearnedMacs.Count > 0 || !device.IsSwitch) ? "1" : "2");
                    Num(t, "1.3.6.1.2.1.2.2.1.13." + i, 65, "0");
                    Num(t, "1.3.6.1.2.1.2.2.1.14." + i, 65, "0");
                    Num(t, "1.3.6.1.2.1.2.2.1.19." + i, 65, "0");
                    Num(t, "1.3.6.1.2.1.2.2.1.20." + i, 65, "0");
                    Str(t, "1.3.6.1.2.1.31.1.1.1.1." + i, port.Name);
                    Num(t, "1.3.6.1.2.1.31.1.1.1.15." + i, 66, port.Fiber ? "1000" : "100");
                    Str(t, "1.3.6.1.2.1.31.1.1.1.18." + i, port.Alias ?? (port.PeerDevice != null ? "к " + port.PeerDevice.Name : string.Empty));
                    Num(t, "1.3.6.1.2.1.31.1.1.1.19." + i, 67, "0");
                }

                if (device.IsSwitch || device.IsRouter)
                {
                    Lldp(t, device);
                }

                if (device.IsSwitch)
                {
                    Bridge(t, device);
                }

                if (device.IsRouter)
                {
                    foreach (var entry in device.Arp)
                    {
                        var index = "1." + entry.Ip;
                        Bytes(t, "1.3.6.1.2.1.4.22.1.2." + index, ParseMac(entry.Mac));
                        Num(t, "1.3.6.1.2.1.4.22.1.4." + index, 2, "3");
                    }
                }

                return t;
            }

            // Связь поднята, только если исправны оба порта и сосед жив: недоступное устройство считается выключенным.
            private static bool LinkUp(SimPort port)
            {
                return port.Up &&
                       port.PeerPort != null &&
                       port.PeerPort.Up &&
                       port.PeerDevice.Reachable;
            }

            private static void Lldp(Dictionary<string, SnmpVariable> t, SimDevice device)
            {
                if (device.Scenario == DeviceScenario.LldpDisabled)
                {
                    return;
                }

                Num(t, "1.0.8802.1.1.2.1.3.1.0", 2, "4");
                Bytes(t, "1.0.8802.1.1.2.1.3.2.0", device.Mac);
                Str(t, "1.0.8802.1.1.2.1.3.3.0", device.Name);

                foreach (var port in device.Ports)
                {
                    var p = port.IfIndex.ToString(CultureInfo.InvariantCulture);
                    Num(t, "1.0.8802.1.1.2.1.3.7.1.2." + p, 2, "5");
                    Str(t, "1.0.8802.1.1.2.1.3.7.1.3." + p, port.Name);
                    Str(t, "1.0.8802.1.1.2.1.3.7.1.4." + p, port.Description);

                    var peer = port.PeerDevice;

                    if (peer == null ||
                        !LinkUp(port) ||
                        peer.Scenario == DeviceScenario.LldpDisabled ||
                        !(peer.IsSwitch || peer.IsRouter))
                    {
                        continue;
                    }

                    var index = "0." + p + ".1";
                    Num(t, "1.0.8802.1.1.2.1.4.1.1.4." + index, 2, "4");
                    Bytes(t, "1.0.8802.1.1.2.1.4.1.1.5." + index, peer.Mac);
                    Num(t, "1.0.8802.1.1.2.1.4.1.1.6." + index, 2, "5");
                    Str(t, "1.0.8802.1.1.2.1.4.1.1.7." + index, port.PeerPort.Name);
                    Str(t, "1.0.8802.1.1.2.1.4.1.1.8." + index, port.PeerPort.Description);
                    Str(t, "1.0.8802.1.1.2.1.4.1.1.9." + index, peer.Name);
                    Str(t, "1.0.8802.1.1.2.1.4.1.1.10." + index, peer.SysDescr);

                    // Возможности системы (LLDP-MIB BITS): мост — 0x20, маршрутизатор — 0x08.
                    var capabilities = new byte[] { (byte)(peer.IsRouter ? 0x08 : 0x20), 0x00 };
                    Bytes(t, "1.0.8802.1.1.2.1.4.1.1.11." + index, capabilities);
                    Bytes(t, "1.0.8802.1.1.2.1.4.1.1.12." + index, capabilities);
                }
            }

            private static void Bridge(Dictionary<string, SnmpVariable> t, SimDevice device)
            {
                var bridgeId = BridgeId(device.BridgePriority, device.Mac);
                Num(t, "1.3.6.1.2.1.17.2.1.0", 2, "3");
                Bytes(t, "1.3.6.1.2.1.17.2.5.0", BridgeId(4096, RootMac));
                Num(t, "1.3.6.1.2.1.17.2.6.0", 2, device.BridgePriority == 4096 ? "0" : "20000");
                Num(t, "1.3.6.1.2.1.17.2.7.0", 2, device.BridgePriority == 4096 ? "0" : RootPort(device).ToString(CultureInfo.InvariantCulture));

                foreach (var port in device.Ports)
                {
                    var bp = port.IfIndex.ToString(CultureInfo.InvariantCulture);
                    var up = port.PeerPort == null ? port.Up && port.LearnedMacs.Count > 0 : LinkUp(port);
                    Num(t, "1.3.6.1.2.1.17.1.4.1.2." + bp, 2, bp);
                    Num(t, "1.3.6.1.2.1.17.2.15.1.1." + bp, 2, bp);
                    Num(t, "1.3.6.1.2.1.17.2.15.1.2." + bp, 2, "128");
                    Num(t, "1.3.6.1.2.1.17.2.15.1.3." + bp, 2, up ? port.StpState.ToString(CultureInfo.InvariantCulture) : "1");
                    Num(t, "1.3.6.1.2.1.17.2.15.1.4." + bp, 2, "1");
                    Num(t, "1.3.6.1.2.1.17.2.15.1.5." + bp, 2, port.Fiber ? "20000" : "200000");
                    Bytes(t, "1.3.6.1.2.1.17.2.15.1.6." + bp, BridgeId(4096, RootMac));
                    Num(t, "1.3.6.1.2.1.17.2.15.1.7." + bp, 2, "0");
                    Bytes(t, "1.3.6.1.2.1.17.2.15.1.8." + bp, bridgeId);
                    Bytes(t, "1.3.6.1.2.1.17.2.15.1.9." + bp, new byte[] { 0x80, (byte)port.IfIndex });
                    Num(t, "1.3.6.1.2.1.17.2.15.1.10." + bp, 65, "1");

                    var macs = new List<string>(port.LearnedMacs);

                    if (port.PeerDevice != null && LinkUp(port))
                    {
                        macs.Add(MacText(port.PeerDevice.Mac));
                    }

                    foreach (var mac in macs)
                    {
                        var bytes = ParseMac(mac);
                        var index = string.Join(".", bytes.Select(item => item.ToString(CultureInfo.InvariantCulture)));
                        Bytes(t, "1.3.6.1.2.1.17.4.3.1.1." + index, bytes);
                        Num(t, "1.3.6.1.2.1.17.4.3.1.2." + index, 2, bp);
                        Num(t, "1.3.6.1.2.1.17.4.3.1.3." + index, 2, "3");
                    }
                }
            }

            private static byte[] RootMac;

            private static int RootPort(SimDevice device)
            {
                var uplink =
                    device.Ports.FirstOrDefault(item => item.Fiber && LinkUp(item) && item.StpState == 5);

                return uplink == null ? 0 : uplink.IfIndex;
            }

            internal static void SetRoot(byte[] mac)
            {
                RootMac = mac;
            }

            private static byte[] BridgeId(int priority, byte[] mac)
            {
                return new[] { (byte)(priority >> 8), (byte)priority }.Concat(mac ?? new byte[6]).ToArray();
            }

            private static byte[] PortMac(SimDevice device, SimPort port)
            {
                var mac = (byte[])device.Mac.Clone();
                mac[0] = 0x06;
                mac[5] = (byte)(mac[5] + port.IfIndex);
                return mac;
            }

            private static byte[] ParseMac(string text)
            {
                return text.Split(':').Select(item => byte.Parse(item, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray();
            }

            private static void Str(Dictionary<string, SnmpVariable> t, string oid, string value)
            {
                Bytes(t, oid, Encoding.UTF8.GetBytes(value ?? string.Empty), value ?? string.Empty);
            }

            private static void Bytes(Dictionary<string, SnmpVariable> t, string oid, byte[] payload, string display = null)
            {
                var encoded = new byte[payload.Length + 2];
                encoded[0] = 0x04;
                encoded[1] = (byte)payload.Length;
                Buffer.BlockCopy(payload, 0, encoded, 2, payload.Length);
                t[oid] = new SnmpVariable(oid, 4, display ?? string.Join(" ", payload.Select(item => item.ToString("X2", CultureInfo.InvariantCulture))), encoded);
            }

            private static void Num(Dictionary<string, SnmpVariable> t, string oid, int typeCode, string value)
            {
                t[oid] = new SnmpVariable(oid, typeCode, value, new byte[0]);
            }
        }

        private sealed class OidComparer : IComparer<string>
        {
            public static readonly OidComparer Instance = new OidComparer();

            public int Compare(string x, string y)
            {
                var a = x.Split('.');
                var b = y.Split('.');

                for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
                {
                    var compare = long.Parse(a[i], CultureInfo.InvariantCulture).CompareTo(long.Parse(b[i], CultureInfo.InvariantCulture));

                    if (compare != 0)
                    {
                        return compare;
                    }
                }

                return a.Length.CompareTo(b.Length);
            }
        }
    }
}
