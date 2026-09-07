# PROJECT_STATE

## Текущая версия

0.2-dev

## Текущее состояние

Sprint 0, Sprint 1 и Sprint 2 завершены.

Следующий этап: Sprint 3 — SNMP transport.

## Основа проекта

- решение из 13 проектов;
- платформенная база: shared core netstandard2.0; legacy Service/WPF net48; modern Engine net8.0 сейчас с целевым .NET 10 LTS;
- архитектура сборки x64;
- WPF-каркас;
- базовая тестовая инфраструктура MSTest;
- SQLite через System.Data.SQLite;
- native SQLite через SourceGear.sqlite3;
- миграции схемы с таблицей schema_migrations;
- включён PRAGMA foreign_keys = ON.

## Sprint 1 — SQLite и миграции

Реализованы:
- SqliteConnectionFactory;
- DatabaseInitializer;
- MigrationRunner;
- schema_migrations;
- app_settings;
- транзакционное применение миграций;
- защита от повторной версии миграции;
- rollback ошибочной миграции;
- повторная инициализация базы без повторного применения миграций.

## Sprint 2 — AccessProfile и секреты

Реализованы:
- модель AccessProfile;
- SNMP версии v1/v2c/v3;
- ISecretProtector;
- DPAPI-защита секретов с DataProtectionScope.CurrentUser;
- таблицы access_profiles, secrets, access_profile_targets, access_profile_exclusions, access_profile_tcp_ports;
- AccessProfileRepository;
- SecretRepository;
- AccessProfileScopeRepository;
- каскадное удаление связанных данных;
- проверка диапазона TCP-портов;
- integration-тесты, подтверждающие отсутствие plaintext-секрета в SQLite.

## Известные ограничения

- SNMP transport ещё не реализован;
- discovery ещё не реализован;
- сбор инвентаря ещё не реализован;
- построение физической топологии ещё не реализовано;
- карта и мониторинг ещё не реализованы;
- текущая сборка x64;
- DPAPI CurrentUser требует отдельного решения для сервисной учётной записи при переходе к Windows Service.

## Следующий шаг

Sprint 3: SNMP transport.

## Sprint 3 — SNMP transport завершен

Реализовано:
- транспортный контракт ISnmpTransport;
- SNMP GET для v1 и v2c;
- SNMPv3 USM;
- аутентификация MD5/SHA1;
- privacy DES/AES;
- discovery SNMPv3 engine parameters;
- обработка notInTimeWindow;
- timeout и retry;
- отдельные типы транспортных ошибок;
- секреты передаются в транспорт через отдельные credentials и не логируются;
- Lextm.SharpSnmpLib 12.5.7;
- transport-level тесты timeout/retry и credentials.

Следующий Sprint: Sprint 4 — Inventory Collector.
## Sprint 4 — Inventory Collector завершен

Реализовано:
- контракт IInventoryCollector;
- SNMP WALK для v1;
- SNMP BulkWalk для v2c/v3;
- сбор sysDescr, sysObjectID, sysUpTime, sysContact, sysName и sysLocation;
- сбор ifTable и ifXTable;
- нормализованный InventorySnapshot и InventoryInterface;
- management IP хранится только как адрес источника наблюдения и не используется как DeviceId;
- отсутствие ifXTable не делает устройство недоступным: используется доступная часть ifTable;
- SNMPv3 дополнен SHA-256/SHA-384/SHA-512 и AES-192/AES-256;
- MD5/SHA1/DES сохранены только как legacy-совместимость;
- unit-тесты inventory и fallback для старых устройств;
- сборка проходит без предупреждений компилятора.

Следующий Sprint: Sprint 5 — Discovery.

## Sprint 5 — Discovery завершен

