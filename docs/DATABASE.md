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
