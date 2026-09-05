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

еализовано:
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

еализовано:
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

еализовано:
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

еализовано:
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

еализовано:
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

еализовано:
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

еализовано:
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

еализовано:
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

- ктивный cross-cutting backlog ведётся в `docs/BACKLOG.md`.
- еальные проблемы эксплуатации фиксируются в `docs/FRICTION_LOG.md`.
- Third-party зависимости учитываются в `THIRD-PARTY.md`.
- ользовательские строки WPF переводятся на resource-based localization.
- linux-x64 publish считается проверкой совместимости сборки/публикации; полноценная runtime-проверка Linux будет добавлена после появления реальной логики в NetLoom.Engine.