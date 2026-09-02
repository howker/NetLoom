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