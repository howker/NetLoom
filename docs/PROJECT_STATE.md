# PROJECT_STATE

## Текущая версия

0.2-dev

## Текущее состояние

Sprint 32C localization foundation, post-closure neutral-resource correction, master-plan documentation alignment, canonical documentation consolidation и AI development verification hardening завершены.

Текущий обязательный следующий этап: реалистичный 3–5 device SNMP/snmpsim stand с реальным использованием WPF, затем review только наблюдаемого `FRICTION_LOG.md` и выбор ровно одного следующего product Sprint.

До review stand friction новый product Sprint не назначается.

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
- transport-neutral `PhysicalCycleBasisElement`;
- pure `PhysicalCycleBasisDetector`;
- undirected materialized PhysicalLink multigraph;
- reverse/duplicate dedupe по canonical LinkKey;
- сохранение настоящих parallel links;
- 2-edge parallel physical cycles;
- deterministic fundamental cycle basis;
- stable `CycleKey` из PhysicalLink.Id;
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
- `PhysicalCycleBasisDetector`;
- materialized topology persistence;
- STP observation/projector pipeline;
- Engine runtime;
- MapLink/WPF;
- SQLite schema.

Migration011 остаётся последней; migrations: 11.

Следующий P0: user-facing ring semantics before per-ring protection labels. `Ring protection analyzer` остаётся отдельным последующим P0.

## Sprint 28 — operator-facing ring semantics

Реализовано:
- Sprint 25 primitive переименован в `PhysicalCycleBasisElement` / `PhysicalCycleBasisDetector`;
- legacy `RingKey` переименован в `CycleKey`;
- отдельный transport-neutral `PhysicalRedundancyRegion`;
- `PhysicalRedundancyRegionKind = SimpleRing / ParallelLinks / Composite`;
- `PhysicalRedundancyRegionDetector`;
- basis-independent vertex-biconnected decomposition;
- stable RegionKey по PhysicalLink.Id membership;
- simple-ring classification без enumeration всех simple cycles;
- Composite semantics для chorded/dense regions;
- ParallelLinks semantics без ложного user-facing ring;
- articulation-separated rings остаются separate regions;
- regression coverage для triangle, chorded square, figure-eight, parallel, reverse duplicate, acyclic/disconnected, manual/hidden/stale/archive, endpoint refinement и deterministic input order.

Не изменены:
- Sprint 27 graph-safety analyzers;
- STP observation/tree projection;
- materialized topology persistence;
- Engine runtime;
- MapSnapshot/WPF;
- SQLite schema.

Migration011 остаётся последней; migrations: 11.

Следующий P0: Ring protection analyzer. Protection status должен потреблять stable operator-facing region semantics и не менять membership.

## Sprint 29 — Ring protection analyzer

Реализовано:
- transport-neutral `RingProtectionAnalysis`;
- `RingProtectionStatus = NotApplicable / Protected / Unprotected / Degraded / Unresolved`;
- pure `RingProtectionAnalyzer`;
- shared exact `StpEndpointStateResolver`;
- Sprint 27 forwarding-cycle analysis переведён на тот же resolver без изменения его публичного contract;
- protection применяется только к Sprint 28 `SimpleRing`;
- Blocking и Disabled разделены;
- complete exact endpoint coverage требуется для Protected/Unprotected/Degraded;
- explainability buckets Forwarding/Blocking/Disabled/Unresolved PhysicalLinkId;
- explicit InstanceId isolation;
- manual/unmanaged missing InterfaceId -> Unresolved;
- stale membership не меняет protection classification;
- deterministic results.

Не изменены:
- PhysicalRedundancyRegion membership;
- materialized topology persistence;
- Engine runtime;
- MapSnapshot/WPF;
- SQLite schema.

Migration011 остаётся последней; migrations: 11.

Следующий P0 определяется актуальным `docs/BACKLOG.md`.

## Sprint 30A — MAC/IP lookup backend

Текущий schema level: Migration012.

