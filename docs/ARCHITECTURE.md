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
