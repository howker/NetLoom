# PROJECT_STATE

## Текущая версия

0.2-dev

## Текущее состояние

The realistic stand gate, Sprint 33A/33B, Sprint 34A through Sprint 34K, the UI foundation task, Sprint 35, Sprint 36, and Sprint 37 are complete. The retained notification-delivery path remains supported but is not the active product priority.

Sprint 37 — manual topology from the UI — is technically complete and operator-accepted. The accepted Sprint 37 implementation head is `e94538d173c4bea4fa5bf4c3a650a270b8e65906`. The localized WPF editor creates/edits/removes manual devices, virtual ports and manual cables through the Application command boundary; automatic topology can be selected as a cable endpoint but is read-only there. Successful writes use the existing coherent refresh path, and accepted manual topology survives full Desktop close/reopen.

The final operator-remediation removed several misleading presentation behaviors instead of documenting workarounds. Device cards grow from measured content rather than a fixed height; cable selection preserves viewport position; destructive deletion is explicit and only real cable references block it; leaving a new/edited device for the Links tab requires an explicit save decision. Automatic interface diagnostics now use observed IF-MIB `ifName`, `ifAlias`, `ifIndex` and `ifType` without inventing vendor-specific prefixes, and management address is displayed as observed metadata rather than identity.

`Migration020InterfaceIdentityAndManagementAddress` is now the current schema migration. It adds nullable `devices.management_address` and `interfaces.if_type`; the shared manual-topology tables themselves remain unchanged. Management IP remains observation/materialization metadata and is never DeviceId. High-frequency monitoring counters remain outside this topology metadata migration.

Technical acceptance for the final Sprint 37 remediation included forced solution build, cause-oriented targeted tests, full modern/legacy/integration/snapshot regression, exact repository-boundary/blob proof and push. Focused operator re-test then passed. No further Sprint 37 product change is open.

The next product work is fixed by the existing `Committed sequence`: Sprint 38 — Locations on the map. Its operator outcome is to read the physical object by site/building/room/rack boundaries instead of a flat graph. Sprint 39 and Sprint 40 remain subsequent committed items; no reorder has been approved.

## Основа проекта

- решение из 14 проектов;
- платформенная база: shared core netstandard2.0; legacy Service/WPF net48; modern Engine net8.0 сейчас с целевым .NET 10 LTS;
- архитектура сборки x64;
- WPF-каркас;
- MSTest infrastructure with legacy `net48` and modern `net8.0` execution lanes;
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

## Исторические ограничения после Sprint 2

- SNMP transport ещё не реализован;
- discovery ещё не реализован;
- сбор инвентаря ещё не реализован;
- построение физической топологии ещё не реализовано;
- карта и мониторинг ещё не реализованы;
- текущая сборка x64;
- DPAPI CurrentUser требует отдельного решения для сервисной учётной записи при переходе к Windows Service.

## Исторический следующий шаг после Sprint 2

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

Sprint 32C is closed. The planned realistic stand gate and subsequent `FRICTION_LOG.md` review have now been completed; see the current execution gate and stand-acceptance section below.


## Current execution gate after Sprint 34A — completed

The architecture/master-plan review, realistic stand gate, modern test foundation, Sprint 33A, Sprint 33B, and Sprint 34A are complete.

Confirmed current facts:
- Sprint 32C implementation and documentation are closed;
- the post-closure neutral-resource correction is complete;
- the realistic 3–5 device stand gate is complete;
- the modern `net8.0` portable/core test lane is established alongside legacy `net48` compatibility/WPF-specific tests;
- Sprint 33A incremental WPF map reconciliation is implemented, operator-accepted, committed and pushed;
- Sprint 33B collision-safe topology link-label placement is implemented, operator-accepted, committed and pushed;
- Sprint 34A collects interface error/discard Counter32 values plus `ifCounterDiscontinuityTime` and provides portable wrap/discontinuity-safe interval delta evaluation;
- Sprint 34A deliberately does not persist previous interface counter samples, classify degradation, create incidents, or deliver notifications;
- the repeated LLDP link-label readability problem remains resolved and is no longer the active friction priority;
- no other recurring real-use friction is currently recorded;
- exactly one next product Sprint is committed: Sprint 34B — durable interface-counter baseline and restart-safe interval evaluation;
- product scope remains network observability/topology/diagnostics, not process-data acquisition or a generic NMS;
- the long architecture roadmap remains direction, not an automatic Sprint sequence.

Longer directions — current-state interface degradation classification, UI design tokens/themes, semantic animations, durable incidents/outbox, outbound notification, production configuration, runtime/deployment readiness, HTTP API/Web, probable failure-boundary localization, additional industrial protection and Site/Probe — remain roadmap/candidates unless separately committed.

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

The master-plan documentation alignment is also recorded. The realistic stand gate that followed this correction has now been completed; its result is recorded below.

## Realistic stand acceptance and LLDP topology baseline closure — 2026-09-13

The post-Sprint 32C realistic stand gate is complete.

Confirmed:
- the LLDP topology baseline implementation passed a forced solution build with 0 warnings and 0 errors;
- the full Unit, Integration and Snapshot regression passed without rebuilding the already-verified binaries;
- repository text-integrity and `git diff --check` passed;
- the realistic backend stand produced four named devices (`stand-sw-01` through `stand-sw-04`) and two confirmed LLDP physical links;
- operator acceptance in the WPF client showed the same four named devices, two links, and LLDP port labels;
- exact staged/index/commit boundary proof covered 23 implementation paths;
- implementation commit `18df16fc2d2dfdf7230a3ecfeac910b46a8f9c63` (`Implement LLDP topology baseline`) is pushed with `HEAD == origin/main` and a clean worktree;
- `Migration013LldpTopologyIdentity` is now the latest schema migration.

