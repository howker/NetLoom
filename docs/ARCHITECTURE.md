# ARCHITECTURE

## Назначение

NetLoom состоит из нескольких слоёв:

Network
  ->
Collectors
  ->
Observations / Evidence
  ->
Topology Resolver
  ->
Domain Model
  ->
WPF Map

## Главное правило

Полученный SNMP, LLDP, CDP, FDB, ARP или STP результат является наблюдением, а не сразу фактом физической топологии.

## Проекты

- NetLoom.Domain — доменная модель.
- NetLoom.Application — сценарии использования.
- NetLoom.Contracts — DTO и команды.
- NetLoom.Protocols.Snmp — SNMP, LLDP, CDP, FDB, ARP, STP collectors.
- NetLoom.Topology — identity, link, ring и STP resolver.
- NetLoom.Persistence.Sqlite — SQLite, миграции, репозитории.
- NetLoom.Service — фоновый мониторинг.
- NetLoom.Wpf — русскоязычный интерфейс.

## Ключевые ограничения

- IP не является DeviceId.
- FDB не является физической связью.
- bridge-port не равен ifIndex без dot1dBasePortIfIndex.
- Ручные устройства участвуют в физическом графе.
- Мониторинг может быть полностью остановлен.
- UI не должен владеть domain state.

## Cross-platform baseline — Sprint 7.5

Эта секция имеет приоритет над прежними Windows-only формулировками.

- Portable core: `NetLoom.Domain`, `NetLoom.Application`, `NetLoom.Contracts`, `NetLoom.Topology` → `netstandard2.0`.
- Adapters: `NetLoom.Protocols.Snmp`, `NetLoom.Persistence.Sqlite` → `net48;net8.0`.
- `NetLoom.Service` и `NetLoom.Wpf` остаются `net48` для legacy Windows deployment и совместимости с Windows 8.1.
- `NetLoom.Engine` — modern Windows/Linux backend; сейчас `net8.0` под SDK 8.0.424, целевой production runtime — .NET 10 LTS после отдельного обновления toolchain.
- DPAPI — только Windows-реализация `ISecretProtector`; Linux secret protector добавляется отдельно.
- UI ↔ backend определяется transport-neutral contracts. Named Pipes — только возможный локальный Windows transport. Сетевой transport для Linux/remote будет выбран отдельным ADR.
- WPF после service split не пишет SQLite напрямую.
- Основной backend остаётся C#/.NET. Go допускается только как возможный будущий `NetLoom.Probe`.

## Topology resolution boundary

Pipeline физической топологии:

`raw observations → normalized evidence → correlation → PhysicalLinkCandidate → lifecycle/materialization → PhysicalLink`

`PhysicalLinkCandidate` является промежуточным объяснимым результатом resolver.

Он содержит:
- local/remote endpoint claims;
- protocol-specific port references;
- confidence;
- список evidence;
- ссылки на исходные observations там, где они существуют.

Запрещено:
- создавать PhysicalLink непосредственно из FDB;
- создавать PhysicalLink непосредственно из ARP/FDB correlation;
- использовать IP как внутренний DeviceId;
- считать LLDP localPortNumber равным ifIndex;
- считать CDP cdpCacheIfIndex равным ifIndex;
- считать bridgePortIndex равным ifIndex.

Материализация PhysicalLink и lifecycle выполняются после resolver отдельным слоем.
## Topology lifecycle boundary

После topology resolution lifecycle обрабатывается отдельно от discovery и polling.

Pipeline:

`Observation → evidence → PhysicalLinkCandidate → lifecycle/materialization → PhysicalLink`

Lifecycle различает:
- existence физического объекта;
- freshness последнего подтверждающего evidence.

Freshness:
- Fresh;
- Aging;
- Stale.

Запрещено:
- удалять устройство по timeout;
- удалять link по poll failure;
- удалять topology только потому, что новый poll не вернул evidence;
- считать Stale эквивалентом Deleted;
- изменять manual topology discovery-процессом.

