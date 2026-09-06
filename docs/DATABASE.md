# DATABASE

База данных NetLoom использует SQLite.

## Основные принципы

- GUID хранится как TEXT.
- Время хранится в UTC в формате ISO-8601 как TEXT.
- Boolean хранится как INTEGER 0/1.
- Enum хранится текстом.
- Секреты хранятся через DPAPI.
- Схема будет версионироваться через таблицу schema_migrations.
- Устройства и связи не удаляются автоматически.
- Observation отделён от PhysicalLink.
- FDB не считается физической связью.

## Планируемые таблицы

Первые миграции будут создавать таблицы:

- schema_migrations;
- app_settings;
- locations;
- devices;
- device_identities;
- management_endpoints;
- interfaces.

## Ключевое разделение

Данные и выводы должны чётко разделяться на:

- текущее состояние;
- историю изменений;
- system configuration;
- security;
- user layout;
- operator actions.

Схема данных будет реализована дальше, на следующих спринтах.

## AccessProfile и секреты

Sprint 2 добавляет:
- `access_profiles` — параметры профиля доступа без секретов;
- `secrets` — DPAPI-защищённые значения;
- `access_profile_targets` — области применения профиля;
- `access_profile_exclusions` — исключения;
- `access_profile_tcp_ports` — разрешённые TCP-порты.

Секреты хранятся как BLOB после DPAPI-защиты. Plaintext community/password в SQLite не допускается. Удаление AccessProfile каскадно удаляет связанные секреты, targets, exclusions и TCP-порты.

## cdp_observations

ормализованные CDP-наблюдения.

люч:

`observation_id + cache_if_index + device_index`

Таблица хранит CDP address/device/port/platform/capabilities, native VLAN, duplex, sysName, sysObjectID, management address, physical location и lastChange.

`observation_id` ссылается на `observations` с `ON DELETE CASCADE`.

`cache_if_index` сохраняется как исходное наблюдаемое значение. н не является `InterfaceId` и не должен использоваться как внутренний идентификатор интерфейса без отдельного сопоставления.
## bridge_port_mappings

Нормализованное отображение BRIDGE-MIB:

`observation_id + bridge_port_index + if_index`

Таблица хранит явное соответствие bridge-port и ifIndex из `dot1dBasePortIfIndex`.

Несколько ifIndex для одного bridgePortIndex сохраняются как неоднозначные evidence и не разрешаются автоматически.

## fdb_observations

Нормализованные записи forwarding database.

Ключ:

`observation_id + mac_address`

Поля:
- `mac_address`;
- `bridge_port_index`;
- `status`.

`bridge_port_index` не является ifIndex. Для перехода к интерфейсу требуется запись из `bridge_port_mappings`.

FDB-запись не является PhysicalLink.
## arp_observations

ормализованные ARP/Neighbor Discovery observations.

люч:

`observation_id + if_index + address_type + ip_address + table_kind`

оля:
- `if_index`;
- `address_type`;
- `ip_address`;
- `physical_address`;
- `entry_type`;
- `entry_state`;
- `table_kind`.

`table_kind` различает modern `ipNetToPhysicalTable` и legacy `ipNetToMediaTable`.

`if_index` является IF-MIB ifIndex устройства, на котором получено neighbor observation.

MAC/IP запись является observation evidence и не является DeviceId или PhysicalLink.
## locations

ерархический справочник физических расположений.

оля:
- `location_id` — GUID;
- `parent_location_id` — nullable GUID;
- `name`;
- `description`;
- `created_utc`;
- `updated_utc`.

`parent_location_id` ссылается на `locations.location_id`.

граничения:
- Location не может быть своим parent;
- repository запрещает циклы;
- repository запрещает удаление Location с дочерними Location;
- rename/move сохраняют `location_id`.

а Sprint 14 таблица назначения Device→Location намеренно отсутствует, поскольку стабильная materialized Device entity ещё не введена. IP и MapNode.Key не используются как постоянные идентификаторы такого назначения.
## Migration 009 — Materialized physical topology

Migration 009 adds the persistent common physical graph:

- devices;
- interfaces;
- physical_links.

Internal identifiers are GUID values serialized as text.

devices.location_id references the actual Sprint 14 primary key:

locations(location_id)

Manual and discovered topology use the same tables.

Manual topology is stored as:

- devices.discovery_origin = Manual;
- devices.monitoring_capability = None;
- interfaces.is_manual = 1;
- physical_links.strength = Manual.

Automatic persistence must not replace an existing manual device, interface, or physical link.

A physical-link interface endpoint must belong to the device specified for that endpoint.

A connected manual device or interface cannot be deleted while a physical link references it.

High-frequency monitoring metrics are not stored in these topology tables.