Реализовано:
- модели DiscoveryRequest, DiscoveryTarget, DiscoveryCandidate и DiscoverySnmpProfile;
- DiscoveryEngine без записи в БД;
- ICMP используется только как дополнительный сигнал и не блокирует SNMP;
- открытые TCP-порты могут создавать кандидата без SNMP;
- исключения имеют приоритет и исключённые адреса не опрашиваются;
- IPv4 CIDR expander с ограничением максимального количества адресов;
- поддержка целей IpAddress, Cidr и Hostname;
- дедупликация адресов после раскрытия целей;
- SystemNetworkDiscoveryProbe для ICMP/TCP;
- SystemHostnameResolver для DNS;
- management IP остаётся адресом наблюдения и не используется как DeviceId;
- unit-тесты для ICMP/TCP, CIDR, DNS, exclusions и discovery-логики.

Следующий Sprint: Sprint 6 — Observation layer.

## Sprint 6 — Observation layer завершен

Реализовано:
- нейтральная доменная модель Observation;
- ObservationKind для SNMP inventory, LLDP, CDP, FDB, ARP, Health и STP;
- Application-контракт IObservationStore;
- SnmpObservation с сохранением raw SNMP varbinds;
- Migration003Observations;
- таблицы observations и snmp_varbinds;
- индексы по времени, источнику/типу и OID;
- атомарное сохранение observation + varbinds;
- восстановление raw varbinds без потери порядка и encoded value;
- удаление observation каскадно удаляет его varbinds;
- source_address является адресом источника наблюдения, а не DeviceId;
- Observation layer не создаёт Device или Link автоматически;
- integration-тесты хранения и migration 003.

Следующий Sprint: Sprint 7 — LLDP.
## Sprint 7 — LLDP завершен

Реализовано:
- доменная модель LLDP-наблюдений: локальные порты и удалённые соседи;
- парсер стандартного LLDP-MIB из raw SNMP varbinds;
- корректная обработка составного индекса lldpRemTimeMark + lldpRemLocalPortNum + lldpRemIndex;
- lldpRemLocalPortNum и lldpLocPortNum не трактуются как ifIndex;
- malformed LLDP rows игнорируются без создания вымышленных соседей;
- LLDP collector выполняет WALK локальной и удалённой таблиц;
- raw varbinds сохраняются до нормализованной обработки;
- один observation_id сохраняется между raw и normalized LLDP;
- Migration004LldpObservations и таблица lldp_observations;
- normalized LLDP-наблюдения сохраняются отдельно от физических связей;
- unit и integration тесты LLDP pipeline.

Следующий Sprint: Sprint 8 — CDP.

## Sprint 7.5 — Cross-platform readiness завершен

- Shared core переведён на `netstandard2.0`.
- SNMP/SQLite adapters переведены на `net48;net8.0`.
- Добавлен `NetLoom.Engine` (`net8.0`) и подтверждён `linux-x64` publish.
- Legacy `net48` build/tests сохранены без предупреждений.
- `NetLoom.Service`/`NetLoom.Wpf` остаются legacy Windows host/client.
- IPC объявлен transport-neutral; Named Pipes больше не является обязательной границей.
- Go не используется для основного Engine; возможен только отдельный будущий Probe.
- ТЗ и Приложение к ТЗ синхронизированы с новым решением.

Целевой production modern runtime: .NET 10 LTS после контролируемого обновления SDK/toolchain.

Следующий Sprint: Sprint 8 — CDP.

## Sprint 8 — CDP завершен

Реализовано:
- модель CDP-наблюдений;
- parser CISCO-CDP-MIB из raw SNMP varbinds;
- составной индекс cdpCacheIfIndex + cdpCacheDeviceIndex;
- cdpCacheIfIndex сохраняется как наблюдаемое значение и не превращается автоматически в InterfaceId;
- CDP collector выполняет WALK cdpCacheEntry;
- raw Observation сохраняется до нормализованных CDP-данных;
- raw и normalized представления используют один observation_id;
- Migration005CdpObservations;
- таблица cdp_observations и поисковые индексы;
- unit и integration тесты CDP pipeline;
- CDP-наблюдение само по себе не создаёт PhysicalLink.

Следующий Sprint: Sprint 9 — BridgePortResolver + FDB.
## Sprint 9 — BridgePortResolver + FDB завершен