До появления стабильных materialized DeviceId/PhysicalLinkId lifecycle policy остаётся чистой доменной логикой без отдельного persistence по строковому subjectKey.
## Map projection boundary

Визуализация физической топологии отделена от topology resolution.

Pipeline:

`observations → resolver → lifecycle → map projection → MapSnapshot → UI`

`NetLoom.Contracts` содержит transport-neutral map DTO:
- MapSnapshot;
- MapNode;
- MapLink;
- MapEvidenceItem;
- confidence/freshness/evidence enums.

`NetLoom.Topology` выполняет:
- projection PhysicalLinkCandidate в карту;
- объединение duplicate undirected links;
- deterministic layout v1;
- передачу evidence/freshness/confidence.

`NetLoom.Wpf` выполняет только отображение MapSnapshot.

Запрещено:
- выполнять topology resolver в WPF;
- использовать IP как DeviceId;
- считать MapNode.Key внутренним DeviceId;
- записывать topology facts из UI;
- добавлять прямую зависимость WPF на NetLoom.Topology для принятия topology решений.

Map contracts являются частью transport-neutral boundary для будущего NetLoom.Engine/Service IPC.
## Location boundary

`Location` является отдельной доменной сущностью для организационной и визуальной группировки topology.

Структура:
- LocationId — GUID;
- ParentLocationId — nullable GUID;
- Name;
- Description.

Location hierarchy не участвует в identity resolution или PhysicalLink resolution.

Наблюдаемые значения `sysLocation` и CDP `PhysicalLocation` являются evidence/metadata и не должны автоматически превращаться в постоянный Location без явной политики или действия оператора.

Map projection может получить transient Location assignment:

`Location → MapLocation → MapNode.LocationId → WPF`

Запрещено:
- использовать Location как DeviceId;
- использовать IP или MapNode.Key как постоянный foreign key назначения Device→Location;
- изменять PhysicalLink из-за rename/move/delete Location;
- выполнять Location assignment business logic внутри WPF.
## Materialized physical graph (Sprint 15)

Sprint 15 introduces the first persistent common physical topology graph.

The graph contains:

- TopologyDevice with an internal GUID identity;
- DeviceInterface with an internal GUID and optional IF-MIB ifIndex;
- PhysicalLink with an internal GUID and stable link_key;
- persistent Device-to-Location association through devices.location_id.

Manual and discovered topology use the same entities and the same graph. Separate manual_* tables are not used.

Manual topology is represented by explicit attributes:

- device: DiscoveryOrigin = Manual;
- device: MonitoringCapability = None;
- interface: IsManual = true;
- link: PhysicalLinkStrength = Manual;
- observation: ObservationKind.Manual, SourceAddress = User.

Automatic discovery must not replace or delete existing manual topology.

IP addresses, MAC addresses, host names and protocol identifiers are not NetLoom DeviceId.

FDB/ARP correlation alone still cannot create a direct physical cable.

Map presentation keys remain separate from persistent topology identity. Localized labels remain in the client resource layer.

## Sprint 15.1b — целостность PhysicalLink identity

Идентичность materialized PhysicalLink принадлежит Domain и не задаётся вызывающим кодом.

Инварианты:
- `PhysicalLink.Id` — постоянный идентификатор materialized физической связи;
- `LinkKey` вычисляется централизованно из канонически упорядоченных полных endpoint'ов;
- наблюдения `A:p1 ↔ B:p2` и `B:p2 ↔ A:p1` дают один `LinkKey`;
- известный interface endpoint не понижается обратно до `null` при более слабом rediscovery;
- единственный совместимый provisional link может быть уточнён с сохранением `PhysicalLink.Id` и `FirstSeenUtc`;
- при нескольких совместимых provisional link resolver persistence не угадывает соответствие;
- параллельные связи остаются различимыми, когда их различает interface identity;
- automatic topology не может заменить manual PhysicalLink даже при другом входящем GUID.

