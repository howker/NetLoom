# PROJECT_STATE

## Текущая версия

0.2-dev

## Текущее состояние

Sprint 0, Sprint 1 и Sprint 2 завершены.

Следующий этап: Sprint 3 — SNMP transport.

## Основа проекта

- решение из 13 проектов;
- целевая платформа .NET Framework 4.8;
- архитектура сборки x64;
- WPF-каркас;
- базовая тестовая инфраструктура MSTest;
- SQLite через System.Data.SQLite;
- native SQLite через SourceGear.sqlite3;
- миграции схемы с таблицей schema_migrations;
- включён PRAGMA foreign_keys = ON.

## Sprint 1 — SQLite и миграции

Реализованы:
- SqliteConnectionFactory;
- DatabaseInitializer;
- MigrationRunner;
- schema_migrations;
- app_settings;
- транзакционное применение миграций;
- защита от повторной версии миграции;
- rollback ошибочной миграции;
- повторная инициализация базы без повторного применения миграций.

## Sprint 2 — AccessProfile и секреты

Реализованы:
- модель AccessProfile;
- SNMP версии v1/v2c/v3;
- ISecretProtector;
- DPAPI-защита секретов с DataProtectionScope.CurrentUser;
- таблицы access_profiles, secrets, access_profile_targets, access_profile_exclusions, access_profile_tcp_ports;
- AccessProfileRepository;
- SecretRepository;
- AccessProfileScopeRepository;
- каскадное удаление связанных данных;
- проверка диапазона TCP-портов;
- integration-тесты, подтверждающие отсутствие plaintext-секрета в SQLite.

## Известные ограничения

- SNMP transport ещё не реализован;
- discovery ещё не реализован;
- сбор инвентаря ещё не реализован;
- построение физической топологии ещё не реализовано;
- карта и мониторинг ещё не реализованы;
- текущая сборка x64;
- DPAPI CurrentUser требует отдельного решения для сервисной учётной записи при переходе к Windows Service.

## Следующий шаг

Sprint 3: SNMP transport.