Реализовано:
- parser BRIDGE-MIB для dot1dBasePortIfIndex и dot1dTpFdbTable;
- явное отображение bridgePortIndex → ifIndex;
- BridgePortResolver не использует fallback bridgePortIndex == ifIndex;
- неоднозначное или отсутствующее отображение не разрешается;
- FDB хранит evidence «MAC виден за bridge-port» и не создаёт PhysicalLink;
- collector выполняет два WALK: bridge-port mapping и FDB;
- raw Observation сохраняется до нормализованных данных;
- Migration006FdbObservations;
- таблицы bridge_port_mappings и fdb_observations;
- unit и integration tests;
- проверены legacy net48 и linux-x64 Engine.

Следующий Sprint: Sprint 10 — ARP/MAC correlation.
## Sprint 10 — ARP/MAC correlation завершен

Реализовано:
- modern ipNetToPhysicalTable parser;
- IPv4 ARP и IPv6 Neighbor Discovery;
- legacy ipNetToMediaTable fallback;
- fallback выполняется только при SNMP Protocol failure;
- timeout/socket ошибки не подавляются;
- raw Observation сохраняется до normalized ARP/ND;
- Migration007ArpObservations;
- таблица arp_observations;
- MAC correlation объединяет ARP/ND и FDB evidence;
- корреляция не создаёт PhysicalLink;
- неоднозначный bridgePort mapping не приводит к угадыванию ifIndex;
- invalid/local/incomplete neighbor entries не используются для корреляции;
- regression tests покрывают IPv6 ND, ambiguous mapping и отсутствие ложной MAC correlation;
- проверены полный regression suite и linux-x64 Engine.

Следующий Sprint: Sprint 11 — Topology Resolver v1.
## Sprint 11 — Topology Resolver v1 завершен

Реализовано:
- отдельная модель PhysicalLinkCandidate;
- LinkEndpointClaim не является DeviceId;
- LLDP и CDP создают strong adjacency evidence;
- ARP/FDB correlation является только weak supporting evidence;
- ARP/FDB correlation без LLDP/CDP не создаёт link candidate;
- LLDP localPortNumber сохраняет собственную семантику;
- CDP cdpCacheIfIndex не преобразуется автоматически в ifIndex;
- candidate содержит источник, observation_id, capturedUtc и объяснение evidence;
- confidence отделён от типа evidence;
- настоящий PhysicalLink на этом этапе не материализуется;
- regression tests защищают запрет FDB-only link и port-index guessing;
- проверены полный regression suite и linux-x64 Engine.

Следующий Sprint: Sprint 12 — stale/lifecycle.
## Sprint 12 — stale/lifecycle завершен

Реализовано:
- состояния Fresh / Aging / Stale;
- FirstSeenUtc и LastSeenUtc;
- deterministic lifecycle policy через переданный nowUtc;
- poll failure не удаляет устройство или линк;
- отсутствие нового evidence не удаляет устройство или линк;
- stale не означает deleted;
- новое evidence возвращает discovered topology в Fresh;
- старое observation не перемещает LastSeenUtc назад;
- manual topology не стареет и не удаляется discovery/lifecycle;
- regression tests защищают lifecycle-инварианты;
- полный regression suite и linux-x64 Engine проходят.

Persistence lifecycle намеренно отложен до появления стабильных materialized DeviceId/PhysicalLinkId. ременный subjectKey не является постоянным идентификатором .

Следующий Sprint: Sprint 13 — map v1.
## Sprint 13 — Map v1 завершен

Реализовано:
- transport-neutral MapSnapshot / MapNode / MapLink contracts;
- MapEvidenceItem с confidence, freshness и evidence;
- TopologyMapProjector вынесен из WPF;
- NetLoom.Topology формирует готовый MapSnapshot;
- WPF не содержит topology resolution logic;
- WPF зависит от Contracts/Application и не получает ссылку на Topology;
- presentation key карты не является DeviceId;
- IP-адрес не используется как DeviceId;
- deterministic layout v1 без GraphX/MSAGL;
- обратные направления одного физического candidate объединяются в один undirected map link;
- duplicate evidence объединяется;
- self-link не отображается;
- пользовательский интерфейс Map v1 русскоязычный;
- regression tests, полный test suite и linux-x64 publish проходят.