Persistence объединяет lifecycle timestamps монотонно: `FirstSeenUtc` движется только к более раннему значению, а `LastSeenUtc`, `LastConfirmedUtc` и `LastResolvedUtc` — только к более позднему.

## Sprint 15.1c — current PhysicalLink evidence

Resolver evidence и materialized evidence разделены по слоям.

`TopologyEvidence` остаётся resolver-моделью в `NetLoom.Topology`. Перед persistence она переводится в Domain-модель `PhysicalLinkEvidence`. Поэтому `NetLoom.Persistence.Sqlite` не получает зависимость на `NetLoom.Topology`.

Каждый evidence имеет обязательный `SlotDiscriminator`, описывающий стабильный семантический slot:
- LLDP/CDP — directional local-port identity;
- ARP/FDB correlation — correlation MAC или детерминированный fallback.

SQLite хранит только bounded current evidence snapshot для materialized PhysicalLink. Это не append-only history и не time-series storage.

`MaterializedTopologyMapProjector` читает current evidence и передаёт provenance на MapLink. Manual PhysicalLink продолжает формировать synthetic Manual evidence.

## Sprint 16 — Live Map backend bridge

Рабочая Windows composition root — `NetLoom.Desktop`.

Поток чтения карты:
`SQLite materialized graph → repositories → MaterializedMapSnapshotProvider → MaterializedTopologyMapProjector → MapSnapshot → WPF`.

`IMapSnapshotProvider` находится в Application и является границей между UI и backend projection.

`NetLoom.Wpf` по-прежнему не получает прямых ссылок на Persistence или Topology. Renderer знает только Application/Contracts. `NetLoom.Desktop` является composition root и связывает конкретные SQLite/Topology реализации с WPF.

Текущая реализация работает in-process. Это переходная композиция до service/IPC split: будущий IPC client сможет реализовать тот же `IMapSnapshotProvider`, не меняя renderer.

WPF обновляет snapshot периодически и не пишет topology/configuration SQLite.

Путь БД для Desktop:
1. `--database <path>`;
2. переменная окружения `NETLOOM_DATABASE`;
3. `%LOCALAPPDATA%\NetLoom\netloom.db`.

## Sprint 17 — Simulator raw SNMP replay

`NetLoom.Simulator` воспроизводит сохранённые raw SNMP varbind snapshots без сетевого transport и без дублирования protocol parsing.

Поток:
`JSON snapshot v1 → RawSnmpSnapshotCodec → SnmpObservation/SnmpVariable → production parser → normalized observation`.

Production parsers не копируются и не подменяются. Replay dispatch использует существующие:
- `LldpObservationParser`;
- `CdpObservationParser`;
- `FdbObservationParser`;
- `ArpObservationParser`.

Snapshot v1 хранит:
- observation id;
- ObservationKind;
- source address;
- captured UTC;
- для каждого varbind: OID, type code, display value и raw encoded value в Base64.

Replay infrastructure находится в tool-проекте `NetLoom.Simulator`, а не в production Engine/Persistence слоях.

`SnmpInventory` намеренно не эмулируется: сейчас inventory реализован transport-driven collector-ом и не имеет отдельного production raw parser. Искусственный parser в Simulator не вводится.

Новых runtime/NuGet dependencies нет; используется framework `System.Runtime.Serialization`.

## Sprint 18 — Monitoring Runtime

`NetLoom.Application.Monitoring.MonitoringRuntime` является синхронным one-cycle orchestration boundary. Scheduler в него не встроен.

Один poll cycle:
`MonitoringPollRequest → LLDP/CDP/FDB/ARP production collectors → existing raw + normalized observation stores → MonitoringPollResult`.

Каждый protocol poll является независимым step: ошибка одного collector не прекращает остальные step'ы того же cycle.

