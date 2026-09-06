# DECISIONS

## ADR-001

DeviceId является GUID. IP-адрес не является идентификатором устройства.

## ADR-002

Observation отделён от PhysicalLink.

## ADR-003

FDB означает MAC reachability, а не физическое подключение.

## ADR-004

Bridge-port всегда сопоставляется через dot1dBasePortIfIndex.

## ADR-005

Link Strength и Freshness являются независимыми свойствами.

## ADR-006

Временная недоступность не удаляет Device или Link.

## ADR-007

Ручные устройства являются частью общего физического графа.

## ADR-008

Ручные устройства участвуют в поиске колец.

## ADR-009

Мониторинг может быть полностью остановлен оператором.

## ADR-010

Health, Interface Monitoring и Topology Discovery имеют независимое расписание.

## ADR-011

ICMP не является критерием существования Device.

## ADR-012

Credentials задаются только оператором. Credential guessing запрещён.

## ADR-013

WPF является представлением, а не владельцем domain state.

## ADR-014

После выделения Service UI не пишет SQLite напрямую.

## ADR-015

Автоматический Discovery не перемещает зафиксированные пользователем элементы карты.

## ADR-016

Location является отдельной сущностью и не кодируется в имени Device.

## ADR-017

Vendor-specific особенности изолируются в VendorAdapter.

## ADR-018

Turbo Ring не интерпретируется как RSTP.

## ADR-019

STP state хранится как Observation с InstanceId.

## ADR-020

Причинно-следственная связь аварии не объявляется автоматически; Event Correlation формирует только вероятную корреляцию.

## ADR-021 — SQLite runtime и архитектура сборки x64

**Решение:** использовать System.Data.SQLite совместно с native runtime SourceGear.sqlite3. Основная конфигурация сборки NetLoom по умолчанию — x64.

**Причины:**

- System.Data.SQLite 2.x использует отдельную native-библиотеку e_sqlite3;
- SourceGear.sqlite3 не поддерживает AnyCPU для .NET Framework;
- x64 обеспечивает однозначный выбор native SQLite и воспроизводимую сборку;
- при необходимости x86 будет выпускаться как отдельная конфигурация, а не через AnyCPU.

**Следствия:**

- Platform и PlatformTarget задаются централизованно через Directory.Build.props;
- solution по умолчанию собирается как Debug|x64 / Release|x64;
- все проекты, включая тесты, должны проходить обычные dotnet build/test без ручного указания Platform;
- native e_sqlite3.dll должна попадать в выходной каталог потребителя SQLite.

## ADR-022 — Хранение секретов AccessProfile

**Решение:** секреты AccessProfile не хранятся в открытом виде. Перед записью в SQLite они шифруются Windows DPAPI с областью `DataProtectionScope.CurrentUser`.

**Причины:**
- исключить plaintext community/password из базы данных;
- использовать встроенный механизм защиты Windows без собственного криптографического формата;
- привязать возможность расшифровки к учётной записи, под которой работает NetLoom.

**Следствия:**
- таблица `secrets` хранит только защищённый BLOB;
- SNMP community, authentication password и privacy password не должны попадать в логи;
- при будущем переносе NetLoom в Windows Service потребуется явно определить сервисную учётную запись и сценарий миграции секретов;
- замена секрета выполняется только через слой `ISecretProtector`.

## ADR-023 — SNMP transport на SharpSnmpLib

**ешение:** использовать Lextm.SharpSnmpLib 12.5.7 как низкоуровневую SNMP-библиотеку, изолированную внутри NetLoom.Protocols.Snmp.

**ричины:**
- совместимость с .NET Framework 4.8;
- поддержка SNMP v1, v2c и v3;
- поддержка USM authentication/privacy;
- Application и Domain не зависят от типов SharpSnmpLib.

**Следствия:**
- остальной NetLoom работает через ISnmpTransport;
- SNMP timeout считается транспортной ошибкой, а не признаком отсутствия устройства;
- retry выполняется транспортным слоем;
- SNMPv3 использует discovery engine parameters и повторную синхронизацию при notInTimeWindow;
- community/password не должны попадать в сообщения ошибок и журналы.
## ADR-024 — Inventory Collector отделен от Persistence

**Решение:** сбор SNMP-инвентаризации выполняется через `IInventoryCollector` и возвращает нейтральный `InventorySnapshot`. Коллектор не записывает данные напрямую в SQLite.