Следующий Sprint: Sprint 14 — locations.
## Sprint 14 — Locations завершен

Реализовано:
- отдельная сущность Location с GUID LocationId;
- ParentLocationId для иерархии площадка / здание / этаж / помещение / шкаф;
- имя и необязательное описание;
- Migration008 и таблица locations;
- SQLite Location repository;
- проверка существования parent;
- запрет parent=self;
- защита от циклов;
- удаление Location с дочерними элементами запрещено;
- rename/move сохраняют LocationId;
- MapLocation и опциональный LocationId у MapNode;
- Location overlay не изменяет node/link identity;
- WPF отображает Location только как presentation metadata;
- постоянная Device→Location привязка по IP или MapNode.Key не создаётся;
- полный regression suite и linux-x64 publish проходят.

Следующий Sprint: Sprint 15 — manual media converters / unmanaged devices.
## Cross-cutting project hygiene

- Активный cross-cutting backlog ведётся в `docs/BACKLOG.md`.
- Реальные проблемы эксплуатации фиксируются в `docs/FRICTION_LOG.md`.
- Third-party зависимости учитываются в `THIRD-PARTY.md`.
- Пользовательские строки WPF переводятся на resource-based localization.
- linux-x64 publish считается проверкой совместимости сборки/публикации; полноценная runtime-проверка Linux будет добавлена после появления реальной логики в NetLoom.Engine.
## Sprint 15 — Materialized physical graph

Реализовано:

- общий постоянный граф TopologyDevice / DeviceInterface / PhysicalLink;
- внутренние GUID для устройств, интерфейсов и физических связей;
- Migration009 с таблицами devices, interfaces, physical_links;
- постоянная привязка Device к Location через devices.location_id;
- ручные неуправляемые устройства через DiscoveryOrigin = Manual;
- MonitoringCapability.None для ручных неуправляемых устройств;
- ручные интерфейсы с ifIndex = null и IsManual = true;
- ручные физические связи с PhysicalLinkStrength.Manual;
- защита ручной топологии от автоматической перезаписи;
- запрет удаления связанных ручных устройств и интерфейсов;
- проверка принадлежности interface endpoint соответствующему device;
- ObservationKind.Manual в существующей observation-модели;
- источник ручного действия: SourceAddress = User;
- общий MapSnapshot для автоматической и ручной топологии;
- opaque presentation keys, не раскрывающие DeviceId;
- transport-neutral map metadata: origin, monitoring capability, category;
- WPF отображает новые значения только через .resx;
- location overlay сохраняет Sprint 15 metadata.

Количество миграций: 9.

linux-x64 publish остаётся compatibility smoke-test сборки/публикации и пока не является подтверждением полноценной Linux runtime-функциональности Engine.

## Sprint 15.1b — PhysicalLink identity integrity

Реализовано:
- Domain-owned canonical `PhysicalLink.LinkKey`;
- одинаковая identity при reverse endpoint order;
- сохранение `PhysicalLink.Id` при rediscovery и однозначном refinement;
- отказ от угадывания при ambiguous refinement;
- поддержка различимых parallel links;
- защита manual PhysicalLink при automatic save с другим входящим GUID;
- monotonic persistence для first/last seen, resolved и confirmed timestamps;
- regression tests для reverse rediscovery, refinement, ambiguity, parallel links и timestamps.

Количество миграций остаётся 9.

Linux `linux-x64` publish в этом спринте является compatibility smoke test и не считается доказательством полноценной Linux runtime-функциональности Engine.

## Sprint 15.1c — current PhysicalLink evidence

Реализовано:
- обязательный `TopologyEvidence.SlotDiscriminator`;
- LLDP/CDP local-port slot identity;
- ARP/FDB correlation MAC slot identity;
- Domain-модель `PhysicalLinkEvidence`;
- `PhysicalLinkEvidenceMaterializer`;
- Migration010 `physical_link_evidence_current`;
- bounded replace-snapshot persistence с last-write-wins для одного slot;
- current evidence readback по одной связи и по всему materialized graph;
- automatic current evidence в `MaterializedTopologyMapProjector`;
- end-to-end regression: provisional → reverse rediscovery → refinement → newer poll → evidence readback → Map.