`NetLoom.Engine` является первым concrete host:
- `poll-once` выполняет реальный SNMP polling;
- используется `SharpSnmpTransport`;
- используются существующие production parsers;
- raw и normalized protocol observations записываются существующими stores;
- secrets не принимаются в command-line arguments и не печатаются;
- v1/v2 community и v3 secret material читаются только из environment variables.

Monitoring Runtime не создаёт Scheduler, Health metrics или Interface metrics и не пишет high-frequency time series в topology/configuration SQLite.

Linux runtime gate проверяется фактическим запуском опубликованного linux-x64 Engine с командой `runtime-smoke` внутри WSL или Docker. Этот smoke выполняет реальный MonitoringRuntime orchestration path, но намеренно не выполняет внешний SNMP network access.

Self-contained publish включает managed/.NET runtime, но Linux host всё равно должен предоставлять стандартные native runtime dependencies ОС (включая ICU, OpenSSL, libc, libstdc++ и zlib); acceptance проверяет запуск в реальной Linux-среде.

## Sprint 19 — Scheduler

`MonitoringScheduler` находится в Application и повторяет существующий `MonitoringRuntime.PollOnce`.

Scheduler использует fixed-delay semantics:
`poll cycle completion → interval wait → next poll cycle`.

Одновременные cycle для одного scheduler instance не запускаются. Это исключает overlap by construction и не требует блокировок вокруг `MonitoringRuntime`.

Первый cycle запускается немедленно. `NetLoom.Engine schedule` использует `--interval-seconds`, значение по умолчанию — 60 секунд.

Stop/cancellation semantics:
- cancellation проверяется до нового cycle и после завершения cycle;
- ожидание между cycle прерываемо;
- уже начатый synchronous collector/poll не прерывается, потому что текущие collector interfaces не принимают `CancellationToken`;
- `Ctrl+C` в Engine запрашивает graceful stop и не удаляет последнее состояние/observations.

Scheduler не является Health/Interface monitoring и не вводит metric/time-series storage.

## Sprint 20 — metric storage boundary + Health snapshot model

До реализации high-frequency Health/Interface collection вводится отдельная Application abstraction `IMonitoringMetricStore`.

Topology/configuration SQLite не является metric/time-series storage. Sprint 20 не добавляет туда metric tables и не создаёт migrations.

`MonitoringMetricSample` идентифицируется стабильным `DeviceId` и опциональным `InterfaceId`. IP-адрес не используется как metric-series identity.

`HealthSnapshot` допускает отсутствие `DeviceId`, потому что operator/runtime observation может существовать до identity binding. Такой snapshot не проецируется в persistent metric sample до появления стабильного DeviceId.

На этом этапе определены только два Health metric kinds:
- availability;
- uptime seconds.

Concrete time-series backend, retention, aggregation и Interface counters не выбираются в Sprint 20.

Health collector/polling ещё не реализован и остаётся следующим P0 этапом.

## Sprint 21 — SNMP Health monitoring

Health monitoring подключается как отдельный `MonitoringPollKind.Health` к существующему `MonitoringRuntime` и `MonitoringScheduler`.

Production source — lightweight SNMP GET только `sysUpTime.0` (`1.3.6.1.2.1.1.3.0`). Полная Inventory/interface collection для Health poll не запускается.

Успешный SNMP GET означает `HealthStatus.Up`. Значение sysUpTime при возможности преобразуется в `TimeSpan`. Отсутствующий/неразбираемый sysUpTime не превращает успешный poll в failure: uptime остаётся null.

SNMP timeout/socket/protocol/credential failures не преобразуются автоматически в `HealthStatus.Down`: exception остаётся failed monitoring step. Это важно, потому что failed poll не является доказательством отсутствия устройства.

`MonitoringPollRequest.DeviceId` nullable. Engine принимает optional `--device-id <guid>`. IP остаётся только source address и никогда не становится DeviceId.