**Причины:**
- наблюдение устройства и сохраненное текущее состояние имеют разный жизненный цикл;
- ошибка или частичный ответ SNMP не должны автоматически удалять ранее известные данные;
- Application не должен зависеть от SharpSnmpLib или SQLite;
- один и тот же collector должен использоваться discovery, polling и simulator.

**Следствия:**
- `NetLoom.Protocols.Snmp` преобразует SNMP varbinds в нейтральную модель Application;
- management IP является адресом опроса, а не идентификатором Device;
- недоступный `ifXTable` считается частичным inventory, а не отсутствием устройства;
- сохранение и reconciliation inventory будут выполняться отдельным слоем.

## ADR-025 — Discovery возвращает кандидатов, а не создаёт Device напрямую

**Решение:** слой Discovery формирует кандидатов и факты доступности, но не создаёт и не удаляет сущности Device.

**Причины:**
- IP-адрес не является DeviceId;
- один и тот же Device может иметь несколько management endpoints;
- отсутствие ICMP/SNMP-ответа не доказывает отсутствие устройства;
- идентификация Device должна основываться на нескольких признаках и выполняться отдельным reconciliation-слоем;
- Discovery должен оставаться повторяемым и не иметь скрытых побочных эффектов в БД.

**Следствия:**
- ICMP, TCP и SNMP являются независимыми сигналами;
- исключённые цели не опрашиваются;
- AccessProfile определяет разрешённые оператором SNMP credentials и область применения;
- DiscoveryCandidate содержит адрес наблюдения, найденные TCP-порты и InventorySnapshot, если SNMP ответил;
- создание/обновление Device будет выполняться последующим слоем на основании observations и identity claims.

## ADR-026 — Observation отделён от фактов инвентаря и топологии

**ешение:** сырые результаты опроса сохраняются как неизменяемые Observation. Observation фиксирует тип, адрес источника, UTC-время и исходные данные протокола. аличие Observation само по себе не создаёт Device, Interface или PhysicalLink.

**ричины:**
- результат опроса является свидетельством, а не подтверждённым фактом топологии;
- один IP не должен автоматически становиться идентификатором Device;
- повторная обработка raw SNMP данных должна быть возможна без повторного сетевого опроса;
- LLDP/CDP/FDB/ARP/STP должны сохранять происхождение и время наблюдения;
- Simulator сможет подавать сохранённые raw varbinds тем же парсерам, что используются при реальном опросе.

**Следствия:**
- observations хранит метаданные наблюдения;
- snmp_varbinds хранит исходные varbinds с порядком, type code, display value и encoded value;
- reconciliation и topology resolver работают отдельными слоями;
- retention применяется к Observation и связанным raw данным, но не означает автоматическое удаление Device или PhysicalLink;
- неудачный будущий poll не удаляет предыдущие наблюдения или факты топологии.
## ADR-027 — LLDP является наблюдением, а не физической связью

**Решение:** данные LLDP сохраняются как raw SNMP Observation и как нормализованные LLDP-наблюдения. На этапе LLDP не создаётся PhysicalLink.

**Причины:**
- LLDP сообщает сведения о соседе, но окончательная физическая связь должна формироваться отдельным topology resolver;
- локальная нумерация LLDP-портов не обязана совпадать с ifIndex;
- удалённая таблица LLDP имеет составной индекс из timeMark, localPortNum и remoteIndex;
- raw данные должны оставаться доступными для повторного разбора и Simulator;
- неполные или ошибочные строки LLDP не должны превращаться в вымышленные факты топологии.

**Следствия:**
- lldpRemLocalPortNum хранится как LLDP local port number и не преобразуется в ifIndex без отдельного подтверждённого mapping;
- один локальный LLDP-порт может содержать несколько remote entries;
- observation_id связывает raw varbinds и нормализованный LLDP snapshot;
- PhysicalLink будет создаваться позднее topology resolver на основании LLDP/CDP и других evidence;
- удаление observation каскадно удаляет его нормализованные LLDP rows, но не означает автоматическое удаление существующего PhysicalLink.

## ADR-028 — Portable core + legacy Service + modern Engine

**Решение:** основной backend остаётся на C#/.NET. `Domain`, `Application`, `Contracts`, `Topology` используют `netstandard2.0`; `Protocols.Snmp` и `Persistence.Sqlite` — `net48;net8.0`; `Service`/`Wpf` сохраняют `net48`; `NetLoom.Engine` является modern Windows/Linux host.