Реализован backend foundation для поиска MAC/IP:

- optional stable observation→DeviceId binding;
- runtime binding успешных ARP/FDB observations при наличии `MonitoringPollRequest.DeviceId`;
- raw retention автоматически удаляет binding вместе с observation;
- MAC lookup по persisted FDB evidence;
- IP lookup через usable ARP IP→MAC evidence и затем FDB sightings;
- exact `bridgePort -> ifIndex` ambiguity сохраняется;
- exact `(DeviceId, ifIndex) -> InterfaceId` используется только при materialized interface;
- состояния `FdbNotObserved`, `ObservationUnbound`, `BridgePortUnresolved`, `BridgePortAmbiguous`, `InterfaceNotMaterialized`, `ResolvedInterface`;
- никакого access-port guessing и никакого PhysicalLink из FDB/ARP.

Пользовательский backlog-пункт «поиск MAC/IP до switch/interface» остаётся открытым до Sprint 30B: локализованный WPF search UX, представление ambiguity/freshness и navigation/highlight.

## Sprint 30B — WPF MAC/IP search UX

Реализован локализованный операторский поиск MAC/IP:

- `MacIpLookupSearchService` auto-detects и нормализует IP/MAC;
- WPF получает read-only `IMacIpLookupReader` через Desktop composition;
- все пользовательские строки поиска находятся в `UiStrings.resx`;
- candidate list сохраняет ambiguity и unresolved states;
- отображаются FDB/ARP timestamps и evidence source;
- materialized `MapNode` несёт optional stable DeviceId отдельно от opaque presentation Key;
- `ResolvedInterface` подсвечивает устройство и прокручивает карту к нему;
- InterfaceId/ifIndex показываются в деталях;
- interface-level/port highlight не создаётся;
- поиск не меняет topology, manual nodes, links или monitoring state;
- Migration012 остаётся последней.

Sprint 30A + 30B закрывают backlog «Удобный поиск MAC/IP до конкретного switch/interface».

## Sprint 31A — minimal topology/ring alert semantics

Добавлен transport-neutral current-state alert contract и pure `TopologyAlertEvaluator`.

Подтверждённые правила:
- confirmed forwarding cycle -> Critical;
- SimpleRing Unprotected не дублирует Critical и обязан совпадать с confirmed forwarding-cycle evidence того же InstanceId;
- complete degraded SimpleRing -> Warning для Disabled и/или multiple Blocking links;
- Protected / Unresolved / NotApplicable -> no alert;
- bridge/SPOF, Aging/Stale и единичный poll failure -> no active topology-failure alert;
- AlertKey deterministic и пригоден для current-state dedupe/transition comparison;
- evaluator не меняет topology и не использует FDB/ARP/IP inference;
- Sprint 31A не добавляет WPF, delivery, ack/silence/history;
- Migration012 остаётся последней.

Backlog parent «Минимальные alerts для реально полезных topology/ring failures» остаётся открытым до Sprint 31B: operator-facing read-only alert surface + transition/repeat suppression.

## Sprint 31B — operator-facing read-only topology alerts

