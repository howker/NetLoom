# NetLoom Third-Party Dependencies

Реестр внешних runtime-зависимостей, которые требуют проверки перед будущим коммерческим распространением.

| Dependency | Version | License / status | Usage |
| --- | --- | --- | --- |
| Lextm.SharpSnmpLib | 12.5.7 | MIT/X11 | SNMP transport |
| System.Data.SQLite | 2.0.4 | Public Domain | SQLite ADO.NET provider |
| SourceGear.sqlite3 | 3.53.4 | SQLite Public Domain | Native SQLite builds |

## Policy

Перед добавлением новой внешней зависимости необходимо:

1. Зафиксировать package/version.
2. Проверить лицензию из первичного или package metadata источника.
3. Добавить запись в этот файл.
4. Отдельно оценить copyleft и redistribution requirements.
5. GPL/AGPL не добавлять в ядро без отдельного решения.
6. Не считать отсутствие явного запрета разрешением на коммерческое распространение.