На Sprint 7.5 Engine использует `net8.0` из-за текущего SDK 8.0.424. Целевой production modern runtime — .NET 10 LTS после контролируемого обновления toolchain.

**Следствия:** portable core не использует Windows-only API; DPAPI остаётся Windows implementation `ISecretProtector`; Linux secret protection добавляется отдельно; legacy Service и modern Engine используют одинаковые Application/Domain contracts.

## ADR-029 — Transport-neutral IPC

**Решение:** UI ↔ backend определяется контрактами, а не Named Pipes. Named Pipes допустим как локальный Windows transport. Сетевой transport для Linux/remote deployment выбирается отдельным ADR.

**Следствия:** `NetLoom.Contracts` не зависит от WPF/Named Pipes/transport library; после service split UI не пишет SQLite напрямую; выбор gRPC/HTTP не фиксируется заранее. Go не является backend Engine и может рассматриваться только для будущего отдельного `NetLoom.Probe`.

## ADR-030 — CDP является evidence, а cdpCacheIfIndex сохраняется без преждевременного связывания

**ешение:** CDP хранится как raw SNMP Observation и как нормализованное CDP-наблюдение. аличие CDP-записи само по себе не создаёт PhysicalLink.

**ричины:**
- cdpCacheEntry индексируется парой cdpCacheIfIndex + cdpCacheDeviceIndex;
- cdpCacheIfIndex сохраняется как исходное наблюдаемое значение;
- DeviceId, sysObjectID, management address и имя соседа являются identity claims/evidence, а не внутренним DeviceId NetLoom;
- связывание локального и удалённого интерфейсов выполняется отдельным topology resolver на основе совокупности evidence.

**Следствия:**
- raw varbinds сохраняются до нормализации;
- raw и normalized CDP используют один observation_id;
- malformed CDP rows не создают ложные сущности;
- CDP и LLDP остаются независимыми источниками evidence;
- topology resolver сможет сопоставлять CDP с inventory и другими наблюдениями позднее.
## ADR-031 — FDB и bridge-port mapping сохраняются как отдельные evidence

**Решение:** FDB-запись не является физическим линком. `dot1dTpFdbPort` сохраняется как bridgePortIndex и разрешается в ifIndex только через явное значение `dot1dBasePortIfIndex`.

**Следствия:**
- запрещено считать bridgePortIndex равным ifIndex;
- при отсутствии однозначного mapping возвращается unresolved;
- значение FDB port 0 не разрешается в интерфейс;
- MAC из FDB означает только «MAC наблюдался за данным bridge-port»;
- PhysicalLink может появиться только на более позднем этапе topology resolution при достаточном наборе evidence.
## ADR-032 — ARP/ND + FDB correlation является evidence, а не PhysicalLink

**ешение:** IP/MAC correlation объединяет наблюдения ARP/Neighbor Discovery и FDB, но сама по себе не создаёт физический линк.

**равила:**
- modern source — ipNetToPhysicalTable;
- legacy IPv4 fallback — ipNetToMediaTable;
- fallback разрешён только при SNMP Protocol failure;
- timeout, socket и authentication/credentials failures не скрываются;
- ARP/ND ifIndex сохраняется как IF-MIB ifIndex;
- FDB interface разрешается только через явный bridgePortIndex → dot1dBasePortIfIndex → ifIndex;
- при неоднозначном bridge mapping FdbIfIndex остаётся unresolved;
- invalid, local и incomplete neighbor entries не используются как neighbor evidence;
- совпадение IP ↔ MAC ↔ FDB port увеличивает объём evidence, но не доказывает прямой физический кабель.

**Следствие:** решение о PhysicalLink принимает только Topology Resolver на следующем этапе.
## ADR-033 — PhysicalLinkCandidate отделён от PhysicalLink

**ешение:** Topology Resolver сначала формирует объяснимые `PhysicalLinkCandidate`, а не записывает наблюдения напрямую как физические связи.

**равила:**
- LLDP/CDP adjacency является strong topology evidence;
- ARP/FDB correlation является weak supporting evidence;
- weak evidence самостоятельно не создаёт link candidate;
- endpoint identity хранится как claim и не становится внутренним DeviceId без отдельного identity resolution;
- IP address не используется как DeviceId;
- LLDP localPortNumber не считается ifIndex;
- CDP cdpCacheIfIndex не считается ifIndex;
- FDB bridgePortIndex разрешается только через dot1dBasePortIfIndex;
- confidence и evidence strength являются разными характеристиками;
- каждый candidate сохраняет объяснимый набор evidence.