Количество миграций: 10.

## Sprint 16 — Live Map backend bridge

Реализовано:
- `IMapSnapshotProvider` в Application;
- `MaterializedMapSnapshotProvider` в Topology;
- реальный read pipeline из materialized SQLite graph в `MapSnapshot`;
- отдельный Windows composition root `NetLoom.Desktop`;
- WPF получает provider через constructor injection;
- автоматическое обновление карты каждые 5 секунд;
- WPF по-прежнему не зависит напрямую от Persistence/Topology;
- Desktop принимает `--database`, затем `NETLOOM_DATABASE`, затем использует `%LOCALAPPDATA%\NetLoom\netloom.db`;
- integration regression проверяет SQLite → provider → projector → MapSnapshot с current link evidence.

Новых миграций нет. Количество миграций остаётся 10.

`NetLoom.Engine` и `NetLoom.Service` пока не становятся monitoring runtime; это отдельные следующие этапы.

## Sprint 17 — Simulator raw SNMP replay

Реализовано:
- JSON raw SNMP snapshot schema v1;
- `RawSnmpSnapshotCodec` load/save/capture/build;
- сохранение OID/type/display/raw encoded bytes;
- replay через настоящие LLDP/CDP/FDB/ARP parsers;
- CLI `NetLoom.Simulator replay <file-or-directory>`;
- четыре безопасных fixture snapshots;
- snapshot regressions для LLDP/CDP/FDB/ARP;
- ARP regression подтверждает использование сохранённого BER payload;
- FDB regression подтверждает bridgePort → ifIndex mapping без подмены bridgePort на ifIndex.

Новых миграций нет. Количество миграций остаётся 10.
Новых сторонних зависимостей нет.

Inventory raw replay не объявлен реализованным, поскольку отдельного production raw inventory parser сейчас нет.

## Sprint 18 — Monitoring Runtime

Реализовано:
- `MonitoringPollRequest`, `MonitoringPollResult` и per-protocol step result;
- синхронный `MonitoringRuntime.PollOnce`;
- независимый запуск LLDP/CDP/FDB/ARP production collectors;
- продолжение cycle после ошибки отдельного protocol collector;
- `NetLoom.Engine poll-once`;
- concrete composition `SharpSnmpTransport + existing SQLite raw/normalized stores + production parsers`;
- operator-supplied SNMP secrets только через environment variables;
- секреты не принимаются через command line и не выводятся;
- `runtime-smoke` path без network access;
- фактический запуск self-contained linux-x64 Engine подтверждён через WSL Ubuntu с установленными native runtime dependencies;
- unit regressions для isolation/subset/UTC.

Scheduler не реализован и остаётся следующим отдельным P0 этапом.

Health/Interface high-frequency metrics не записываются. Metric/time-series storage gate остаётся открытым до соответствующего спринта.

Новых миграций нет. Количество миграций остаётся 10.

## Sprint 19 — Scheduler

Реализовано:
- Application `MonitoringScheduler`;
- немедленный первый poll cycle;
- fixed-delay cadence между завершёнными cycle;
- отсутствие overlap by construction;
- bounded scheduler result без накопления истории cycle в памяти;
- graceful cancellation между cycle;
- `NetLoom.Engine schedule --interval-seconds <N>`;
- `Ctrl+C` запрашивает остановку Scheduler;
- `runtime-smoke` теперь проверяет и Scheduler path;
- unit regressions для cadence/cancellation/interval validation.

Текущая граница cancellation: уже начатый synchronous collector не прерывается. Это будет возможно только после отдельного изменения collector contracts.

Health/Interface monitoring и metric/time-series storage не входят в Sprint 19.

Новых миграций нет. Количество миграций остаётся 10.

## Sprint 20 — metric storage boundary + Health snapshot model

