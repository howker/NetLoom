# PROJECT_STATE

## Текущая версия

0.0

## Текущий этап

Спринт 0 завершён — окружение и каркас проекта подготовлены.

Следующий этап: Спринт 1 — SQLite и система миграций.

## Работает

- Git-репозиторий и удалённый origin настроены.
- Зафиксирован .NET SDK 8.0.424 через global.json.
- Установлены и проверены Build Tools и .NET Framework 4.8 targeting pack.
- Создан NetLoom.sln.
- Созданы 10 основных проектов:
  - NetLoom.Domain;
  - NetLoom.Application;
  - NetLoom.Contracts;
  - NetLoom.Protocols.Snmp;
  - NetLoom.Topology;
  - NetLoom.Persistence.Sqlite;
  - NetLoom.Service;
  - NetLoom.Wpf;
  - NetLoom.Simulator;
  - NetLoom.DbTool.
- Созданы 3 тестовых проекта:
  - NetLoom.Tests.Unit;
  - NetLoom.Tests.Integration;
  - NetLoom.Tests.Snapshots.
- Все проекты нацелены на .NET Framework 4.8.
- Основные зависимости между проектами настроены.
- WPF-каркас успешно собирается.
- Добавлена политика переносов строк через .gitattributes.
- Все три smoke-теста проходят успешно.

## Проверка

Последняя контрольная проверка:

- dotnet build NetLoom.sln — успешно;
- dotnet test NetLoom.sln — успешно;
- предупреждений сборки: 0;
- ошибок сборки: 0;
- тестов пройдено: 3;
- тестов не пройдено: 0;
- git diff --check — без ошибок.

## Известные ограничения

- функциональная доменная модель ещё не реализована;
- SQLite и миграции ещё не реализованы;
- SNMP ещё не реализован;
- discovery ещё не реализован;
- карта ещё не реализована;
- мониторинг ещё не реализован.

## Следующий шаг

Спринт 1: добавить SQLite, schema_migrations и минимальный механизм версионируемых миграций.
