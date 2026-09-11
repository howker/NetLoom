# NetLoom Backlog

Этот файл дополняет Sprint-план из PROJECT_STATE.md и не заменяет его.

Приоритет определяется сначала собственной эксплуатацией NetLoom, затем коммерческой подготовкой.

## P0 — нужен для собственного рабочего NetLoom

- [x] Sprint 15 — materialized graph + manual/unmanaged topology.
- [x] Sprint 15.1a - text integrity hardening.
- [x] Sprint 15.1b - canonical PhysicalLink identity + monotonic lifecycle timestamps.
- [x] Sprint 15.1c - bounded current PhysicalLink evidence + map explainability.
- [x] Подключить реальный backend pipeline к MapSnapshot и WPF, чтобы карта показывала живую сеть.
- [x] NetLoom.Simulator: replay сохранённых raw SNMP varbind snapshots через настоящие parsers.
- [x] Monitoring Runtime.
- [x] Post-Sprint 31 hardening — live polling materializes explicit stable DeviceId and IF-MIB interface identity into materialized topology without inventing PhysicalLink.
- [x] Scheduler.
- [x] Sprint 20 — metric/time-series storage boundary + Health snapshot model.
- [x] Sprint 21 — SNMP Health monitoring via sysUpTime.
- [x] Health monitoring.
- [x] Sprint 22 — IF-MIB current interface status monitoring.
- [x] Interface monitoring.
- [x] Sprint 23a — BRIDGE-MIB STP normalized observation foundation.
- [x] Sprint 23b1 — STP MonitoringRuntime + Engine integration.
- [x] Sprint 23b2 — Simulator raw STP replay through production parser.
- [x] STP/RSTP Collector.
- [x] Sprint 24 — STP tree projection foundation.
- [x] STP tree projection.
- [x] Sprint 25 — deterministic physical ring detection.
- [x] Physical ring detection.
- [x] Sprint 26A — SQLite WAL + concurrency hardening.
- [x] Sprint 26B — bounded raw observation retention + evidence expiry semantics.
- [x] Sprint 27 — basis-independent graph safety analysis.
- [x] Basis-independent graph safety analysis: forwarding-cycle detection, bridges and blast radius.
- [x] Sprint 28 — user-facing ring semantics + cycle-basis primitive clarification.
- [x] User-facing ring semantics before per-ring protection labels.
- [x] Sprint 29 — Ring protection analyzer.
- [x] Ring protection analyzer.
- [x] Удобный поиск MAC/IP до конкретного switch/interface.
  - [x] Sprint 30A — stable observation→DeviceId binding + backend MAC/IP evidence lookup.
  - [x] Sprint 30B — localized WPF MAC/IP search UX + navigation/highlight to switch/interface candidates.
- [x] Минимальные alerts для реально полезных topology/ring failures.
  - [x] Sprint 31A — pure current-state topology/ring alert semantics + deterministic AlertKey.
  - [x] Sprint 31B — operator-facing read-only alert surface + current-state transition/repeat suppression.

## P0 — архитектурные gates

- [x] До массовой записи метрик в monitoring ввести отдельную abstraction metric/time-series storage.
- [ ] Не складывать высокочастотные временные ряды в topology/configuration SQLite без отдельного решения.
- [x] Когда NetLoom.Engine начнёт выполнять реальные polling/runtime задачи, добавить настоящий Linux runtime smoke test.
- [x] Simulator должен уметь воспроизводить raw protocol snapshots без обхода production parsers.

## P1 — полезно после появления рабочей карты

- [ ] Topology snapshots / машина времени.
- [ ] Diff карты до/после инцидента.
- [ ] SFP/DDM optical health.
- [ ] SNMP trap ingestion.
- [ ] Syslog ingestion.
- [ ] Экспорт схемы объекта в PNG/PDF.
- [ ] Инвентаризация и экспорт CSV.
- [ ] Выявление вероятного unmanaged switch по нескольким MAC за портом.

## Product readiness — дёшево сейчас

- [x] Transport-neutral contracts.
- [x] Отдельный modern Engine.
- [x] UI business logic отделена от topology resolver.
- [x] Encoding policy через .editorconfig.
- [x] Восстановлены повреждённые кодировкой-комментарии в ARP/FDB/correlation/resolver и добавлен UTF-8 audit.
- [x] Расширить text-integrity audit на содержимое WPF `.resx` `<value>`, чтобы ловить повреждённые/съеденные начальные символы операторских строк.
- [x] UI localization infrastructure.
- [x] Реестр third-party лицензий.
- [x] Friction log.
- [ ] Проверка лицензии каждой новой зависимости до merge.
- [ ] Проверка репозитория на реальные credentials / production identifiers.