Реализовано:
- `IMonitoringMetricStore` как отдельный Application boundary;
- `MonitoringMetricSample` со стабильным `DeviceId` и опциональным `InterfaceId`;
- `MonitoringMetricKind.HealthAvailability`;
- `MonitoringMetricKind.HealthUptimeSeconds`;
- `HealthSnapshot`;
- `HealthStatus`;
- `HealthMetricProjector`;
- запрет persistent metric projection для Health snapshot без стабильного DeviceId;
- unit regressions для identity/UTC/availability/uptime.

Не реализовано:
- concrete time-series backend;
- Health SNMP collector;
- Health scheduler command;
- Interface monitoring;
- retention/aggregation.

Health monitoring остаётся следующим P0 пунктом.

Новых migrations нет. Количество migrations остаётся 10.

## Sprint 21 — SNMP Health monitoring

Реализовано:
- `IHealthCollector` и `HealthCollectionRequest`;
- production `SnmpHealthCollector`;
- lightweight GET только `sysUpTime.0`;
- tolerant sysUpTime parser;
- `MonitoringPollKind.Health`;
- Health step в `MonitoringRuntime`;
- current `HealthSnapshot` в `MonitoringPollStepResult`;
- optional stable `--device-id <guid>` в Engine;
- Health включён в default poll kinds;
- `schedule --kinds health` использует существующий Scheduler;
- runtime-smoke покрывает Health path;
- unit regressions для uptime parsing, stable/unbound DeviceId и failure isolation.

Failed SNMP Health poll не означает исчезновение Device и не создаёт `HealthStatus.Down` автоматически.

Concrete metric backend/history не реализован. High-frequency Health history не пишется в topology/configuration SQLite.

Interface monitoring остаётся следующим P0 этапом.

Новых migrations нет. Количество migrations остаётся 10.

## Sprint 22 — IF-MIB current interface status monitoring

Реализовано:
- `IInterfaceCollector`;
- `InterfaceCollectionRequest`;
- `InterfaceMonitoringSnapshot`;
- production `SnmpInterfaceStatusCollector`;
- lightweight `ifAdminStatus` + `ifOperStatus` walks;
- tolerant status/index parser;
- `MonitoringPollKind.Interface`;
- Interface step в `MonitoringRuntime`;
- current Interface snapshots в `MonitoringPollStepResult`;
- Engine output для current admin/oper status;
- default poll kinds включают Interface;
- `schedule --kinds interface` использует существующий Scheduler;
- runtime-smoke покрывает Interface path;
- unit regressions для parser, DeviceId separation и failure isolation.

`ifIndex` не является `InterfaceId`. Persistent interface metric identity не создаётся из ifIndex.

Interface counter/rate history и concrete metric backend не реализованы. High-frequency interface history не пишется в topology/configuration SQLite.

STP/RSTP Collector остаётся следующим P0 этапом.

Новых migrations нет. Количество migrations остаётся 10.

## Sprint 23a — BRIDGE-MIB STP observation foundation

Реализовано:
- Domain `StpObservation` и `StpPortState`;
- explicit common `InstanceId = cist`;
- Application `IStpCollector`, `IStpObservationParser`, `IStpObservationStore`, `StpCollectionRequest`;
- production `StpCollector`;
- production `StpObservationParser`;
- raw `ObservationKind.Stp` сохраняется до normalized rows;
- root bridge/root cost/root bridge port;
- port priority/state/enable/path cost/designated bridge data;
- поддержка `dot1dStpPortPathCost32`;
- explicit `dot1dBasePortIfIndex` mapping;
- отсутствие fallback `bridgePortIndex == ifIndex`;
- `SqliteStpObservationStore`;
- Migration011 `stp_observations` + `stp_port_states`;
- unit/integration regressions для mapping, ambiguity, path cost и persistence.

Не входят в Sprint 23a:
- MonitoringRuntime/Engine/Scheduler integration;
- Simulator replay/fixture;
- MSTP instance collection;
- STP tree projection;
- physical ring detection;
- Ring protection analyzer;
- Turbo Ring/vendor ring collection.