**Следствие:** материализация, объединение, lifecycle и удаление/устаревание физических связей выполняются отдельным слоем после topology resolution.
## ADR-034 — stale является состоянием freshness, а не удалением

**ешение:** lifecycle физической топологии разделяет freshness и existence.

Состояния freshness:
- Fresh;
- Aging;
- Stale.

**равила:**
- timeout/poll failure не удаляет Device или PhysicalLink;
- отсутствие нового evidence не удаляет Device или PhysicalLink;
- Stale не эквивалентен Deleted;
- новое валидное evidence может вернуть объект в Fresh;
- FirstSeenUtc не изменяется при повторных наблюдениях;
- LastSeenUtc никогда не перемещается назад;
- manual topology не удаляется и не стареет из-за discovery;
- lifecycle вычисляется через явно переданный nowUtc для детерминированности.

**Persistence:** отдельная lifecycle-таблица на этом этапе не создаётся. Сохранение FirstSeenUtc/LastSeenUtc/Freshness будет добавлено вместе с materialized topology, когда появятся стабильные DeviceId/PhysicalLinkId. роизвольный subjectKey не должен становиться постоянным идентификатором .

**Следствие:** факт отсутствия ответа является состоянием наблюдения, а не доказательством отсутствия физического объекта.
## ADR-035 — карта является transport-neutral projection

**ешение:** WPF не строит физическую топологию самостоятельно. Backend/topology layer формирует transport-neutral `MapSnapshot`, который UI только отображает.

Разделение:

`Topology Resolver / Lifecycle → TopologyMapProjector → Contracts.MapSnapshot → WPF`

**равила:**
- `MapNode.Key` является presentation key и не является внутренним DeviceId;
- IP-адрес не становится DeviceId;
- topology resolution не выполняется в WPF;
- confidence, freshness и evidence передаются в map contracts;
- layout v1 является deterministic;
- одинаковая недиректированная связь не должна отображаться дважды из-за обратного protocol observation;
- map contracts остаются пригодными для будущего service/engine IPC;
- WPF не получает прямую зависимость от NetLoom.Topology.

**Следствие:** будущая замена WPF renderer, переход на service IPC или Linux-hosted Engine не требуют переноса topology business logic в UI.
## ADR-036 — Location является отдельной сущностью и не частью Device identity

**ешение:** физическое расположение моделируется отдельной сущностью `Location` с собственным GUID.

Иерархия строится через `ParentLocationId`.

**равила:**
- LocationId не является DeviceId;
- Location name не является DeviceId;
- sysLocation и CDP PhysicalLocation остаются observations и автоматически не создают постоянную Location assignment;
- rename/move Location не меняют identity устройства или link;
- Location не влияет на Topology Resolver;
- удаление Location не является удалением Device или PhysicalLink;
- циклическая иерархия запрещена;
- Location с дочерними Location нельзя удалить;
- MapNode может содержать опциональный LocationId только как presentation metadata.

До появления materialized Device с внутренним GUID постоянная таблица Device→Location не создаётся. Запрещено сохранять такое назначение по IP или MapNode.Key.
## ADR-037 — Common materialized physical graph

**Status:** Accepted.

### Context

Before Sprint 15 NetLoom had observations, evidence, topology candidates, lifecycle policy and map projection, but no persistent common physical graph with stable internal identities.

Manual unmanaged equipment must participate in the same physical topology as discovered devices.

### Decision

Use one materialized physical graph for both discovered and manually entered topology.

Internal identities for devices, interfaces and physical links are GUID-based.

Do not create separate manual_* tables.

Manual topology is represented through explicit attributes:

- DeviceDiscoveryOrigin.Manual;
- MonitoringCapability.None;
- DeviceInterface.IsManual = true;
- PhysicalLinkStrength.Manual.

Automatic discovery must not silently replace or delete existing manual topology.

Persistent Device-to-Location assignment now uses stable internal DeviceId.

Manual user actions use the existing observation model:

- ObservationKind.Manual;
- SourceAddress = User.

Map contracts expose neutral enums for origin, monitoring capability and category. Localized presentation remains in WPF resources.

### Consequences