## Потом, когда собственный NetLoom реально используется

- [ ] Английская локаль UI.
- [ ] Перевод документации и сайта.
- [ ] Web UI.
- [ ] Multi-user / roles.
- [ ] REST API для внешних клиентов.
- [ ] Installer / auto-update.
- [ ] Licensing / editions.
- [ ] Billing / merchant of record.
- [ ] Коммерческие тарифы.
- [ ] Немецкая и другие локали по реальному спросу.

## Правило приоритета

Повторяющаяся реальная проблема из FRICTION_LOG имеет приоритет над speculative product feature.
## Operational hardening backlog

- [ ] Surface raw-expired evidence state in localized evidence UI when that detail panel is implemented.
- [ ] Configurable SNMP WALK varbind limit with explicit step failure before unbounded memory growth.
- [ ] Propagate CancellationToken into the active poll/collector path; cancellation must not become a failed protocol step.
- [ ] Multi-device scheduler: bounded parallelism, per-device cadence and startup jitter.
- [x] Clarify/rename fundamental cycle-basis primitive before exposing user-facing named rings.
## P0 - Desktop operational reliability before the next product feature

- [ ] Sprint 32A - coherent non-blocking Desktop refresh with last-known-good semantics.
  - [ ] Capture one `MaterializedTopologyReadSet` per refresh on one SQLite connection and one read transaction.
  - [ ] The read transaction contains only persistence reads and closes before projection, analysis, transition tracking, or UI work.
  - [ ] `MaterializedTopologyReadSet` contains Devices, Interfaces, PhysicalLinks, PhysicalLinkEvidence, Locations, and LatestStp.
  - [ ] Map and topology alerts are computed from the same captured read-set.
  - [ ] At most one periodic refresh is in flight; timer ticks are skipped while it is running.
  - [ ] SQLite reads and pure projection/analysis never block the WPF Dispatcher thread.
  - [ ] `TopologyAlertTransitionTracker.Observe()` remains Dispatcher-owned and runs only for a successfully completed whole refresh result.
  - [ ] Failed, cancelled, or invalidated refresh work does not mutate transition state.
  - [ ] After the first successful refresh, a later failure keeps the last-known-good map and alerts visible.
  - [ ] The UI shows stale/error state and the time of the last successful refresh; a later successful refresh clears stale state.
  - [ ] A failure before the first successful refresh is distinct from a healthy empty-topology state.
  - [ ] Window close stops scheduling, cancels or invalidates refresh/lookup work in flight, and prevents post-close UI apply.
  - [ ] Normal cancellation during window close is not reported as a refresh failure.
  - [ ] `SqliteStpObservationStore.GetLatest()` no longer performs N+1 connections/queries.
  - [ ] A concurrent writer commit between read phases cannot produce a mixed topology snapshot or a false alert transition.
  - [ ] The consistency integration test uses a deterministic synchronization seam and is demonstrated RED on the old multi-connection path before the fix.
  - [ ] Lookup search runs off the Dispatcher thread.
  - [ ] Lookup is single-flight: an older request cannot overwrite a newer result.
  - [ ] Window close cancels or invalidates lookup work in flight and prevents post-close lookup apply.
  - [ ] Lookup under SQLite contention does not freeze the Dispatcher.
  - [ ] Manual contention acceptance confirms that the Desktop remains responsive while a SQLite writer holds a lock.

- [ ] Sprint 32B - persistent host logging for Desktop and Engine.
  - [ ] Use one logging mechanism for both hosts.
  - [ ] Persist Desktop refresh/search failures instead of relying on debugger-only `Trace` output.
  - [ ] Persist Engine polling/retention/materialization failures instead of relying on console-only output.
  - [ ] Default Windows logs to `%ProgramData%\NetLoom\logs`.
  - [ ] Define portable Linux log-path semantics for the `net8.0` Engine host.
  - [ ] Make log level and rotation configurable.

- [ ] Architecture gate before Sprint 32C - decide the final owner of localization resources and record it in `DECISIONS.md`.
- [ ] Sprint 32C - localization foundation: English neutral resources, Russian satellite resources, explicit culture selection, pluralization, removal of the Topology text leak, `.cs` localization-integrity guard, and template-resource cleanup.
- [ ] After 32A-32C, run a realistic 3-5 device SNMP/snmpsim stand acceptance and add only real operational observations to `FRICTION_LOG.md`.
- [ ] Choose the next product feature only after stand acceptance and `FRICTION_LOG.md` review; do not start topology snapshots / time-machine work automatically.