`poll-once`/`schedule` по умолчанию включают Health. Для независимого Health cadence оператор может запускать `schedule --kinds health`.

Health snapshot возвращается в `MonitoringPollStepResult` и выводится Engine как current result. Sprint 21 не пишет Health time-series history в topology/configuration SQLite и не добавляет concrete `IMonitoringMetricStore`.

## Sprint 22 — IF-MIB current interface status monitoring

Interface monitoring подключается как `MonitoringPollKind.Interface` к существующим `MonitoringRuntime` и `MonitoringScheduler`.

Production collector выполняет только два IF-MIB walk:
- `ifAdminStatus` (`1.3.6.1.2.1.2.2.1.7`);
- `ifOperStatus` (`1.3.6.1.2.1.2.2.1.8`).

Полный `SnmpInventoryCollector` не запускается на interface-monitoring cadence.

`ifIndex` извлекается из OID suffix и остаётся device-local protocol index. Он не является `InterfaceId` и не используется как persistent metric-series identity.

`InterfaceMonitoringSnapshot` является current observation result: nullable stable `DeviceId`, `IfIndex`, admin/oper status и UTC timestamp. IP остаётся source address request'а и не становится identity.

Sprint 22 не создаёт interface counter/rate history и не пишет high-frequency interface data в topology/configuration SQLite. Concrete metric backend остаётся отдельным gate.

Для независимого cadence используется существующий `schedule --kinds interface`.

## Sprint 23a — BRIDGE-MIB STP normalized observation foundation

Sprint 23a вводит production raw + normalized observation boundary для common STP/RSTP tree, но ещё не подключает STP к `MonitoringRuntime`, Engine Scheduler или Simulator CLI.

Источник v1 — BRIDGE-MIB:
- `dot1dBasePortIfIndex` для явного `bridgePortIndex -> ifIndex`;
- bridge-level `dot1dStpProtocolSpecification`, `dot1dStpDesignatedRoot`, `dot1dStpRootCost`, `dot1dStpRootPort`;
- `dot1dStpPortTable`, включая `dot1dStpPortState` и `dot1dStpPortPathCost32`.

Common tree получает явный `InstanceId = cist`. `InstanceId` не опускается даже для единственного common-tree snapshot.

`bridgePortIndex` никогда не подменяет `ifIndex`. При отсутствии или неоднозначности `dot1dBasePortIfIndex` normalized `IfIndex` остаётся `null`.

Collector сначала сохраняет immutable raw `SnmpObservation` с `ObservationKind.Stp`, затем production parser создаёт normalized STP observation и store сохраняет его под тем же `observation_id`.

STP observation не создаёт и не изменяет `Device`, `DeviceInterface` или `PhysicalLink`. STP tree projection остаётся отдельным следующим слоем.

Sprint 23a не реализует MSTP instances, vendor ring protocols или Turbo Ring. Runtime/Scheduler/Simulator integration будет завершено отдельной частью до закрытия backlog `STP/RSTP Collector`.

## Sprint 23b1 — STP MonitoringRuntime + Engine integration

Sprint 23b1 подключает уже существующий production `IStpCollector` к общему `MonitoringRuntime` как `MonitoringPollKind.Stp`.

STP выполняется внутри того же per-step exception boundary, что LLDP/CDP/FDB/ARP/Health/Interface. Ошибка STP не отменяет остальные protocol steps и не удаляет Device, DeviceInterface, PhysicalLink или last-known topology state.

Engine production composition использует существующие `SqliteObservationStore`, `SqliteStpObservationStore`, `StpObservationParser` и `StpCollector`. Отдельной runtime-only STP логики нет.

`poll-once` и `schedule` получают STP через существующий generic kinds parser; default kinds включают STP. Независимый cadence доступен как `schedule --kinds stp`.

Scheduler implementation не меняется.

Runtime smoke включает STP и ищет Health/Interface/STP steps по `MonitoringPollKind`, а не по фиксированной позиции.