- manual and discovered devices participate in the same physical graph;
- future ring detection operates across both manual and automatic topology;
- IP address remains separate from DeviceId;
- FDB/ARP correlation alone still does not create a direct physical cable;
- topology lifecycle can now be associated with stable materialized identifiers;
- WPF remains independent from SQLite persistence.

## ADR-038 — Canonical PhysicalLink identity

Статус: принято.

`PhysicalLink.Id` является постоянной идентичностью materialized физического соединения.

`LinkKey` не принимается от caller и вычисляется Domain из канонически упорядоченных endpoint'ов `(DeviceId, InterfaceId?)`. Поэтому обратное направление наблюдения не создаёт другую физическую связь.

Reconciliation:
1. точное совпадение canonical endpoint'ов обновляет существующий PhysicalLink;
2. один совместимый provisional PhysicalLink может быть уточнён с сохранением `Id` и `FirstSeenUtc`;
3. несколько совместимых provisional PhysicalLink считаются неоднозначностью — выбор не угадывается;
4. более грубое rediscovery не стирает уже известный interface endpoint;
5. manual PhysicalLink защищается по разрешённой физической идентичности, а не только по входящему GUID.

`Migration009MaterializedTopology` остаётся неизменной.

## ADR-039 — Bounded current PhysicalLink evidence

Статус: принято.

Materialized PhysicalLink хранит отдельный bounded current evidence snapshot.

Resolver `TopologyEvidence` не передаётся напрямую в persistence, потому что это создало бы недопустимую зависимость Persistence → Topology. Для границы materialization используется Domain-модель `PhysicalLinkEvidence`.

Evidence slot идентифицируется сочетанием:
`physical_link_id + evidence_kind + source_address + slot_discriminator`.

`SlotDiscriminator` обязателен и формируется там, где известна семантика evidence. SourceAddress остаётся частью ключа, поэтому evidence разных направлений не схлопывается.

Current evidence имеет replace-snapshot / last-write-wins semantics и не является историческим или time-series хранилищем.

## ADR-040 — Desktop composition root и IMapSnapshotProvider

Статус: принято.

Для подключения реального materialized graph к WPF вводится Application boundary `IMapSnapshotProvider`.

Конкретный provider находится в Topology и читает только через существующие Application repository interfaces.

Windows executable `NetLoom.Desktop` является composition root:
- инициализирует SQLite schema;
- создаёт topology/location repositories;
- создаёт `MaterializedMapSnapshotProvider`;
- передаёт provider в WPF `MainWindow`.

WPF не получает ссылок на `NetLoom.Persistence.Sqlite` или `NetLoom.Topology`.

Эта схема намеренно допускает будущую замену in-process provider на IPC proxy после service split без изменения WPF renderer.

`NetLoom.Engine` и `NetLoom.Service` в этом решении не объявляются monitoring runtime: их polling/runtime orchestration остаётся отдельным backlog.

## ADR-041 — Raw SNMP snapshot replay через production parsers

Статус: принято.

Simulator использует версионированный raw snapshot format v1 и восстанавливает настоящий `SnmpObservation`/`SnmpVariable`.

Encoded varbind value сохраняется отдельно в Base64 и не восстанавливается из display text. Это необходимо для parser-ов, использующих raw BER payload.

После восстановления raw observation Simulator вызывает те же LLDP/CDP/FDB/ARP parser classes, которые использует production collection pipeline.

Отдельная simulator-only интерпретация OID не допускается.

Inventory replay откладывается до появления production raw inventory parser boundary; обход через fake transport не считается эквивалентом raw parser replay.

## ADR-042 — Monitoring Runtime one-cycle boundary

Статус: принято.

Monitoring Runtime отделяется от Scheduler.

`MonitoringRuntime.PollOnce` оркестрирует один набор выбранных protocol collectors и возвращает результат по каждому step. Ошибка одного protocol collector не отменяет другие collectors.

Runtime переиспользует существующие production LLDP/CDP/FDB/ARP collectors, parsers и observation stores. Отдельная runtime-only protocol logic не создаётся.

Первый concrete host — modern `NetLoom.Engine`. Legacy `NetLoom.Service` может позднее собрать тот же Application runtime boundary.

Persisted access profiles пока не используются Engine runtime автоматически: существующий Linux target не имеет DPAPI implementation, а scope repository пока не является полным read model. До отдельного cross-platform secret/config decision Engine получает операторские SNMP secrets только через environment variables и никогда не выводит их.