`STP/RSTP Collector` остаётся открытым до runtime + simulator completion.

Количество migrations: 11.

## Sprint 23b1 — STP MonitoringRuntime + Engine integration

Реализовано:
- `MonitoringPollKind.Stp`;
- isolated STP step в `MonitoringRuntime`;
- production Engine wiring `StpCollector + StpObservationParser + SqliteStpObservationStore`;
- STP включён в default Engine kinds;
- `schedule --kinds stp` переиспользует общий Scheduler;
- runtime-smoke включает STP и ищет step по kind, без positional assumptions;
- unit regression подтверждает, что failed STP step не отменяет следующий collector;
- failed STP poll не меняет materialized topology.

Не входят:
- Simulator raw STP replay/fixture;
- STP tree projection;
- physical ring detection;
- Ring protection analyzer;
- MSTP instances;
- Turbo Ring/vendor ring protocols.

Migration011 остаётся последней migration; количество migrations: 11.

`STP/RSTP Collector` остаётся открытым до Sprint 23b2 Simulator completion.

## Sprint 23b2 — Simulator raw STP replay

Реализовано:
- `SnmpSnapshotReplayer` поддерживает `ObservationKind.Stp`;
- replay вызывает production `StpObservationParser`;
- добавлен versioned raw fixture `stp-basic.json`;
- fixture содержит bridge-level STP scalars, `dot1dBasePortIfIndex` и STP port rows;
- snapshot regression подтверждает `InstanceId = cist` и mapping `BridgePortIndex 5 -> IfIndex 101`;
- existing Sprint 23a parser regressions продолжают покрывать missing/ambiguous mapping.

Не изменены MonitoringRuntime/Scheduler, SQLite schema, materialized Device/DeviceInterface/PhysicalLink, STP tree projection, physical ring detection, MSTP и Turbo Ring/vendor protocols.

Migration011 остаётся последней migration; количество migrations: 11.

`STP/RSTP Collector` завершён и закрыт. Следующий P0: `STP tree projection`.

## Sprint 24 — STP tree projection

Реализовано:
- transport-neutral `StpTreeSnapshot`;
- `StpTreePort` и normalized `StpTreePortState`;
- pure deterministic `StpTreeProjector`;
- explicit stable DeviceId input;
- binding `IfIndex -> InterfaceId` только внутри указанного Device и только при единственном совпадении;
- root port projection;
- forwarding/blocking/listening/learning/disabled/broken/unknown states;
- unresolved/cross-device/ambiguous binding остаётся `InterfaceId = null`;
- deterministic ordering по BridgePortIndex;
- unit regressions для root/forwarding/blocking, safe InterfaceId binding, unresolved/ambiguity и determinism.

Не изменены:
- PhysicalLink и materialized physical topology;
- MonitoringRuntime/Scheduler;
- STP collector/parser/store;
- SQLite schema;
- MapLink physical semantics;
- WPF persistence boundary;
- MSTP/Turbo Ring/vendor ring protocols.

Migration011 остаётся последней migration; количество migrations: 11.

Backlog `STP tree projection` закрыт.

Следующий P0: `Physical ring detection`.

## Sprint 25 — Physical ring detection

Реализовано:
- transport-neutral `PhysicalRing`;
- pure `PhysicalRingDetector`;
- undirected materialized PhysicalLink multigraph;
- reverse/duplicate dedupe по canonical LinkKey;
- сохранение настоящих parallel links;
- 2-edge parallel physical cycles;
- deterministic fundamental cycle basis;
- stable `RingKey` из PhysicalLink.Id;
- manual topology participation;
- Stale/Aging/Fresh links сохраняют physical-fact semantics;
- archived links исключаются;
- hidden links остаются physical facts;
- unit regressions для triangle, acyclic/disconnected graph, manual link, reverse duplicate, parallel cycle, hidden/stale/archive semantics и bounded cycle basis.

Не изменены:
- PhysicalLink и его persistence;
- FDB/ARP resolver semantics;
- STP collector/tree projection;
- MonitoringRuntime/Scheduler;
- MapLink/WPF;
- SQLite schema;
- vendor ring/MSTP logic.