Реализовано:
- успешный STP poll теперь использует существующий `IObservationDeviceBindingStore` и при наличии `MonitoringPollRequest.DeviceId` привязывает `StpObservation.Observation.Id` к stable DeviceId;
- добавлен read-only `ILatestStpObservationReader`;
- `SqliteStpObservationStore.GetLatest(instanceId)` выбирает по одному latest bound STP observation на каждый DeviceId для explicit InstanceId, с deterministic ordering `captured_utc DESC, observation_id DESC`;
- source address не используется как device identity;
- добавлен Application boundary `ITopologyAlertSnapshotProvider`;
- `MaterializedTopologyAlertSnapshotProvider` on-demand читает current materialized PhysicalLink/interfaces и latest STP, затем выполняет `StpTreeProjector`, forwarding-cycle analysis, redundancy-region/ring protection analysis и существующий `TopologyAlertEvaluator`;
- WPF получает provider только через read-only Application contract; SQLite из WPF не читается и не записывается напрямую;
- текущий operator surface работает для explicit CIST instance `"cist"` и показывает current Critical/Warning alerts с reasons, region keys и PhysicalLinkId evidence;
- `TopologyAlertTransitionTracker` хранит только process-local наборы AlertKey отдельно по InstanceId;
- переходы: active без предыдущего active set -> `FirstAppearance`, тот же set -> `Unchanged`, другой non-empty set -> `Changed`, non-empty -> empty -> `Resolved`;
- `Unchanged` подавляет повторный transition indicator, но current alert list продолжает обновляться как current-state surface;
- после process restart suppression state отсутствует, поэтому существующий active alert снова классифицируется как `FirstAppearance`;
- history, delivery, acknowledgement, silence и persistent transition state не добавлены;
- существующий `Migration012ObservationDeviceBindings` переиспользован для STP; `Migration013` не вводится;
- одновременно исправлены обнаруженные повреждённые русские operator-facing values в `UiStrings.resx`.

Проверка на рабочем репозитории перед commit:
- `git diff --check` — без ошибок;
- unit tests: 171/171;
- integration tests: 50/50;
- `NetLoom.Desktop` build: 0 warnings, 0 errors.

Commit `0e39460` (`Sprint 31B: add read-only topology alert surface`) отправлен в `origin/main`; на момент закрытия `HEAD == origin/main`, worktree clean.

Sprint 31A + 31B закрывают backlog parent «Минимальные alerts для реально полезных topology/ring failures».

## Text-integrity hardening после Sprint 31

Завершён ближайший технический follow-up из Product readiness:
- `tools/Check-TextEncoding.ps1` теперь разбирает WPF `.resx` как XML и проверяет начало каждого operator-facing `<data><value>`;
- подозрительное начало WPF resource value становится failing `RESX_DROPPED_CAPITAL`, а не только информационным `SUSPECT_DROPPED_CAPITAL`;
- malformed WPF `.resx` отдельно даёт `INVALID_RESX_XML`;
- намеренно строчный `EvidenceCount` разрешён явным resource-key exception;
- добавлен unit regression `UiStringResourceIntegrityTests` для dropped-capital corruption.

Проверка на рабочем репозитории перед commit:
- `Check-TextEncoding.ps1` — PASS;
- WPF `.resx <value>` text-integrity policy — PASS;
- PowerShell UTF-8 BOM policy — PASS;
- unit tests: 172/172.

Commit `a695955` (`Harden WPF resource text integrity audit`) отправлен в `origin/main`; на момент закрытия `HEAD == origin/main`, worktree clean.

Следующий продуктовый этап определяется актуальными `docs/BACKLOG.md` и `FRICTION_LOG.md`; отдельный новый P0 feature сейчас не назначен.

## Post-Sprint 31 — acceptance and live monitoring topology materialization

Commit `cc36fc0` (`Materialize polled devices into topology`) подключил production materialization к `MonitoringRuntime` через `IMonitoringTopologyMaterializer` и существующий `SqliteMaterializedTopologyRepository`.

Подтверждённые semantics:
- успешные LLDP/CDP/FDB/ARP/STP steps при explicit `MonitoringPollRequest.DeviceId` refresh'ят materialized Device;
- успешный Health step при explicit `DeviceId` refresh'ит Device;
- успешный Interface step materializes automatic `DeviceInterface` по exact `(DeviceId, ifIndex)`;
- `source_address` не используется как DeviceId и unbound poll не создаёт guessed identity;
- repeated Interface poll сохраняет stable `InterfaceId`, earliest `FirstSeenUtc` и продвигает `LastSeenUtc`;
- manual Device/Interface не перезаписываются automatic materialization;
- polling не создаёт `PhysicalLink`;
- IF-MIB admin/oper status остаётся current monitoring snapshot, а materialization фиксирует stable topology identity/lifecycle.