Monitoring Runtime не является Scheduler и не вводит monitoring metric storage.

## ADR-043 — Scheduler fixed-delay без overlap

Статус: принято.

Scheduler остаётся отдельным Application boundary над `MonitoringRuntime.PollOnce`.

Используется fixed-delay cadence: следующий cycle начинается только после завершения предыдущего cycle и последующего interval wait. Fixed-rate параллельный запуск не используется, поэтому slow poll не создаёт overlap или очередь конкурентных SNMP cycle.

Первый cycle выполняется немедленно.

Cancellation останавливает ожидание и предотвращает следующий cycle. Уже выполняющийся synchronous poll завершается штатно; interrupt in-flight collector откладывается до отдельного изменения collector contracts с `CancellationToken`.

Первый concrete host — `NetLoom.Engine schedule`. `Ctrl+C` преобразуется в graceful cancellation.

Scheduler не пишет Health/Interface metrics и не изменяет topology/configuration SQLite schema. Metric/time-series storage gates остаются открытыми.

## ADR-044 — отдельная metric/time-series storage boundary

Статус: принято.

High-frequency monitoring metrics не записываются в topology/configuration SQLite.

Application вводит `IMonitoringMetricStore` как отдельный boundary для будущего metric/time-series backend. Concrete backend не фиксируется заранее.

Metric-series identity использует стабильный `DeviceId` и, для interface metrics, опциональный `InterfaceId`. IP-адрес не является DeviceId и не используется как постоянный series key.

`HealthSnapshot` может быть unbound (`DeviceId = null`) для operator/probe результата, но `HealthMetricProjector` не создаёт persistent metric sample до identity binding.

Sprint 20 вводит модель Health availability/uptime и storage abstraction, но не объявляет Health collector или high-frequency persistence реализованными.

Отдельное решение по concrete metric/time-series backend, retention и aggregation остаётся обязательным до массовой записи Health/Interface history.

## ADR-045 — Health poll использует отдельный sysUpTime GET

Статус: принято.

Health polling не переиспользует полный `SnmpInventoryCollector`, потому что это ненужно запускало бы interface walks на Health cadence.

Production `SnmpHealthCollector` выполняет отдельный SNMP GET `sysUpTime.0`.

Успешный GET формирует `HealthSnapshot(Status=Up)`. Transport/protocol/credential exception остаётся failed poll step и не объявляется `Down` или отсутствием Device.

Stable identity передаётся отдельно как nullable `DeviceId`; source IP не используется как DeviceId. Unbound Health result допустим для live operator output.

Health интегрируется в существующие `MonitoringRuntime`/`MonitoringScheduler`; отдельный scheduler implementation не создаётся.

Sprint 21 не выбирает concrete metric backend и не пишет high-frequency Health history в topology/configuration SQLite. Открытый gate по concrete metric backend/retention сохраняется.

## ADR-046 — Interface monitoring использует lightweight IF-MIB status walks

Статус: принято.

Interface monitoring не переиспользует полный `SnmpInventoryCollector`: production collector выполняет только `ifAdminStatus` и `ifOperStatus` walks.

`ifIndex` является device-local SNMP index. Он не становится глобальным или persistent `InterfaceId`.

Current result может быть unbound по stable interface identity; до появления explicit binding к materialized `InterfaceId` он не проецируется в persistent interface metric series.

Polling failure остаётся failed step и не удаляет Device/Interface и не переписывает materialized topology state.

Используется существующий fixed-delay Scheduler; отдельный interface scheduler не создаётся.

Sprint 22 не выбирает concrete metric backend и не пишет high-frequency interface history в topology/configuration SQLite.

## ADR-047 — STP v1 хранится как raw + normalized BRIDGE-MIB Observation

Статус: принято.

STP/RSTP common-tree evidence сохраняется по существующему observation pattern: raw `SnmpObservation` с `ObservationKind.Stp` сохраняется раньше normalized STP rows и имеет тот же `observation_id`.

Common tree получает явный `InstanceId = cist`. Это не означает поддержку MSTP instances: она не заявляется Sprint 23a.

`bridgePortIndex` не является `ifIndex`. Единственный разрешённый mapping: `bridgePortIndex -> dot1dBasePortIfIndex -> ifIndex`. При отсутствующем или неоднозначном mapping `IfIndex` остаётся unresolved.

BRIDGE-MIB port state и root data являются evidence и не мутируют `DeviceInterface`, `PhysicalLink` или физическую топологию.

