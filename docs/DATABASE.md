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