Observed friction at this gate:
- long LLDP link annotation text could be partially obscured by a neighboring node card; this was recorded in `FRICTION_LOG.md`;
- at that time it was a single observation, so Sprint 33A remained the selected next Sprint.

Sprint selected by this gate:
- Sprint 33A — incremental WPF map reconciliation keyed by stable `DeviceId` / `PhysicalLinkId`.

Sprint 33A has since been completed; its closure and the repeated-friction decision are recorded below.
## Modern test foundation and Sprint 33A closure — 2026-09-14

The dual-runtime test foundation and Sprint 33A are complete.

Modern test foundation:
- commit `ad6af4c2fbc2dc639ffb484b281df89be0bd0fce` added `tests/NetLoom.Tests.Modern/NetLoom.Tests.Modern.csproj`;
- the project targets `net8.0` and reuses compatible existing test sources rather than duplicating test logic;
- the foundation acceptance executed 16/16 modern tests, 204/204 legacy Unit tests, 59/59 Integration tests, and 7/7 Snapshot tests;
- WPF-specific tests remain in the legacy `net48` lane because the WPF client itself is still `net48`.

Sprint 33A implementation:
- `MapLink` now carries an optional stable `PhysicalLinkId` distinct from its presentation key;
- `MaterializedTopologyMapProjector` propagates the materialized physical-link identity into the map contract;
- WPF map apply is incremental instead of clearing and recreating the complete `MapCanvas`;
- retained device visuals are reconciled by stable `DeviceId`, with legacy presentation-key fallback where stable device identity is unavailable;
- retained physical-link visuals are reconciled by stable `PhysicalLinkId`, with map-link-key fallback for legacy/non-materialized snapshots;
- unchanged node/link visual objects are reused and updated in place; removed topology entities remove only their own visuals;
- retained node Canvas positions are preserved across refresh instead of being overwritten by newly projected coordinates;
- existing lookup highlight state remains applied to retained node visuals; passive periodic refresh no longer forces highlighted-device `BringIntoView()` navigation;
- Sprint 33A does not claim new zoom or pin UX: those controls/state are not currently exposed by the WPF client. The reconciliation boundary is structured so future client-owned view state can be retained without another full-redraw design.

Verification and acceptance:
- the reconciliation tests were demonstrated RED as 3/3 failures against the old full-redraw path before production changes;
- the legacy Unit test project explicitly enables WPF references for WPF-specific regression coverage;
- forced solution build passed;
- targeted GREEN passed: WPF reconciliation 3/3 and modern `net8.0` stable-physical-link identity 1/1;
- full regression passed: modern `net8.0` 17/17, legacy Unit 208/208, Integration 59/59, Snapshot 7/7;
- repository text-integrity and `git diff --check` passed;
- exact staged/index boundary proof covered six Sprint 33A files;
- operator acceptance used `artifacts/realistic-stand/operator-baseline.db` and kept the expected four devices and two LLDP links visible across multiple periodic refresh cycles;
- implementation commit `b5b546394361104e72ba73f84dab23538528d558` (`Implement incremental WPF map reconciliation`) is pushed with `HEAD == origin/main` and a clean worktree.

Repeated operational friction:
- the same long LLDP link-label overlap/readability problem was observed again during Sprint 33A operator acceptance;
- this is now a repeated real-use problem, so the backlog priority rule promotes it ahead of speculative candidates.

Next committed product Sprint:
- Sprint 33B — topology link-label readability and collision-safe placement. The implementation scope must be based on an audit of current WPF link-label geometry and should improve readability without changing topology identity or semantics.

No other product feature is committed at this point.

## Sprint 33B closure — 2026-09-14

Sprint 33B — topology link-label readability and collision-safe placement — is complete.

Implementation:
- the WPF link-label renderer no longer relies on a fixed midpoint offset for long physical-link annotations;
- the retained link-label `TextBlock` is measured before placement;
- placement candidates are evaluated around the link and rejected when the measured label rectangle intersects node-card rectangles;
- collision checks cover all current node cards, not only the two link endpoints;
- the Sprint 33A retained link visual identity is preserved across refresh;
- topology contracts, physical-link identity, projection semantics and backend persistence were not changed by Sprint 33B.

Verification and acceptance:
- two WPF link-label collision regressions were demonstrated RED as 2/2 failures against the old midpoint placement before the production change;
- forced solution build passed;
- targeted Sprint 33B GREEN passed 2/2;
- full regression passed: modern `net8.0` 17/17, legacy Unit 210/210, Integration 59/59, Snapshot 7/7;
- repository text-integrity and `git diff --check` passed;
- exact staged/index boundary proof covered two Sprint 33B files;
- operator acceptance used `artifacts/realistic-stand/operator-baseline.db`;
- the realistic topology remained four named devices and two LLDP physical links across multiple refresh cycles;
- both long LLDP annotations were fully readable and no longer obscured by node cards in the accepted stand layout;
- implementation commit `b7e6a8013055fff0cf9c58666d6b922fceabc717` (`Improve topology link label placement`) is pushed with `HEAD == origin/main` and a clean worktree.

Friction review after Sprint 33B:
- the recurring link-label readability item is resolved and should be reopened only if it is observed again in real use;
- no other recurring friction currently outranks the product plan.

Next product Sprint selected at Sprint 33B closure:
- Sprint 34A — interface degradation detection foundation.

Sprint 34A was later narrowed by source audit to the trustworthy interface-counter delta foundation recorded in the Sprint 34A closure below. In particular, the completed Sprint did not add `ifLastChange`, persistence, thresholds, incidents, or outbound delivery.

## Sprint 34A closure — 2026-09-14

Sprint 34A — interface counter delta foundation — is complete.