Sprint 23b1 не меняет SQLite schema: Migration011 остаётся последней миграцией. STP tree projection, ring detection, MSTP и vendor ring protocols не входят.

Simulator raw STP replay остаётся Sprint 23b2, поэтому backlog `STP/RSTP Collector` ещё открыт.

## Sprint 23b2 — Simulator raw STP replay

Sprint 23b2 завершает STP/RSTP Collector boundary добавлением raw STP snapshot replay в существующий `NetLoom.Simulator`.

Simulator продолжает использовать versioned raw SNMP snapshot format v1 и существующий codec, восстанавливающий тот же `SnmpObservation`/`SnmpVariable`, что используется production pipeline. Для `ObservationKind.Stp` `SnmpSnapshotReplayer` вызывает непосредственно production `StpObservationParser`; simulator-only разбор STP OID запрещён.

Fixture `stp-basic.json` содержит bridge scalars, explicit `dot1dBasePortIfIndex` mapping и STP port rows. Snapshot regression подтверждает `InstanceId = cist`, root `bridgePortIndex = 5 -> ifIndex = 101` и port `BridgePortIndex = 5 -> IfIndex = 101`. Тем самым replay не допускает fallback `bridgePortIndex == ifIndex`.

Missing/ambiguous mapping остаётся покрыт unit regressions production parser из Sprint 23a. Sprint 23b2 не меняет SQLite schema, Scheduler, MonitoringRuntime, materialized topology, STP tree projection, MSTP или vendor ring protocols.

После Sprint 23b2 backlog `STP/RSTP Collector` закрыт. Следующий P0 — `STP tree projection`.

## Sprint 24 — STP tree projection

Sprint 24 вводит отдельную transport-neutral projection model для normalized STP state.

Pipeline:
`StpObservation + explicit stable DeviceId + materialized DeviceInterface[] -> StpTreeProjector -> Contracts.StpTreeSnapshot`.

Projection не использует source IP как DeviceId. Stable `DeviceId` передаётся caller'ом явно.

Resolved `IfIndex` связывается со stable `InterfaceId` только при единственном совпадении `(DeviceId, IfIndex)`. Если совпадения нет, оно принадлежит другому Device или неоднозначно, `InterfaceId` остаётся `null`.

`BridgePortIndex` сохраняется отдельно и никогда не используется как fallback для `IfIndex`.

`StpTreeSnapshot` содержит:
- explicit `InstanceId`;
- protocol root identifier (`DesignatedRoot`) без подмены его NetLoom DeviceId;
- root bridge-port/ifIndex/stable InterfaceId, если привязка однозначна;
- детерминированно упорядоченные STP ports;
- transport-neutral port state: Disabled/Blocking/Listening/Learning/Forwarding/Broken/Unknown;
- stable InterfaceId только при безопасной привязке.

STP projection не читает и не изменяет `PhysicalLink`, не создаёт active-tree cable facts и не выполняет ring detection.

SQLite schema не меняется. Migration011 остаётся последней.

`MapLink` остаётся моделью физической связи и намеренно не перегружается STP semantics. UI integration может потреблять `StpTreeSnapshot` отдельным read-only overlay boundary.

Следующий P0 после Sprint 24 — `Physical ring detection`.

## Sprint 25 — Physical ring detection

Sprint 25 добавляет pure deterministic detector физических L2 cycles поверх уже materialized `PhysicalLink`.

Input:
`IEnumerable<PhysicalLink> -> PhysicalRingDetector -> IReadOnlyList<PhysicalRing>`.

Граф является undirected multigraph на уровне stable `DeviceId`. Каждая canonical materialized physical link является отдельным edge. `InterfaceId` остаётся частью `PhysicalLink.LinkKey`, поэтому разные interface-to-interface cables между той же парой devices являются различимыми parallel edges.