SQLite schema не изменена; `Migration012` остаётся последней.

### Acceptance 2026-09-11

На рабочем `main` подтверждено:
- Sprint 31B manual WPF/SQLite transitions: `FirstAppearance`, `Unchanged` suppression, `Changed`, `Changed` suppression, `Resolved`, `Resolved` suppression;
- live SNMP v2c Interface poll materialized ровно два automatic interfaces для explicit DeviceId;
- второй идентичный poll не создал дубликаты, сохранил оба `InterfaceId` и `FirstSeenUtc`, продвинул `LastSeenUtc`;
- polling не создал `PhysicalLink`;
- targeted Sprint 31 unit tests: 6/6;
- targeted integration tests: 7/7;
- full regression: Unit 177/177, Integration 51/51, Snapshots 7/7, всего 235/235;
- `NetLoom.Desktop` build: 0 warnings, 0 errors;
- text-integrity audit: PASS;
- до documentation closure `git diff --check` clean, worktree clean, `HEAD == origin/main == cc36fc0f71499a216413ac56f1862cdd1935b664`.

Acceptance не потребовал изменений production-кода.
## Sprint 32A — coherent non-blocking Desktop refresh

Sprint 32A is complete.

Implemented:
- one `MaterializedTopologyReadSet` per refresh from one SQLite connection and one read transaction;
- the read transaction closes before projection, topology/ring analysis, transition tracking, or UI apply;
- map and topology alerts are computed from the same captured read-set;
- `SqliteStpObservationStore.GetLatest()` no longer performs N+1 connections;
- periodic refresh uses background READ -> COMPUTE and Dispatcher APPLY with single-flight coordination;
- `TopologyAlertTransitionTracker.Observe()` remains Dispatcher-owned and runs only after a successful whole refresh;
- failed, cancelled, invalidated, or post-close refresh work cannot mutate transition state or apply UI;
- last-known-good map/alerts remain visible on refresh failure, with distinct first-load failure and stale/recovery states;
- lookup runs off the Dispatcher, is serialized/single-flight, rejects stale completion, and is invalidated on close;
- production `NetLoom.Desktop` composition uses `MaterializedTopologyRefreshSnapshotProvider` with `SqliteMaterializedTopologyReadSetReader`; WPF remains free of direct Persistence/Topology references.

Deterministic proof:
- the old multi-connection mixed-read path was demonstrated RED with a synchronization seam;
- the coherent read-set path and composite refresh provider are GREEN and use one SQLite connection/read-set per refresh;
- lifecycle, last-known-good, lookup serialization, transition, and STP regressions are covered by automated tests.

Acceptance 2026-09-12:
- full regression before the final Desktop composition wiring: Unit 191/191, Integration 55/55, Snapshots 7/7, total 253/253;
- after production wiring, `NetLoom.Desktop` forced build completed with 0 warnings and 0 errors;
- after production wiring, `Sprint32ACoherentReadSetTests` passed 2/2;
- text-integrity audit passed with the reviewed dropped-capital allowlist at 172 lines / 161 fingerprints;
- `git diff --check` passed;
- production wiring commit `0c22d4d` (`Wire desktop to coherent topology refresh`) is pushed with `HEAD == origin/main`;
- manual SQLite contention used a real `BEGIN IMMEDIATE` writer transaction for 25 seconds against the two-device database; while the writer lock was held, the Desktop remained responsive, lookup remained responsive, and the map stayed visible; after lock release, normal refresh recovered.

Sprint 32A is closed.

## Sprint 32B — persistent host logging for Desktop and Engine

Sprint 32B is complete.

