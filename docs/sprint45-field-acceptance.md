# Sprint 45 — полевая приёмка и закрытие backend

Дата закрытия: 2026-09-27  
Базовый коммит после protocol-identity fix: `6252ccddb28b48bba4bc3fdd42d5136776266a69`.

## Цель

Проверить NetLoom на реальной производственной сети и на фактической целевой ОС без изменения production-окружения ради приложения.

Целевая площадка:

- Windows Server 2012 R2 Standard x64;
- PowerShell 4.0;
- app-local UCRT/VC runtime;
- self-contained .NET 8 Desktop/Engine;
- реальное SNMP-оборудование нескольких производителей;
- медленный RDP-канал, поэтому полевые обновления должны оставаться малыми.

## Фактически подтверждено

На реальной площадке подтверждены:

- запуск Desktop и Engine на Windows Server 2012 R2 с app-local prerequisites;
- создание и безопасное хранение SNMP-профиля;
- SNMP v1/v2c discovery и monitoring path через выбранный профиль;
- обнаружение 55 устройств;
- материализация 275 интерфейсов;
- LLDP topology materialization;
- 10 физических связей;
- после protocol-identity repair и повторной валидации:
  - 9 связей `Confirmed`;
  - 1 связь `Observed`;
  - 19 current link-evidence records;
- persistence после полного restart приложения без повторного опроса;
- PNG + CSV export;
- сохранение DeviceId, layout, links и evidence после offline repair полевой БД.

## Backend-дефекты, найденные только в поле

### Monitoring profile propagation

Discovery передавал выбранный SNMP-профиль в Engine, Monitoring — нет. Исправлено без требования профиля для legacy/manual monitoring.

### CDP topology materialization

CDP observation доходил до storage, но не передавался в topology materializer. Добавлена optional capability без расширения legacy materializer contract.

### LLDP interface reconciliation

LLDP-связи использовали synthetic interfaces с `ifIndex = NULL`, хотя соответствующие IF-MIB interfaces уже существовали.

Исправлено:

- числовой LLDP local port сопоставляется с уникальным существующим `ifIndex`;
- automatic link references переносятся на canonical interface;
- link IDs и evidence сохраняются;
- obsolete synthetic interfaces удаляются, когда это доказуемо безопасно;
- local port reconciliation выполняется до remote-neighbor resolution.

Полевой результат: synthetic LLDP interfaces уменьшились с 16 до 1.

Оставшийся synthetic interface намеренно сохранён:

- устройство: `VOS-2 EDS408`, `10.48.228.84`;
- LLDP port: `5`;
- это remote side односторонней связи;
- нет достаточного reciprocal local-port evidence для безопасного назначения `ifIndex`.

### Binary protocol identity

SNMP transport сохранял:

- `DisplayValue = Data.ToString()`;
- `encoded_value = Data.ToBytes()`.

Бинарные LLDP/STP identifiers ошибочно использовали lossy `DisplayValue` как identity. Октеты `>= 0x80` превращались в `?`, что создавало реальные identity collisions.

Полевой пример LLDP:

- `00:90:E8:38:CA:95`;
- `00:90:E8:38:CA:DB`;

до исправления нормализовались одинаково.

Полевой пример STP:

- raw BER: `040810006C3B6BE8D3A4`;
- canonical bridge ID: `1000.6C3B6BE8D3A4`.

Исправлено:

- общий BER payload decoder;
- LLDP chassis/port IDs декодируются по subtype без потери байтов;
- STP bridge IDs и designated port декодируются из BER payload;
- CDP binary network-address fields обрабатываются отдельно от DisplayString fields;
- ARP использует тот же общий BER helper.

## Offline repair полевой базы

Исторические повреждённые normalized identity были восстановлены только из `snmp_varbinds.encoded_value`.

Не использовались эвристики восстановления из уже повреждённых строк.

Исправлены исторические строки:

- `devices.lldp_chassis_id`: 7;
- `lldp_local_system.chassis_id`: 112;
- `lldp_observations.chassis_id`: 411;
- `lldp_observations.port_id`: 162;
- `stp_observations.designated_root`: 125;
- `stp_port_states.designated_root`: 1560;
- `stp_port_states.designated_bridge`: 1560;
- `stp_port_states.designated_port`: 1560.

После repair:

- `PRAGMA integrity_check = ok`;
- `PRAGMA foreign_key_check` — 0 нарушений;
- 55 устройств;
- 275 интерфейсов;
- 10 физических связей;
- topology persisted after restart.

Оригинальная offline backup полевой БД должна храниться как контрольная точка и не использоваться для дальнейших экспериментов.

## Итог Sprint 45

Backend Sprint 45 считается полевым GREEN.

Новые backend-патчи не должны добавляться в Sprint 45 без нового подтверждённого полевого дефекта. Следующий этап — операторский UI/UX redesign, основанный на замечаниях реальной эксплуатации.