Перед анализом одинаковый `LinkKey` дедуплицируется. Это защищает от reverse/duplicate representation одной физической связи, не схлопывая настоящие parallel links.

Detector строит детерминированный fundamental cycle basis по `PhysicalLink.Id`, а не перечисляет все simple cycles. Это ограничивает размер результата cycle rank графа и не создаёт экспоненциальную выдачу на больших topology snapshots.

Два разных parallel PhysicalLink между двумя devices образуют 2-edge physical cycle. Это только утверждение о physical multigraph и не означает LAG, STP protection, forwarding loop или vendor ring protocol.

`PhysicalRing.RingKey` вычисляется из отсортированного набора stable `PhysicalLink.Id`, поэтому не зависит от порядка входа и сохраняется при endpoint refinement, если сохраняются link IDs.

Eligibility:
- `IsArchived = true` исключает link из current physical graph analysis;
- `IsHidden` не исключает link: hidden является presentation state, а не доказательством отсутствия кабеля;
- Fresh/Aging/Stale не фильтруют link: freshness не эквивалентна deletion;
- manual PhysicalLink участвует наравне с automatic;
- self-device links detector игнорирует как невалидный physical-ring edge.

Detector не использует IP/sourceAddress, FDB/ARP, STP state или vendor ring protocols и не изменяет materialized topology.

SQLite schema не меняется; Migration011 остаётся последней.

Следующий P0 — `Ring protection analyzer`.

## Sprint 26A — SQLite concurrency hardening

NetLoom Desktop и Engine могут одновременно открывать одну локальную SQLite database. Поэтому connection и write-decision policy является общей persistence boundary.

Каждый `SqliteConnectionFactory.OpenConnection()` использует `SQLiteConnectionStringBuilder`:
- `ForeignKeys = true`;
- `JournalMode = WAL`;
- `BusyTimeout = 5000 ms`;
- `SyncMode = Normal`.

WAL является persistent database mode. Database должна находиться на локальной файловой системе; WAL не рассматривается как network-filesystem transport.

Repository read-modify-write методы `SavePhysicalLink`, `SaveDevice` и `SaveInterface` выполняют решение внутри одного `BEGIN IMMEDIATE ... COMMIT/ROLLBACK` scope:
- `ResolvePhysicalLinkTarget` и последующий merge/save атомарны относительно другого writer;
- `ProtectManualDevice` и upsert атомарны;
- `ProtectManualInterface` и upsert атомарны.

Это не меняет PhysicalLink identity/refinement policy и не меняет manual-topology rules — только делает уже существующее решение serializable между процессами.

Migration policy:
1. `schema_migrations` bootstrap остаётся idempotent `CREATE TABLE IF NOT EXISTS`;
2. после bootstrap `ApplyPending` получает immediate write lock;
3. applied versions читаются после получения lock;
4. все pending migrations данного запуска выполняются как одна atomic batch;
5. при ошибке batch откатывается целиком; migrations, применённые до данного запуска, не затрагиваются.

Это сознательное уточнение Sprint 1 semantics. Перечисление migrations остаётся прежним, Migration011 последняя, Migration012 не вводится.

Raw observation retention не входит в Sprint 26A и остаётся Sprint 26B. SourceAddress/IP не становится DeviceId.

Ring protection analyzer остаётся отложенным. Basis-independent forwarding-cycle/bridge/blast-radius анализ можно реализовать независимо от user-facing ring naming semantics.

## Sprint 26B — bounded raw observation retention

Raw protocol observations имеют отдельную эксплуатационную retention-политику и не являются lifecycle-политикой materialized topology.