Implemented:
- shared `NetLoom.HostLogging` host-logging layer for Desktop and Engine using NLog 6.2.0;
- separate persistent host files `desktop.log` and `engine.log`;
- Windows default log directory `%ProgramData%\NetLoom\logs`;
- portable Engine log directory semantics: `$XDG_STATE_HOME/netloom/logs`, falling back to `$HOME/.local/state/netloom/logs`;
- environment overrides `NETLOOM_LOG_DIRECTORY`, `NETLOOM_LOG_LEVEL`, `NETLOOM_LOG_MAX_BYTES`, and `NETLOOM_LOG_MAX_ARCHIVES`;
- size-based rotation with bounded archive count;
- Desktop startup failures are persisted as `HOST_FATAL`;
- Desktop installs `HostLogTraceListener` at the composition root so existing WPF refresh/search `Trace` failures are persisted without adding a logging dependency to WPF;
- Engine persists host startup/fatal events, runtime-smoke result, scheduler lifecycle, per-poll summary, failed poll-step type, all-steps-failed state, and retention failures;
- failed Engine poll-step logging intentionally persists `ErrorType` without persisting `MonitoringPollStepResult.ErrorMessage`, reducing the chance of protocol/credential text entering the persistent log;
- polling/materialization failures that surface as failed monitoring steps are therefore persisted through the Engine host boundary;
- `THIRD-PARTY.md` records the NLog dependency/license;
- no SQLite schema change; `Migration012` remains the latest migration.

Commits:
- `bdb7f7f` — `Add shared persistent host logging foundation`;
- `1bd7d8a` — `Wire Engine to persistent host logging`;
- `1ba268e` — `Wire Desktop to persistent host logging`.

Acceptance 2026-09-12:
- forced Unit, Integration, Snapshot, Engine, and Desktop builds passed;
- Unit tests: 197/197;
- Integration tests: 55/55;
- Snapshot tests: 7/7;
- total automated regression: 259/259;
- Desktop invalid-startup acceptance created `desktop.log` and persisted `HOST_STARTED`, `HOST_FATAL`, and the startup exception;
- `NETLOOM_LOG_LEVEL=Error` suppressed `HOST_STARTED` while retaining `HOST_FATAL`;
- rotation acceptance with `NETLOOM_LOG_MAX_BYTES=512` and `NETLOOM_LOG_MAX_ARCHIVES=2` produced exactly two bounded archives;
- a failed SNMP poll persisted `POLL_STEP_FAILED` and `ALL_POLL_STEPS_FAILED`;
- a dedicated SNMP-community canary was absent from the persistent failed-poll log;
- text-integrity audit passed;
- `git diff --check` passed;
- acceptance completed with `HEAD == origin/main == 1ba268ee5071fcd4a35767bb3966d116893dad6d` and a clean worktree.

Sprint 32B is closed.

## Architecture gate before Sprint 32C — localization ownership

The gate is complete and recorded as ADR-063.

Decision:
- `NetLoom.Wpf` owns operator-facing localization resources for the Windows Desktop client;
- `UiStrings.resx` is the English neutral/fallback resource and `UiStrings.ru.resx` is the Russian satellite resource;
- `NetLoom.Desktop` owns explicit startup culture selection before WPF construction, but does not own UI strings;
- Domain/Application/Contracts/Topology/Persistence/Protocols remain localization-neutral and expose structured semantics rather than pre-localized operator text;
- pluralization and operator-facing formatting stay in the WPF presentation/localization layer;
- future presentation clients own their own localization resources instead of putting shared UI strings into `NetLoom.Contracts`.

Sprint 32C is complete.

## Sprint 32C — localization foundation

Implemented:
- `NetLoom.Wpf` now owns the Desktop localization catalog in accordance with ADR-063;
- `UiStrings.resx` is the English neutral/fallback resource;
- `UiStrings.ru.resx` is the Russian satellite resource with key/placeholder parity checks;
- `NetLoom.Desktop` owns explicit startup culture selection before WPF construction;
- default Desktop UI culture is `ru-RU`;
- explicit `en-US` selection is supported;
- unsupported culture selection is rejected rather than silently selecting an undefined locale;
- plural-aware UI formatting is implemented for supported English/Russian count presentations;
- the Topology presentation-text leak was removed so backend topology projection no longer owns the localized unknown-device label;
- operator-facing unknown-device text is localized in WPF;
- WPF template `App.xaml` / `App.xaml.cs` ownership was removed from the library boundary, with Desktop remaining the application composition root;
- localization integrity coverage now checks resource parity/placeholders and blocks Cyrillic string literals in the guarded WPF/Topology C# presentation boundary while allowing comments;
- backend contracts remain localization-neutral.