Implementation:
- `SnmpInterfaceStatusCollector` now walks `ifInErrors`, `ifOutErrors`, `ifInDiscards`, `ifOutDiscards`, and `ifCounterDiscontinuityTime` in addition to interface admin/oper status;
- `InterfaceMonitoringSnapshot` carries nullable raw Counter32 values for interface errors/discards plus nullable `CounterDiscontinuityTimeTicks`, while preserving stable `DeviceId` + `ifIndex` identity and UTC capture-time validation;
- the portable `InterfaceCounterDeltaEvaluator` reports explicit `NoBaseline`, `Valid`, or `Discontinuity` status;
- the evaluator requires the same stable interface identity and a strictly newer current sample;
- when the discontinuity marker is stable, a Counter32 decrease is interpreted with unsigned 32-bit wrap semantics;
- when the discontinuity marker changes, the result is `Discontinuity` and no counter deltas are produced;
- when either sample lacks the discontinuity marker, the result is `NoBaseline` and no delta is invented;
- individual missing raw counters remain nullable in an otherwise valid interval result;
- the completed scope did not add `ifLastChange`, persistence of previous samples, degradation thresholds/classification, incident lifecycle, or outbound notification delivery.

Verification:
- six Sprint 34A regression tests were demonstrated RED on both legacy `net48` and modern `net8.0` before production changes;
- targeted GREEN passed 6/6 on legacy and 6/6 on modern;
- forced solution build passed;
- full regression passed: modern `net8.0` 23/23, legacy Unit 216/216, Integration 59/59, Snapshot 7/7;
- repository text-integrity and `git diff --check` passed;
- exact staged/index/commit boundary proof covered eight Sprint 34A files;
- implementation commit `05d1cd00eff04ed77f2befbfc59d1c3878f61e38` is pushed with `HEAD == origin/main` and a clean worktree.

Next committed product Sprint:
- Sprint 34B — durable interface-counter baseline and restart-safe interval evaluation;
- persist the latest trustworthy raw counter snapshot by stable `DeviceId` + `ifIndex`, restore it after Engine restart, reject non-newer/mismatched samples, and advance baseline state without turning `NoBaseline` or `Discontinuity` into false degradation;
- current-state degradation classification, durable incident lifecycle, and outbound delivery remain separate later steps.

No other product feature is committed at this point.

## Sprint 34B closure — 2026-09-14

Sprint 34B — durable interface-counter baseline and restart-safe interval evaluation — is complete.

Implementation:
- `IInterfaceCounterBaselineStore` defines a persistence-neutral boundary that returns the previous raw sample while replacing it with the current sample;
- `SqliteInterfaceCounterBaselineStore` persists one raw baseline per stable `DeviceId` + `ifIndex`;
- the durable row stores `CapturedUtc`, `ifInErrors`, `ifOutErrors`, `ifInDiscards`, `ifOutDiscards`, and `ifCounterDiscontinuityTime` ticks rather than a derived delta;
- replacement executes under the existing SQLite immediate-write boundary so the previous-sample read and current-sample write are atomic with respect to competing writers;
- a sample whose `CapturedUtc` is not strictly newer than the stored sample is rejected and cannot replace the durable baseline;
- a new runtime instance can recover the persisted sample and feed it back into the existing Sprint 34A `InterfaceCounterDeltaEvaluator`;
- the Interface poll result now exposes `InterfaceCounterEvaluation` values alongside raw interface snapshots when a stable `DeviceId` and baseline store are available;
- the first durable sample evaluates as `NoBaseline`;
- a changed discontinuity marker evaluates as `Discontinuity`, produces no synthetic error/discard delta, and the current sample becomes the next durable baseline;
- the sample after that discontinuity can evaluate normally after restart when the marker remains stable;
- interfaces without stable `DeviceId` are not assigned durable counter identity;
- Engine composition wires the SQLite baseline store into `MonitoringRuntime`;
- `Migration014InterfaceCounterBaselines` creates the durable baseline table and is now the latest schema migration.

Verification:
- a reflection contract test was demonstrated RED 1/1 on the pre-Sprint 34B base before implementation;
- targeted persistence GREEN passed 4/4 on legacy `net48` and 4/4 on modern `net8.0`;
- targeted runtime GREEN passed 2/2 on legacy and 2/2 on modern;
- the first full Integration run exposed one stale migration-count regression in `Sprint26ASqliteConcurrencyTests`: the old test expected 13 schema migrations after migration 014 had correctly raised the total to 14;
- recovery changed only that stale regression expectation and renamed its already-outdated method from an exact-count name to `ConcurrentInitializeOnCleanDatabaseAppliesAllMigrations`; product bytes were unchanged;
- the already-green targeted, modern, and Unit evidence was preserved from the failed run rather than rerun without a source reason;
- after the regression-test correction, a forced solution build passed, Integration passed 63/63, and Snapshot passed 7/7;
- final regression evidence is modern `net8.0` 29/29, legacy Unit 218/218, Integration 63/63, Snapshot 7/7;
- repository text-integrity and `git diff --check` passed;
- exact staged/index/commit boundary proof covered fourteen files, including the corrected concurrency regression;
- implementation/recovery commit `a63ec3e5ea7adf4fc88cdb2f10ee9f404310bb7a` (`Add durable interface counter baselines`) is pushed with `HEAD == origin/main` and a clean worktree.

Scope deliberately still deferred:
- no interface-degradation threshold or severity classification;
- no `ifLastChange` collection was added by Sprint 34B;
- no durable incident lifecycle, repeat/escalation policy, outbox, or outbound notification adapter;
- no high-frequency metric/time-series data was moved into topology/configuration SQLite; the new table holds only the latest raw baseline per stable interface identity.

Next committed product Sprint:
- Sprint 34C — current-state interface degradation classification;
- start with a read-only audit of the portable 34A/34B counter-evaluation path and existing configuration conventions, then define the smallest threshold/classification contract that can consume only `Valid` restart-safe intervals;
- `NoBaseline` and `Discontinuity` remain explicitly non-degrading inputs;
- durable incident lifecycle and outbound notification delivery remain separate later steps.

