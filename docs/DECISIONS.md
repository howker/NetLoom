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