Acceptance 2026-09-12:
- forced Unit build passed;
- localization tests: 8/8;
- forced Integration, Snapshot, and Desktop builds passed;
- full Unit tests: 203/203;
- full Integration tests: 55/55;
- full Snapshot tests: 7/7;
- total automated regression: 265/265;
- Russian satellite assembly was produced and verified;
- culture selection acceptance passed for default `ru-RU`, explicit `en-US`, and unsupported-culture rejection;
- text-integrity plus C# localization guard passed;
- `git diff --check` passed;
- exact implementation boundary was verified as 16/16 paths, including three new files and two intended deletions;
- full staged diff was saved as a review artifact before commit;
- implementation commit `cf6613e` is pushed with `HEAD == origin/main == cf6613ed8d064d0d4ce6e9d91c1b652b5d191140`;
- postflight worktree is clean.

Sprint 32C is closed. The next required step is the planned realistic 3–5 device SNMP/snmpsim stand acceptance, followed by review of `FRICTION_LOG.md` before choosing the next product feature.


## Current execution gate after architecture review

The architecture/master-plan review is recorded in the canonical documentation.

Confirmed current facts:
- Sprint 32C implementation and documentation are closed;
- the post-closure neutral-resource correction is complete;
- structured XML inspection of `UiStrings.resx` and `UiStrings.ru.resx` found no `Name1`, `Icon1`, or `Bitmap1` `<data>` entries; those names exist only in the standard ResX schema documentation comment;
- `NetLoom.Wpf` declares `[assembly: NeutralResourcesLanguage("en")]`;
- product scope is network observability/topology/diagnostics, not process-data acquisition or a generic NMS;
- the long architecture roadmap is direction, not an automatic Sprint sequence.

Committed next steps:
1. Run the realistic 3–5 device SNMP/snmpsim stand with the WPF client as a normal working session.
2. Record only observed operational friction in `FRICTION_LOG.md`.
3. Review that friction and choose exactly one next product Sprint.

Default next candidate, only if stand friction does not reveal a more important problem: incremental WPF map reconciliation that preserves selection, zoom/pan, pinned/manual layout and unchanged visual identity.

Longer directions — interface degradation, durable incidents/outbox, outbound notification, production configuration, runtime/deployment readiness, HTTP API/Web, probable failure-boundary localization, additional industrial protection and Site/Probe — remain roadmap.

## Sprint 32C post-closure correction

The correction is complete.

Verified before the change:
- current `NetLoom.Wpf/AssemblyInfo.cs` did not declare `NeutralResourcesLanguage`;
- structured parsing of both current ResX files found no `Name1`, `Icon1`, or `Bitmap1` `<data>` entries; those names occur only in the standard ResX schema documentation comment and therefore are not product resource keys.

Implemented:
- `NetLoom.Wpf` declares `[assembly: NeutralResourcesLanguage("en")]`;
- localization integrity regression checks the assembly neutral language;
- the same regression checks actual parsed ResX `<data>` keys and explicitly ignores schema-comment examples by construction;
- no ResX product data was removed because no template data entries existed.

Acceptance requirement for this correction:
- the new regression is demonstrated RED against the old `AssemblyInfo.cs`;
- the targeted regression becomes GREEN after the declaration is added;
- forced Unit build and full Unit regression pass;
- forced Desktop build passes;
- text-integrity and `git diff --check` pass.

The master-plan documentation alignment is also recorded. The next committed step is the realistic 3–5 device SNMP/snmpsim stand acceptance followed by `FRICTION_LOG.md` review.