No other product feature is committed at this point.

## Sprint 34C closure — 2026-09-14

Sprint 34C — current-state interface degradation classification — is complete.

Implementation:
- `InterfaceDegradationStatus` defines explicit `Indeterminate`, `Healthy`, and `Degraded` states;
- `InterfaceDegradationPolicy` requires at least one configured finite positive threshold and supports independent error-rate and discard-rate thresholds;
- `InterfaceDegradationClassifier` consumes the restart-safe `InterfaceCounterEvaluation` produced by Sprints 34A/34B;
- `NoBaseline` maps to `Indeterminate` with reason `NoBaseline`;
- `Discontinuity` maps to `Indeterminate` with reason `CounterDiscontinuity`;
- only `Valid` intervals are eligible for threshold classification;
- enabled error and discard categories use the sum of inbound and outbound counter increments normalized to the actual interval as a per-minute rate;
- a rate equal to or greater than its configured threshold is `Degraded`;
- missing counters for an enabled category produce `IncompleteCounterData`; if no known enabled category is already degraded, classification is `Indeterminate`;
- a known threshold breach remains `Degraded` even when another enabled category has incomplete evidence;
- a disabled category does not prevent `Healthy` classification;
- classifications retain stable `DeviceId`, `ifIndex`, UTC capture time, calculated rates, and normalized reasons;
- `MonitoringRuntime` emits interface degradation classifications alongside raw interface snapshots and counter evaluations when a policy is configured;
- Engine classification is opt-in through `--interface-error-rate-per-minute` and/or `--interface-discard-rate-per-minute`;
- with neither option configured, no degradation policy is created and the existing interface polling path remains classification-free;
- Engine console output exposes configured results as `INTERFACE-DEGRADATION` lines;
- Sprint 34C intentionally does not mix `ifAdminStatus`, `ifOperStatus`, or a new `ifLastChange` signal into counter degradation. Hard-down/failure semantics remain a separate concern.

Verification:
- the Sprint 34C contract/classification suite was demonstrated RED as 10/10 legacy failures on the pre-Sprint base;
- targeted GREEN passed 12/12 on legacy `net48` and 12/12 on modern `net8.0`;
- forced solution build passed;
- full regression passed: modern `net8.0` 41/41, legacy Unit 230/230, Integration 63/63, Snapshot 7/7;
- repository text-integrity and `git diff --check` passed;
- exact staged/index/commit boundary proof covered thirteen Sprint 34C files;
- implementation commit `8c5d10aa82e0387bf111fafb0a5379df21bdf943` (`Add interface degradation classification`) is pushed with `HEAD == origin/main` and a clean worktree.

Scope deliberately still deferred:
- no durable previous-classification/current-state transition store;
- no restart-safe first-appearance / changed / resolved suppression for interface degradation;
- no durable incident lifecycle or repeat/escalation timer;
- no persistent outbox and no outbound notification adapter;
- no `ifLastChange` collection and no attempt to collapse oper-down failure semantics into counter-degradation semantics.

Next committed product Sprint:
- Sprint 34D — durable interface-degradation transition state and repeat suppression;
- begin with a read-only audit of the existing `TopologyAlertTransitionTracker`, its restart behavior, and SQLite persistence/write conventions;
- persist only the minimum stable interface classification state needed to distinguish first appearance, unchanged state, changed degradation evidence, and resolution across Engine restart;
- keep persistent outbox/delivery semantics and external notification adapters as separate later work.

No other product feature is committed at this point.

## Sprint 34D closure — 2026-09-14

Sprint 34D — durable interface-degradation transition state and repeat suppression — is complete.

Implementation:
- `IInterfaceDegradationStateStore` defines the persistence-neutral current-state boundary for interface degradation transitions;
- `InterfaceDegradationState` stores only determinate `Healthy` or `Degraded` state for stable `DeviceId` + `ifIndex`, UTC capture time, and a canonical degraded-evidence fingerprint;
- `Indeterminate` is explicitly not a durable state and therefore cannot erase the last determinate state;
- degraded evidence fingerprints are derived from canonical classification reasons rather than volatile numeric rates, so ordinary rate movement does not create false `Changed` transitions;
- `InterfaceDegradationTransitionTracker` produces `Unchanged`, `FirstAppearance`, `Changed`, `Resolved`, or `Indeterminate`;
- a first observed `Healthy` state is `Unchanged`, not an artificial recovery;
- `Healthy -> Degraded` is `FirstAppearance`;
- repeated equivalent `Degraded` is `Unchanged`, including after Engine restart;
- `Degraded` with changed canonical evidence is `Changed`;
- `Degraded -> Healthy` is `Resolved`;
- `Degraded -> Indeterminate -> equivalent Degraded` preserves the durable degraded state and returns to `Unchanged`, rather than creating a false resolution or appearance;
- `SqliteInterfaceDegradationStateStore` persists the latest determinate state by `DeviceId` + `ifIndex`, rejects non-newer timestamps, and uses the existing immediate-write boundary for atomic previous-state read/replacement;
- `Migration015InterfaceDegradationStates` creates the durable state table and is now the latest schema migration;
- `MonitoringRuntime` exposes interface degradation transitions alongside classifications;
- Engine composition wires the SQLite degradation-state store into the monitoring runtime;
- Engine emits `INTERFACE-DEGRADATION-TRANSITION` output only when `HasStateChange` is true, so `Unchanged` and `Indeterminate` do not create repeated transition output;
- Sprint 34D does not add a persistent delivery queue or any external notification adapter.

