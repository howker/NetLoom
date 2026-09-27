# Sprint 46 — операторский UI/UX redesign

Статус: planned  
Предпосылка: Sprint 45 backend field acceptance — GREEN.

## Почему нужен отдельный redesign sprint

Полевая эксплуатация показала, что backend уже способен обнаруживать, опрашивать, сохранять и восстанавливать физическую L2-топологию, но интерфейс скрывает важные зависимости и не показывает ход длительных операций.

Главная проблема Sprint 46 — не «сделать красивее», а сделать состояние системы очевидным оператору без знания внутренней архитектуры NetLoom.

## Принципы

1. **Одна операция — одно видимое состояние.** Пользователь всегда видит, что сейчас делает приложение.
2. **Никаких скрытых межвкладочных зависимостей.** Monitoring не должен зависеть от невидимого выбора во вкладке Discovery.
3. **Цвет только дополняет смысл.** Статус обязан иметь текст/иконку, а цвет — усиливать его.
4. **Текущий контекст всегда рядом с действием.** Профиль, устройство, диапазон, прогресс и ошибки видны там, где запускается операция.
5. **Длительные операции показывают прогресс.** Нужны `N/total`, текущее устройство/IP, success/error/remaining и время последнего события.
6. **Карта не должна требовать ручного разгадывания layout.** Карточки и линии не должны перекрывать друг друга настолько, чтобы скрывать topology.
7. **Backend semantics не переизобретаются в UI.** UI отображает реальные состояния и evidence, а не генерирует новую topology-логику.

## Зафиксированный полевой backlog

### Глобальный SNMP-профиль

Текущая проблема:

- профиль выбирается во вкладке «Обнаружение сети»;
- Monitoring использует тот же профиль;
- пользователь не понимает эту зависимость;
- при отсутствии выбора получает техническую ошибку процесса.

Требование:

- активный SNMP-профиль должен быть частью общего operator context;
- он видим в shell/toolbar или явно в каждой операции, которая его использует;
- Discovery и Monitoring используют одну выбранную identity профиля;
- последний рабочий профиль может восстанавливаться после restart;
- если профиль отсутствует, действие блокируется понятным сообщением до запуска Engine;
- secret никогда не показывается в UI и логах.

### Monitoring

Текущие проблемы:

- непонятно, идёт ли опрос;
- непонятно, закончился ли первый цикл;
- нет текущего устройства;
- нет `N/55`;
- нет success/error/remaining;
- «Опросить сейчас» означает разное при running/stopped state.

Требования:

- явные состояния `Остановлен / Запускается / Выполняется цикл / Останавливается / Ошибка`;
- progress: `текущее / всего`;
- текущий device name + management IP;
- counters success/error/remaining;
- start time + last completed cycle;
- компактный event feed:
  - poll started;
  - SNMP success/failure;
  - interfaces;
  - LLDP/CDP;
  - topology materialization;
  - poll completed;
- contextual actions:
  - когда monitoring stopped: «Опросить выбранное устройство»;
  - когда running: «Запустить цикл сейчас»;
  - отдельные «Запустить мониторинг» / «Остановить мониторинг».

### Discovery

Текущие проблемы:

- найденные устройства не поднимаются вверх;
- текущий адрес визуально почти незаметен;
- состояния выглядят статично;
- ввод диапазона менее привычен оператору.

Требования:

- диапазон задаётся понятными полями `Начальный IP` / `Конечный IP`;
- поддерживается ввод/вывод маски/подсети там, где это требуется операторскому workflow;
- current address выделяется и имеет лёгкую анимацию только во время scan;
- freshly discovered device перемещается наверх или в отдельную секцию «Найдено сейчас»;
- state transitions обновляются без необходимости угадывать, произошло ли изменение;
- итог scan показывает scanned/found/errors/duration.

### Цветовые состояния и левая полоска карточки

Полевой дефект:

- цветная полоска слева у карточек визуально воспринимается как status indicator;
- при изменении состояния её цвет не меняется.

Требование:

- полоска привязана к реальному current status;
- один набор semantic status tokens используется на карте, в discovery и monitoring;
- изменение состояния обновляет цвет динамически;
- состояние одновременно обозначается текстом/иконкой;
- не полагаться только на красный/зелёный.

### Карта и карточки

Полевые проблемы:

- карточки перекрываются;
- линии проходят через карточки/подписи;
- при реальных 55 устройствах readability быстро падает;
- после restart оператору иногда требуется дополнительное действие для удобного обзора.

Требования:

- layout должен учитывать реальные размеры карточек;
- автоматическое размещение не создаёт overlap для типового field snapshot;
- link routing минимизирует проход через карточки;
- manual position остаётся authoritative там, где оператор уже расположил объекты;
- `Показать всё` остаётся доступным, но нормальный restart не должен требовать его как обязательный recovery step;
- topology status/strength/evidence отображаются без перегрузки основного вида.

## Предлагаемая структура operator shell

### Верхний persistent context

Всегда видим:

- active SNMP profile;
- monitoring state;
- current operation;
- compact health/error indicator.

### Основные рабочие области

1. **Карта** — физическая topology и выбранный объект.
2. **Мониторинг** — lifecycle опроса и диагностика.
3. **Обнаружение** — диапазон, progress и найденные устройства.
4. **События/предупреждения** — operator-facing изменения и проблемы.

Не дублировать один и тот же selector/indicator в нескольких местах, если он является глобальным состоянием.

## Implementation slices

### Sprint 46A — shared operator context

Цель: устранить скрытую зависимость SNMP profile и ввести единый operation state.

- shared active-profile selection;
- persistence последнего выбранного профиля;
- validation before Engine launch;
- shared operation-state model;
- shell indicator для active profile + current operation;
- без редизайна topology layout.

Acceptance:

- Monitoring можно запустить, не заходя предварительно в Discovery;
- при отсутствии профиля Engine не стартует и показывается понятное сообщение;
- restart восстанавливает выбранный profile id;
- secret не появляется в state/log/UI.

### Sprint 46B — monitoring progress

- lifecycle state;
- `N/total`;
- current device/IP;
- success/error/remaining;
- cycle timestamps;
- event feed;
- contextual button labels.

Acceptance:

оператор по одному экрану может ответить:
- идёт ли мониторинг;
- какое устройство сейчас опрашивается;
- сколько уже завершено;
- сколько осталось;
- были ли ошибки;
- закончился ли первый цикл.

### Sprint 46C — discovery workflow

- start/end IP + mask/subnet UX;
- prominent current IP;
- newest-found-first;
- animated state transition;
- summary counts and duration.

### Sprint 46D — map status and layout

- dynamic card status strip;
- shared semantic status tokens;
- card-size-aware placement;
- overlap prevention;
- link routing cleanup;
- preserve manual layout.

### Sprint 46E — consistency and acceptance

- accessibility/state-not-by-color-only;
- keyboard/focus sanity;
- restart persistence;
- 55-device field snapshot acceptance;
- PNG/CSV export regression;
- final Server 2012 R2 field check.

## Non-goals

Sprint 46 не должен:

- менять protocol identity semantics;
- выводить physical links из FDB/ARP эвристикой;
- скрывать unknown/one-way topology evidence;
- встраивать SNMP secrets в UI state;
- переписывать backend, который уже прошёл Sprint 45 field acceptance, если этого не требует конкретный UI contract.

## Field acceptance criteria Sprint 46

На реальной базе/площадке оператор без знания внутренних процессов должен:

1. открыть NetLoom после restart;
2. сразу увидеть active profile и monitoring state;
3. запустить monitoring без перехода во вкладку Discovery;
4. видеть progress полного цикла по 55 устройствам;
5. понимать результат выбранного device poll;
6. запустить discovery по понятному диапазону IP;
7. видеть текущий IP и новые найденные устройства;
8. видеть изменение состояния карточки не только текстом, но и динамической полосой;
9. читать карту без массовых overlap;
10. экспортировать PNG/CSV без regression.

Sprint 46 считается завершённым только после повторной полевой проверки на реальном Server 2012 R2.
