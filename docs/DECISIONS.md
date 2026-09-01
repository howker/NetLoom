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
