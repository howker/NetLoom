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