Verification:
- the durable transition contract was demonstrated RED 1/1 on the pre-Sprint 34D base;
- forced solution build passed;
- targeted GREEN passed: Integration 6/6, legacy Unit 6/6, modern `net8.0` 12/12;
- full regression passed: modern `net8.0` 53/53, legacy Unit 236/236, Integration 69/69, Snapshot 7/7;
- repository text-integrity and `git diff --check` passed;
- exact staged/index/commit boundary proof covered nineteen Sprint 34D files;
- schema-count expectations were advanced together with migration 015, including the SQLite concurrency regression, avoiding the stale-count failure previously encountered in Sprint 34B;
- implementation commit `24ed7d421006899bb5b32edee496a1b2c323529e` (`Add durable interface degradation transitions`) is pushed with `HEAD == origin/main` and a clean worktree.

Scope deliberately still deferred:
- no persistent delivery/outbox row is created for a transition;
- no crash-safe atomic boundary yet couples transition-state advancement with durable event enqueue;
- no external notification adapter, acknowledgement, retry, escalation, or dead-letter semantics;
- no attempt to convert `Indeterminate` into a recovery signal;
- no hard-down interface failure model is collapsed into counter degradation.

Next committed product Sprint:
- Sprint 34E — durable interface-degradation event outbox;
- begin with a read-only audit of the 34D state-transition persistence path, existing SQLite transaction helpers, and any existing alert/event delivery abstractions;
- persist only meaningful `FirstAppearance`, `Changed`, and `Resolved` events;
- require an atomic crash-safe boundary between advancing durable transition state and enqueueing the corresponding outbox event so a restart cannot silently lose a real transition;
- `Unchanged` and `Indeterminate` remain non-delivery states;
- external adapters and delivery acknowledgement/retry policy remain separate later steps.

No other product feature is committed at this point.

## Sprint 34E closure — 2026-09-14

Sprint 34E — durable interface-degradation event outbox — is complete.

Implementation:
- `InterfaceDegradationTransitionEvaluator` now owns pure `Indeterminate` / `Unchanged` / `FirstAppearance` / `Changed` / `Resolved` evaluation independently of persistence;
- `IInterfaceDegradationTransitionProcessor` is the runtime boundary used to observe a classified interface state;
- the existing 34D tracker remains compatible while SQLite composition uses `SqliteInterfaceDegradationTransitionProcessor`;
- the SQLite processor performs determinate previous-state load, transition evaluation, state advancement, and meaningful outbox enqueue inside one `SqliteImmediateWrite` transaction;
- `Indeterminate` reads previous determinate state without advancing it and never enqueues an event;
- initial `Healthy` and repeated equivalent `Degraded` remain `Unchanged` and never enqueue an event;
- `FirstAppearance`, `Changed`, and `Resolved` each enqueue exactly one immutable `InterfaceDegradationOutboxEvent`;
- `InterfaceDegradationOutboxEvent` captures stable `DeviceId` + `ifIndex`, UTC capture time, transition kind, previous/current status and evidence fingerprints, current error/discard rates, normalized reasons, and a deterministic immutable event key;
- deterministic event keys are validated when rows are read and serve as the SQLite primary key/idempotency boundary;
- `SqliteInterfaceDegradationEventOutbox.ReadPending(maxCount)` returns pending events in stable capture-time/event-key order;
- `Migration016InterfaceDegradationOutbox` creates `interface_degradation_outbox` and is now the latest schema migration;
- a synthetic SQLite trigger test proves that an outbox insert failure rolls back both the determinate-state update and the event insert; retrying the same observation after removing the trigger correctly produces the original transition and exactly one event;
- Engine composition wires the transactional SQLite processor so the runtime cannot advance durable transition state through a separate non-atomic persistence path.

Verification:
- the durable outbox contract was demonstrated RED 1/1 on the pre-Sprint 34E base;
- forced solution build passed;
- targeted GREEN passed: Integration 6/6 and modern `net8.0` 6/6;
- full regression passed: modern `net8.0` 59/59, legacy Unit 236/236, Integration 75/75, Snapshot 7/7;
- repository text-integrity and `git diff --check` passed;
- exact staged/index/commit boundary proof covered eighteen Sprint 34E files;
- schema-count expectations were advanced together with migration 016;
- implementation commit `a92ee3bbfe7b0bda0405bd97417f299391a6e3b4` (`Add durable interface degradation outbox`) is pushed with `HEAD == origin/main` and a clean worktree.

Scope deliberately still deferred:
- no external notification adapter sends an outbox event yet;
- no event acknowledgement/removal/delivered marker is applied by an external delivery result;
- no retry/backoff, escalation, or dead-letter policy;
- no multi-adapter routing or fan-out;
- no credential/secrets mechanism was invented before a real adapter is selected.

Next committed product Sprint:
- Sprint 34F — first outbound interface-degradation delivery path;
- begin with a read-only audit of the 34E outbox API, Engine hosting/lifecycle boundaries, existing configuration/secrets conventions, and real deployment constraints;
- choose exactly one real outbound adapter only after that evidence is available;
- define delivery acknowledgement so a pending event is never deleted or marked delivered before confirmed adapter success;
- preserve pending events across restart and delivery failure;
- keep routing sophistication, multi-adapter fan-out, escalation, and dead-letter policy as later work.

No other product feature is committed at this point.

## Sprint 34F closure — 2026-09-14

Sprint 34F — first outbound interface-degradation delivery path — is complete.