Правила:
- production default `TopologyLifecyclePolicy` сейчас отсутствует; тестовые 5/15 минут не являются production freshness window;
- raw retention v1 использует фиксированное операционное окно 24 часа и не выводит его из Fresh/Aging/Stale;
- cutoff определяется только по `observations.captured_utc`; `source_address` не участвует в grouping/identity;
- `ObservationKind.Manual` не входит в raw protocol retention;
- за один maintenance-run удаляется не более 8 parent observations;
- каждый parent observation удаляется в отдельном `BEGIN IMMEDIATE`, после чего существующие FK cascade удаляют raw/normalized child rows;
- cleanup выполняется после `poll-once` и после каждого завершённого scheduler cycle;
- ошибка cleanup не превращает успешный protocol poll в failed poll;
- retention не удаляет Device, DeviceInterface, PhysicalLink или manual topology.

`physical_link_evidence_current` остаётся bounded current snapshot и может пережить удаление raw observation. Для explainability введён Application read model `ObservationRawAvailability`:
- `NotApplicable` — evidence не имеет observation id;
- `Available` — referenced observation ещё хранится;
- `Expired` — current evidence сохранён, но referenced raw observation уже удалён retention.

Raw presence проверяется через `LEFT JOIN observations` без загрузки `snmp_varbinds`.

Sprint 26B не меняет `MapEvidenceItem`/WPF. Локализованное отображение raw-expired состояния добавляется отдельно при реализации evidence detail panel.

SQLite schema не меняется; Migration011 остаётся последней. `VACUUM` и ручной WAL checkpoint не выполняются на каждом cleanup.

## Sprint 27 — basis-independent graph safety analysis

Sprint 27 добавляет pure read-only анализ физического L2 multigraph, который не зависит от fundamental cycle basis Sprint 25.

Physical graph eligibility совпадает с Sprint 25:
- source of adjacency — только materialized `PhysicalLink`;
- одинаковый canonical `LinkKey` дедуплицируется;
- разные LinkKey между теми же DeviceId остаются parallel edges;
- `IsArchived = true` исключает link;
- `IsHidden`, Fresh/Aging/Stale и Manual не исключают физический факт;
- self-device links игнорируются.

`AnalyzePhysicalFailures` возвращает результат для каждого eligible PhysicalLink:
- `IsBridge` показывает, является ли link graph bridge / single point of failure;
- для bridge возвращаются две части исходной connected component после удаления link;
- `SeparatedDevicePairCount = |SideA| * |SideB|`;
- для redundant/non-bridge link обе части пусты и separated pair count равен 0.

Blast radius намеренно direction-neutral. Без gateway/root/service context NetLoom не объявляет одну сторону "пострадавшей".

`AnalyzeForwardingCycles` принимает тот же physical graph, transport-neutral `StpTreeSnapshot[]` и explicit `InstanceId`.
Physical adjacency не создаётся из STP.

Endpoint state разрешается только через stable `PhysicalLink.InterfaceId` -> ровно один `StpTreePort.InterfaceId` в ровно одном snapshot данного DeviceId/InstanceId:
- оба endpoint `Forwarding` -> link confirmed forwarding;
- оба endpoint однозначно разрешены и хотя бы один `Blocking`/`Disabled` -> confirmed non-forwarding;
- missing InterfaceId/snapshot/port, duplicate snapshot/port, `Unknown`, `Listening`, `Learning` или `Broken` -> unresolved.

Forwarding warning не перечисляет fundamental cycles. Basis-independent результат — отсортированный набор всех confirmed-forwarding PhysicalLink, которые принадлежат хотя бы одному циклу. В undirected multigraph это forwarding edges, не являющиеся bridges forwarding-subgraph. Поэтому две parallel forwarding edges обе входят в warning set.

`ForwardingCycleAnalysis.IsComplete = false`, если хотя бы один eligible physical link имеет unresolved forwarding state. Отсутствие confirmed cycle при incomplete coverage не является доказательством отсутствия forwarding loop.

`PhysicalRingDetector` Sprint 25 остаётся неизменным и не используется как source of truth для Sprint 27. Его user-facing naming/rename остаётся отдельной задачей перед per-ring protection labels.

SQLite schema, Engine runtime и WPF не меняются.
