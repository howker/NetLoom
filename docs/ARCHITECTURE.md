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