Implementation:
- SMTP relay is the first concrete outbound adapter for durable interface-degradation events;
- `IInterfaceDegradationDeliveryAdapter` keeps the Application delivery boundary transport-neutral;
- `InterfaceDegradationOutboxDispatcher` reads pending durable events, invokes the adapter, and acknowledges an event only after adapter success;
- `SqliteInterfaceDegradationEventOutbox.ReadPending(maxCount)` now returns only rows whose delivery acknowledgement is still absent;
- `SqliteInterfaceDegradationEventOutbox.MarkDelivered(eventKey, deliveredUtc)` records an idempotent UTC acknowledgement for an existing event and never removes the immutable event row;
- `Migration017InterfaceDegradationDelivery` adds durable `delivered_utc` acknowledgement state and the pending-order index, and is now the latest schema migration;
- `SmtpInterfaceDegradationDeliveryAdapter` sends a UTF-8 text notification containing stable event identity, device/interface identity, transition, previous/current state, current rates, and normalized reasons;
- SMTP configuration uses environment variables rather than command-line secrets: `NETLOOM_SMTP_HOST`, optional `NETLOOM_SMTP_PORT`, optional `NETLOOM_SMTP_ENABLE_SSL`, `NETLOOM_SMTP_FROM`, `NETLOOM_SMTP_TO`, optional `NETLOOM_SMTP_USERNAME`, and optional `NETLOOM_SMTP_PASSWORD`;
- username/password must be configured together; absent SMTP configuration leaves the existing monitoring path unchanged and pending events remain durable;
- Engine drains at most 32 pending interface-degradation events after `poll-once` and after each scheduled monitoring cycle when SMTP is configured;
- adapter exceptions are logged and surfaced to stderr without acknowledging the event or stopping the monitoring loop;
- semantics are deliberately at-least-once: if SMTP accepts a message but SQLite acknowledgement subsequently fails, the durable event remains pending and may be sent again after restart rather than being silently lost.

Verification:
- the delivery acknowledgement contract was demonstrated RED 1/1 on the pre-Sprint 34F base;
- forced solution build passed;
- targeted GREEN passed: Integration 4/4, legacy Unit 4/4, modern `net8.0` 8/8;
- full modern regression passed 67/67 and legacy Unit passed 240/240 before the first full Integration run exposed one stale migration-count expectation in `DatabaseInitializationTests.InitializeCreatesSchemaAndIsIdempotent`;
- recovery changed only that regression expectation from 16 to 17 migrations; Sprint 34F product bytes were unchanged;
- the already-green targeted, modern, and Unit evidence was preserved rather than rerun without a source reason;
- after the regression-test correction, a forced solution build passed, Integration passed 79/79, and Snapshot passed 7/7;
- final regression evidence is modern `net8.0` 67/67, legacy Unit 240/240, Integration 79/79, Snapshot 7/7;
- repository text-integrity and `git diff --check` passed;
- exact staged/index/commit boundary proof covered fifteen files, including the corrected database-initialization regression;
- implementation/recovery commit `14f1a87b961939f56d24844f2b5793571d2f6b63` (`Add SMTP interface degradation delivery`) is pushed with `HEAD == origin/main` and a clean worktree.

Scope deliberately still deferred:
- no persisted delivery attempt count, next-attempt time, or backoff schedule;
- no dead-letter terminal state, escalation, or operator acknowledgement workflow;
- no multi-adapter routing or fan-out;
- no claim that SMTP acceptance means a human read the notification;
- no production secrets vault/configuration boundary beyond the existing environment-variable convention;
- no hard-down interface failure model is collapsed into counter degradation.

Next committed product Sprint:
- Sprint 34G — durable delivery retry scheduling and backoff;
- begin with a read-only audit of the 34F SMTP failure/acknowledgement path and Engine polling cadence;
- persist only the minimum attempt metadata required to compute restart-safe retry eligibility;
- avoid retrying a failing relay on every monitoring poll while never deleting or acknowledging a failed event;
- preserve at-least-once delivery semantics;
- keep dead-letter disposition, escalation, routing, and multi-adapter fan-out as later policy.

No other product feature is committed at this point.

## Sprint 34G closure — 2026-09-15

Sprint 34G — durable delivery retry scheduling and backoff — is complete.

Implementation:
- `InterfaceDegradationPendingDelivery` carries the immutable outbox event together with durable retry state;
- `IInterfaceDegradationEventOutbox` now separates administrative pending reads from delivery eligibility reads through `ReadReady(maxCount, eligibleUtc)`;
- failed delivery state is persisted through `MarkDeliveryFailed(eventKey, expectedFailureCount, failedUtc, nextAttemptUtc)`;
- `InterfaceDegradationDeliveryRetryPolicy` is a persistence-neutral pure Application policy rather than SQLite timing logic;
- retry delay is deterministic exponential backoff: 1 minute after the first failure, then 2, 4, 8, 16, 32, and a 60-minute cap for subsequent failures;
- `InterfaceDegradationOutboxDispatcher` asks the outbox only for events eligible at the current UTC time;
- adapter failure is durably recorded before the exception returns to the existing Engine delivery-failure boundary;
- the persisted failure count is used to calculate the next retry, so Engine restart cannot reset retry pressure;
- a failed event remains pending while `ReadReady` suppresses it until `next_delivery_attempt_utc`;
- successful retry retains Sprint 34F acknowledgement semantics: an event is marked delivered only after adapter success;
- the delivery path remains at-least-once, including the existing case where adapter success is followed by acknowledgement persistence failure;
- `Migration018InterfaceDegradationDeliveryRetry` adds `delivery_failure_count`, `last_delivery_failure_utc`, `next_delivery_attempt_utc`, and a delivery-readiness index, and is now the latest schema migration;
- Engine uses the bounded 1-minute-to-60-minute retry policy without adding new credential or command-line configuration;
- dead-letter disposition, escalation, routing, and multi-adapter fan-out remain outside this Sprint.

Verification:
- the durable retry contract was demonstrated RED 1/1 on the pre-Sprint 34G base;
- forced solution build passed;
- targeted GREEN passed: Integration 4/4, legacy Unit 4/4, modern `net8.0` 8/8;
- full regression passed: modern `net8.0` 75/75, legacy Unit 244/244, Integration 83/83, Snapshot 7/7;
- repository text-integrity and `git diff --check` passed;
- exact staged/index/commit boundary proof covered fifteen Sprint 34G files;
- both known migration-count regressions were advanced together with migration 018, avoiding the stale-count failures previously encountered in Sprints 34B and 34F;
- implementation commit `e4b58a6e3b0382a955ea166313f6c2440c264546` (`Add durable delivery retry backoff`) is pushed with `HEAD == origin/main` and a clean worktree.