Migration011 остаётся последней migration; количество migrations: 11.

Backlog `Physical ring detection` закрыт.

Следующий P0: `Ring protection analyzer`.

## Sprint 26A — SQLite concurrency hardening

Реализовано:
- SQLiteConnectionStringBuilder;
- WAL;
- BusyTimeout = 5000 ms;
- synchronous NORMAL;
- existing foreign_keys ON policy сохранена;
- immediate atomic SavePhysicalLink decision;
- immediate atomic SaveDevice manual protection + upsert;
- immediate atomic SaveInterface manual protection + upsert;
- serialized atomic pending migration batch;
- concurrent reader/writer regression;
- concurrent provisional/refined PhysicalLink regression;
- concurrent manual Device/Interface protection regressions;
- concurrent clean DatabaseInitializer regression;
- pending-batch rollback regression;
- connection PRAGMA regression.

PhysicalLink identity/refinement logic, Domain, materialized schema и migration list не менялись.

Migration011 остаётся последней migration; количество migrations: 11.

WAL backup/export rule зафиксировано: live copy одного `.db` запрещён как supported backup workflow; использовать SQLite Backup API/VACUUM INTO или корректный offline snapshot.

Следующий эксплуатационный P0: Sprint 26B — bounded raw observation retention + evidence expiry semantics.

Ring protection analyzer пока не применяется. Отдельно в backlog вынесены basis-independent graph safety analysis и user-facing ring semantics.

## Sprint 26B — bounded raw observation retention

Реализовано:
- Application `IObservationRetentionStore`;
- SQLite bounded time-only retention по `captured_utc`;
- default raw window 24 часа;
- максимум 8 parent observations на poll-cycle;
- один parent delete на одну immediate transaction;
- manual observations исключены из raw protocol cleanup;
- existing FK cascades используются без новой schema;
- Application explainability state `NotApplicable / Available / Expired`;
- SQLite evidence explanation reader через `LEFT JOIN observations`, без загрузки varbinds;
- Engine запускает cleanup после `poll-once` и каждого scheduler cycle;
- retention failure изолирован от protocol poll result;
- integration regressions для strict cutoff, bounded oldest-first, manual exclusion, cascade graph, expired current evidence и WAL reader/writer.

Не изменены:
- Device/Interface/PhysicalLink identity и lifecycle;
- `TopologyLifecyclePolicy` и его thresholds;
- MonitoringScheduler;
- MapEvidenceItem/WPF;
- SNMP WALK limit/cancellation/multi-device scheduler;
- ring semantics/analyzer;
- SQLite schema.

Migration011 остаётся последней; migrations: 11.

Следующий P0 после эксплуатационного hardening: basis-independent graph safety analysis. Отдельно в backlog остаются localized raw-expired UI, WALK limit, active-poll cancellation, multi-device scheduler и user-facing ring semantics.

## Sprint 27 — basis-independent graph safety analysis

Реализовано:
- transport-neutral `PhysicalLinkFailureImpact`;
- transport-neutral `ForwardingCycleAnalysis`;
- pure `PhysicalGraphSafetyAnalyzer`;
- edge-aware multigraph bridge/SPOF detection;
- direction-neutral blast radius через две partitions и separated device-pair count;
- basis-independent forwarding-cycle edge set;
- conservative STP endpoint resolution через stable InterfaceId;
- explicit unresolved coverage semantics;
- parallel-edge correctness;
- deterministic ordering;
- unit regressions для tree/triangle/disconnected semantics, parallel/reverse duplicate, manual/hidden/stale/archive/self, all-forwarding cycle, blocking break, missing/duplicate/transitional STP и deterministic input order.

Не изменены:
- `PhysicalRingDetector`;
- materialized topology persistence;
- STP observation/projector pipeline;
- Engine runtime;
- MapLink/WPF;
- SQLite schema.

Migration011 остаётся последней; migrations: 11.

Следующий P0: user-facing ring semantics before per-ring protection labels. `Ring protection analyzer` остаётся отдельным последующим P0.
