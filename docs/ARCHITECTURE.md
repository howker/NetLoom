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