Scope deliberately still deferred:
- no operator-facing read-only view yet distinguishes immediately ready events from retry-deferred events;
- no real-relay acceptance evidence is recorded yet for target SMTP TLS/auth behavior;
- no dead-letter terminal state, escalation, or operator acknowledgement workflow;
- no multi-adapter routing or fan-out;
- no production secrets vault/configuration layer beyond environment variables;
- no claim that SMTP acceptance means a human read the notification.

Next committed product Sprint:
- Sprint 34H — operator-facing delivery state and SMTP acceptance;
- begin with a read-only audit of the Engine command/output surface and the 34G outbox query model;
- expose delivery state without leaking SMTP credentials: at minimum ready versus retry-deferred pending events and the relevant retry timing/evidence needed for diagnosis;
- add a repeatable acceptance path that uses user-supplied environment configuration against a real or controlled SMTP relay and produces evidence for successful acknowledgement and failed/deferred retry behavior;
- preserve the current at-least-once contract and durable retry state;
- do not invent dead-letter thresholds or escalation policy before operational evidence exists.

No other product feature is committed at this point.

## Sprint 34H closure — 2026-09-15

Sprint 34H — operator-facing delivery state and SMTP acceptance — is complete.

Implementation:
- a read-only `IInterfaceDegradationDeliveryStatusReader` boundary separates operator inspection from the existing delivery write/acknowledgement contract;
- `InterfaceDegradationDeliveryStatusKind` classifies durable outbox evidence as `Ready`, `Deferred`, or `Delivered`;
- `InterfaceDegradationDeliveryStatus` carries immutable event identity plus failure count, last failure UTC, next eligible attempt UTC, and delivered UTC without exposing SMTP credentials;
- `SqliteInterfaceDegradationEventOutbox.ReadStatus(maxCount, nowUtc)` reads recent durable delivery history and derives current operator state from the existing Sprint 34F/34G columns; Sprint 34H adds no database migration;
- Engine command `delivery-status` accepts `--database` and optional `--limit`, does not require a polling address, and prints per-event state plus a ready/deferred/delivered summary;
- `delivery-status` does not read or print `NETLOOM_SMTP_*` configuration;
- Engine command `smtp-acceptance` takes no command-line credentials and sends a synthetic canary through the same `SmtpInterfaceDegradationDeliveryAdapter` and environment-variable configuration used by production delivery;
- the acceptance canary is not inserted into the durable interface-degradation outbox;
- successful SMTP acceptance returns a stable success marker; failure returns a stable error class marker without printing secret values;
- a modern acceptance test uses a controlled loopback `TcpListener` SMTP relay and proves that the production adapter completes a real SMTP conversation;
- the controlled-relay test validates the actual MIME behavior of `SmtpClient`: the UTF-8 message body is base64-encoded and must be decoded before asserting the event payload.

Verification:
- the operator delivery-status contract was demonstrated RED 1/1 on the pre-Sprint 34H base;
- the first forced solution build passed;
- targeted Integration passed 5/5;
- the first targeted modern run reached the real controlled SMTP relay and passed 8/9, with the only failure caused by the test incorrectly searching for `EventKey` in the raw MIME/base64 body rather than decoding it;
- recovery changed only the SMTP acceptance test assertion; Sprint 34H product bytes were unchanged;
- the previous Integration 5/5 evidence and exact modern 8/9 MIME assertion failure were verified before recovery;
- after the test correction, a forced solution build passed and targeted modern passed 9/9;
- the explicit `smtp-acceptance` missing-configuration probe returned the expected failure code and secret-free diagnostic;
- full regression passed: modern `net8.0` 84/84, legacy Unit 244/244, Integration 88/88, Snapshot 7/7;
- repository text-integrity and `git diff --check` passed;
- exact staged/index/commit boundary proof covered twelve Sprint 34H files;
- implementation/recovery commit `0a8ee620eb28ca24232563f8c9119c5e3bc1a5c4` (`Add operator delivery status and SMTP acceptance`) is pushed with `HEAD == origin/main` and a clean worktree.

Scope deliberately still deferred:
- no real target SMTP relay has yet been exercised with the operator's actual deployment TLS/auth configuration;
- no dead-letter terminal state, escalation threshold, or manual operator acknowledgement workflow;
- no multi-adapter routing or fan-out;
- no production secret store beyond environment variables;
- no claim that SMTP relay acceptance means recipient delivery or human acknowledgement;
- no schema change beyond `Migration018InterfaceDegradationDeliveryRetry`.

Next committed validation Sprint:
- Sprint 34I — real target-relay/operator acceptance and evidence review;
- run `smtp-acceptance` using operator-supplied environment variables against the intended deployment relay without storing credentials in source, logs, or command-line history;
- run `delivery-status` against a real working NetLoom database and capture ready/deferred/delivered evidence;
- record observed TLS/auth compatibility, relay acceptance behavior, retry behavior, and diagnostics;
- do not invent dead-letter/escalation thresholds or a new production secret mechanism before that evidence exists;
- if acceptance is green, choose the next product Sprint from the observed evidence and the current `FRICTION_LOG.md`.

No other product feature is committed at this point.

## Sprint 34I environment acceptance review — 2026-09-15

Sprint 34I did not change product source or repository state. The planned real deployment acceptance gate was attempted and is currently blocked by missing deployment prerequisites.

