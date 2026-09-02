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

азделение:

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

ерархия строится через `ParentLocationId`.

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

о появления materialized Device с внутренним GUID постоянная таблица Device→Location не создаётся. апрещено сохранять такое назначение по IP или MapNode.Key.