Turbo Ring и другие vendor ring protocols не интерпретируются как RSTP.

Sprint 23a добавляет Migration011 только для normalized STP observations. Runtime/Scheduler/Simulator integration остаётся обязательной следующей частью перед закрытием `STP/RSTP Collector`.

## ADR-048 — STP polling переиспользует MonitoringRuntime и Scheduler

Статус: принято.

STP добавляется как `MonitoringPollKind.Stp` в существующий `MonitoringRuntime`.

Production Engine использует тот же `StpCollector`, `StpObservationParser`, raw store и normalized STP store, которые определены observation pipeline. Runtime-only parser/collector не создаётся.

STP step изолирован общим per-step exception boundary. Failed STP poll не является topology deletion и не удаляет last-known Device/Interface/PhysicalLink.

Используется существующий fixed-delay `MonitoringScheduler`; отдельный STP scheduler запрещён.

Default Engine kinds включают STP; independent cadence использует `schedule --kinds stp`.

Migration count остаётся 11. Simulator raw STP replay остаётся обязательным Sprint 23b2 перед закрытием `STP/RSTP Collector`.

## ADR-049 — Simulator STP replay использует только production parser

Статус: принято.

`NetLoom.Simulator` восстанавливает raw `SnmpObservation` через существующий versioned snapshot codec и для `ObservationKind.Stp` вызывает production `StpObservationParser`.

STP fixture не содержит отдельной simulator-only семантики OID. Mapping `bridgePortIndex -> dot1dBasePortIfIndex -> ifIndex` выполняется production parser. Common-tree replay сохраняет explicit `InstanceId = cist`; это не объявляет поддержку MSTP instances.

Missing/ambiguous mapping остаётся unresolved по production parser rules и защищён Sprint 23a unit regressions. Новых migrations нет; Migration011 остаётся последней.

После runtime integration Sprint 23b1 и raw Simulator replay Sprint 23b2 backlog `STP/RSTP Collector` закрывается. Следующий слой — `STP tree projection`.

## ADR-050 — STP tree является отдельной projection, а не PhysicalLink state

Статус: принято.

STP/RSTP state проецируется в отдельный transport-neutral `StpTreeSnapshot`.

`MapLink` и `PhysicalLink` продолжают означать физическую связь. STP port state не превращается в физическую adjacency и не перезаписывает link truth.

Projection получает stable `DeviceId` явно от caller. `Observation.SourceAddress` не используется как DeviceId.

Stable `InterfaceId` разрешается только по единственному совпадению `(DeviceId, IfIndex)`. При missing/ambiguous/cross-device match binding остаётся unresolved.

`BridgePortIndex` сохраняется как protocol index и не подменяет `IfIndex`.

`DesignatedRoot` остаётся protocol bridge identifier и не объявляется NetLoom DeviceId без отдельного identity-resolution evidence.

Projection детерминирована и не имеет write-path в topology persistence.

Migration count остаётся 11. Physical ring detection и Ring protection analyzer остаются отдельными следующими слоями.

## ADR-051 — Physical ring detection использует deterministic cycle basis materialized multigraph

Статус: принято.

Physical ring detector анализирует только materialized `PhysicalLink` facts как undirected multigraph по stable DeviceId.

Canonical `PhysicalLink.LinkKey` определяет физическую edge identity с endpoint'ами `(DeviceId, InterfaceId?)`. Одинаковый LinkKey дедуплицируется; разные LinkKey между теми же DeviceId остаются parallel physical edges.

Две разные parallel physical edges являются 2-edge physical cycle. Это не является утверждением о LAG, STP, forwarding loop или vendor protection protocol. Протокольная интерпретация выполняется отдельным `Ring protection analyzer`.

Чтобы избежать экспоненциального перечисления всех simple cycles, detector возвращает deterministic fundamental cycle basis. Порядок обработки задаётся stable `PhysicalLink.Id`. `PhysicalRing.RingKey` строится из отсортированных stable link IDs.

`IsArchived` исключает link из current ring analysis. `IsHidden` не исключает physical fact. Fresh/Aging/Stale не определяют existence и не фильтруют detector. Manual links участвуют наравне с automatic.

Self-device physical links не становятся ring result.

Detector не использует IP/sourceAddress, FDB/ARP или STP state и не имеет write-path в topology persistence.

Migration count остаётся 11. Следующий слой — `Ring protection analyzer`.