Observed evidence:
- repository baseline was `f9b32137454ac1adf186721fc29d388e5ce48464` with `HEAD == origin/main` and a clean worktree;
- target SMTP configuration is not present in the current environment, so real target-relay acceptance cannot be executed without inventing infrastructure values;
- seven local database candidates were discovered:
  - `C:\netloom\artifacts\netloom-demo.db`;
  - `C:\netloom\artifacts\netloom-lldp.db`;
  - `C:\netloom\artifacts\netloom-two-switch.db`;
  - `C:\netloom\artifacts\realistic-stand\four-device.db`;
  - `C:\netloom\artifacts\realistic-stand\one-device.db`;
  - `C:\netloom\artifacts\realistic-stand\operator-baseline.db`;
  - `%LOCALAPPDATA%\NetLoom\netloom.db`;
- all seven databases predate `interface_degradation_outbox`, so none can provide real 34H `delivery-status` evidence without first being used or migrated by a current-schema workflow;
- the environment audit itself was read-only and ended with `REPOSITORY_UNCHANGED=YES`;
- the 34H controlled-loopback SMTP acceptance, targeted tests, and full regression remain the latest positive product evidence;
- therefore Sprint 34I does not establish target-relay TLS/auth compatibility, recipient relay acceptance, production retry behavior, or real outbox state.

Concrete operator friction discovered:
- the first `delivery-status` attempt against the existing default database surfaced raw SQLite `no such table: interface_degradation_outbox` and exit code 2;
- this is not evidence of corruption or a delivery regression: the selected database simply predates migration 016;
- however, raw storage-engine output is not an acceptable operator diagnostic for an intentionally read-only inspection command.

Decision:
- do not fabricate SMTP host/port/account values and do not declare the real target-relay gate passed;
- do not mutate or silently migrate an operator-selected database from the read-only `delivery-status` path;
- do not introduce dead-letter/escalation policy from absent deployment evidence;
- address the observed operator friction first.

Next committed product Sprint:
- Sprint 34J — stable legacy-schema diagnostics for `delivery-status`;
- detect pre-outbox schema before the outbox query;
- return a stable schema-compatibility diagnostic and dedicated nonzero exit code without raw SQLite SQL text;
- preserve read-only database behavior;
- prove a legacy database is unchanged by the diagnostic path;
- prove a current-schema empty database still returns the normal zero-event delivery summary;
- keep real target SMTP acceptance deferred until an actual deployment relay configuration exists.

No dead-letter, escalation, routing, secret-store, or schema migration change is committed by this decision.

## Sprint 34J closure — 2026-09-15

Sprint 34J — stable legacy-schema diagnostics for `delivery-status` — is complete.

Implementation:
- `SqliteConnectionFactory.OpenReadOnlyConnection()` provides a genuine SQLite read-only path with `ReadOnly=true`, `FailIfMissing=true`, and no normal connection-side WAL/bootstrap behavior;
- `SqliteInterfaceDegradationDeliveryStatusSchemaProbe` inspects `PRAGMA table_info(interface_degradation_outbox)` and requires the complete column set consumed by the current delivery-status reader;
- a database with no outbox table or only an incomplete outbox schema is treated as unsupported rather than being queried optimistically;
- Engine `delivery-status` now returns `ERROR: DELIVERY_STATUS_SCHEMA_UNSUPPORTED` and exit code 7 for an unsupported schema before constructing the status reader;
- `SqliteInterfaceDegradationEventOutbox.ReadStatus()` itself uses the new read-only connection path;
- the command does not migrate, initialize, or otherwise mutate an operator-selected database;
- a current-schema empty database remains compatible and returns the normal zero-event status result;
- Sprint 34J adds no migration and leaves `Migration018InterfaceDegradationDeliveryRetry` as the current schema migration.

Verification:
- deterministic RED proved the read-only compatibility boundary absent on the pre-Sprint 34J base: 1/1 failed as expected;
- forced solution build passed;
- targeted GREEN passed: Integration 4/4 and modern `net8.0` 4/4;
- the actual `%LOCALAPPDATA%\NetLoom\netloom.db` observed during Sprint 34I was exercised directly and returned stable exit code 7 plus `ERROR: DELIVERY_STATUS_SCHEMA_UNSUPPORTED`;
- the same observed legacy database was SHA256-identical before and after the read-only command;
- raw SQLite `no such table` output was not exposed by the corrected operator path;
- full regression passed: modern `net8.0` 88/88, legacy Unit 244/244, Integration 92/92, Snapshot 7/7;
- repository text-integrity and `git diff --check` passed;
- exact staged/index/commit boundary proof covered seven Sprint 34J files;
- implementation commit `a355049ce798da196c71b552b15433feb2000d22` (`Add stable delivery status schema diagnostics`) is pushed with `HEAD == origin/main` and a clean worktree.

Remaining deployment evidence:
- target SMTP relay configuration is still absent in the current environment, so no real target-relay TLS/auth acceptance claim is made;
- all seven databases found during the Sprint 34I environment audit remain pre-outbox artifacts, although they are now diagnosed cleanly rather than surfacing raw SQL errors;
- the controlled SMTP relay acceptance from Sprint 34H and the durable retry evidence from Sprint 34G remain the latest positive transport/retry evidence;
- no dead-letter, escalation, routing, or production secret-store behavior is justified by the currently available evidence.

Next committed product Sprint:
- Sprint 34K — actionable SMTP configuration readiness diagnostics;
- expose a read-only Engine command/status that reports whether required SMTP configuration is structurally ready without printing host, addresses, usernames, or passwords;
- distinguish at minimum missing host/from/to, invalid port, invalid SSL boolean, incomplete username/password pairing, and structurally ready configuration;
- make `smtp-acceptance` use the same readiness evaluation before it attempts a network connection so the operator receives a stable reason rather than a generic exception type;
- preserve the existing environment-variable contract, SMTP adapter behavior, and at-least-once delivery semantics;
- do not invent relay values, persist credentials, or add a secret-store architecture in this Sprint.

No dead-letter, escalation, routing, or new delivery transport is committed at this point.
