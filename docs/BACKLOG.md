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
- [x] Dual-runtime test foundation: portable/core regression lane on `net8.0` alongside legacy WPF-specific `net48` tests.
- [x] Реестр third-party лицензий.
- [x] Friction log.
- [ ] Проверка лицензии каждой новой зависимости до merge.
- [ ] Проверка репозитория на реальные credentials / production identifiers.

## Потом, когда собственный NetLoom реально используется

- [x] English neutral/fallback Desktop resources (Sprint 32C).
- [ ] Перевод документации и сайта.
- [ ] Web UI — roadmap only after HTTP API/security boundary and real deployment need.
- [ ] Multi-user / roles.
- [ ] HTTP/REST API — roadmap; implement before Web/remote client access.
- [ ] Installer / auto-update.
- [ ] Licensing / editions.
- [ ] Billing / merchant of record.
- [ ] Коммерческие тарифы.
- [ ] Немецкая и другие локали по реальному спросу.

## Правило приоритета

Повторяющаяся реальная проблема из FRICTION_LOG имеет приоритет над speculative product feature.
## Operational hardening backlog

- [x] Surface raw-expired evidence state in localized evidence UI when that detail panel is implemented — completed in Sprint 35 selected-element diagnostics.
- [ ] Configurable SNMP WALK varbind limit with explicit step failure before unbounded memory growth.
- [ ] Propagate CancellationToken into the active poll/collector path; cancellation must not become a failed protocol step.
- [ ] Multi-device scheduler: bounded parallelism, per-device cadence and startup jitter.
- [x] Clarify/rename fundamental cycle-basis primitive before exposing user-facing named rings.
## P0 - Desktop operational reliability before the next product feature

- [x] Sprint 32A - coherent non-blocking Desktop refresh with last-known-good semantics.
  - [x] Capture one `MaterializedTopologyReadSet` per refresh on one SQLite connection and one read transaction.
  - [x] The read transaction contains only persistence reads and closes before projection, analysis, transition tracking, or UI work.
  - [x] `MaterializedTopologyReadSet` contains Devices, Interfaces, PhysicalLinks, PhysicalLinkEvidence, Locations, and LatestStp.
  - [x] Map and topology alerts are computed from the same captured read-set.
  - [x] At most one periodic refresh is in flight; timer ticks are skipped while it is running.
  - [x] SQLite reads and pure projection/analysis never block the WPF Dispatcher thread.
  - [x] `TopologyAlertTransitionTracker.Observe()` remains Dispatcher-owned and runs only for a successfully completed whole refresh result.
  - [x] Failed, cancelled, or invalidated refresh work does not mutate transition state.
  - [x] After the first successful refresh, a later failure keeps the last-known-good map and alerts visible.
  - [x] The UI shows stale/error state and the time of the last successful refresh; a later successful refresh clears stale state.
  - [x] A failure before the first successful refresh is distinct from a healthy empty-topology state.
  - [x] Window close stops scheduling, cancels or invalidates refresh/lookup work in flight, and prevents post-close UI apply.
  - [x] Normal cancellation during window close is not reported as a refresh failure.
  - [x] `SqliteStpObservationStore.GetLatest()` no longer performs N+1 connections/queries.
  - [x] A concurrent writer commit between read phases cannot produce a mixed topology snapshot or a false alert transition.
  - [x] The consistency integration test uses a deterministic synchronization seam and is demonstrated RED on the old multi-connection path before the fix.
  - [x] Lookup search runs off the Dispatcher thread.
  - [x] Lookup is single-flight: an older request cannot overwrite a newer result.
  - [x] Window close cancels or invalidates lookup work in flight and prevents post-close lookup apply.
  - [x] Lookup under SQLite contention does not freeze the Dispatcher.
  - [x] Manual contention acceptance confirms that the Desktop remains responsive while a SQLite writer holds a lock.

- [x] Sprint 32B - persistent host logging for Desktop and Engine.
  - [x] Use one logging mechanism for both hosts.
  - [x] Persist Desktop refresh/search failures instead of relying on debugger-only `Trace` output.
  - [x] Persist Engine polling/retention/materialization failures instead of relying on console-only output.
  - [x] Default Windows logs to `%ProgramData%\NetLoom\logs`.
  - [x] Define portable Linux log-path semantics for the `net8.0` Engine host.
  - [x] Make log level and rotation configurable.

- [x] Architecture gate before Sprint 32C - localization ownership fixed by ADR-063: WPF owns Desktop UI resources; Desktop owns startup culture selection; backend contracts remain localization-neutral.
- [x] Sprint 32C - localization foundation: English neutral resources, Russian satellite resources, explicit culture selection, pluralization, removal of the Topology text leak, `.cs` localization-integrity guard, and template-resource cleanup.
- [x] Sprint 32C post-closure correction — declared English neutral resources with `NeutralResourcesLanguage("en")` and added regression evidence. Structured XML inspection confirms `Name1`, `Icon1`, and `Bitmap1` are not actual `<data>` resource entries; their text appears only in the standard ResX schema comment and is not a cleanup defect.
- [x] Master-plan documentation alignment — product boundary, platform/runtime split, canonical Engine host, multi-client presentation boundary, client-owned localization, friction-driven prioritization and long-term roadmap are recorded in the canonical documentation.
- [x] Run a realistic 3-5 device SNMP/snmpsim stand acceptance. Use the WPF client as a real working session and add only observed operational friction to `FRICTION_LOG.md`.
- [x] Review `FRICTION_LOG.md` after stand acceptance and choose exactly one next product Sprint. Do not start topology snapshots / time-machine work automatically.

## Execution map after Sprint 36

The realistic stand gate, Sprint 33A, Sprint 33B, the post-Sprint friction review, Sprint 34A through Sprint 34K, the UI foundation preparation task, Sprint 35, and Sprint 36 are complete. The current WPF client now combines coherent selected-element diagnostics with an operator-arrangeable persistent physical map: unlocked nodes can be dragged, locked nodes are visibly marked, viewport/layout state survives restart, zoom spans 1%-500%, the operator can recover a lost topology with Show all, and the map uses a very large virtual workspace for large sites. Calm semantic motion is available from Map settings and is intentionally visible only when meaningful state actually changes. Product priority now advances to Sprint 37 — manual topology from the UI.

### Completed

- [x] Sprint 33A — incremental WPF map reconciliation keyed by stable `DeviceId` / `PhysicalLinkId`; retained WPF visual identity and node Canvas position across refresh, with existing lookup highlight/viewport behavior preserved. Sprint 33A did not add new zoom/pin UX that the current client does not yet expose.
- [x] Sprint 33B — topology link-label readability and collision-safe placement. WPF now measures retained link labels and chooses a node-card-safe placement around the link instead of relying on a fixed midpoint offset; topology identity and semantics remain unchanged.
- [x] Sprint 34A — interface counter delta foundation. Collect `ifInErrors`, `ifOutErrors`, `ifInDiscards`, `ifOutDiscards`, and `ifCounterDiscontinuityTime`; carry the raw nullable values in `InterfaceMonitoringSnapshot`; compute portable interval deltas with explicit `NoBaseline`, `Valid`, and `Discontinuity` results; treat Counter32 decrease as wrap only while the discontinuity marker is stable; never invent a delta when the baseline or discontinuity marker is unavailable. The audited Sprint scope did not add `ifLastChange`, persistence, degradation thresholds, incidents, or outbound delivery.
- [x] Sprint 34B — durable interface-counter baseline and restart-safe interval evaluation. Persist the latest raw counter snapshot by stable `DeviceId` + `ifIndex`, atomically return/replace the previous sample, reject non-newer samples, restore the baseline after Engine restart, and run the existing 34A evaluator against the restored raw sample. A discontinuity interval remains non-degrading and its current sample becomes the next durable baseline.
- [x] Sprint 34C — current-state interface degradation classification. Classify restart-safe counter evaluations as `Indeterminate`, `Healthy`, or `Degraded`; normalize enabled error/discard counter sums to rates per minute; require explicit finite positive thresholds; treat threshold equality as degraded; preserve incomplete-evidence semantics; and guarantee that `NoBaseline` and `Discontinuity` never become degradation. Engine classification is opt-in through `--interface-error-rate-per-minute` and/or `--interface-discard-rate-per-minute`. Interface oper/admin state and `ifLastChange` are not mixed into this counter-degradation classifier.
- [x] Sprint 34D — durable interface-degradation transition state and repeat suppression. Persist only determinate `Healthy` / `Degraded` state by stable `DeviceId` + `ifIndex`; classify `FirstAppearance`, `Unchanged`, `Changed`, `Resolved`, and `Indeterminate`; keep `Indeterminate` from replacing durable state; compare degraded evidence by canonical reason fingerprint rather than volatile rates; and suppress unchanged degradation across Engine restart.
- [x] Sprint 34E — durable interface-degradation event outbox. Extract pure transition evaluation behind a processor boundary; atomically advance determinate degradation state and enqueue immutable delivery-ready events for `FirstAppearance`, `Changed`, and `Resolved`; keep `Unchanged` and `Indeterminate` out of the outbox; use deterministic event keys for idempotency; and prove rollback of both state and event when outbox insertion fails.
- [x] Sprint 34F — first outbound interface-degradation delivery path. Use SMTP relay as the first real adapter; keep credentials in environment variables; mark an outbox event delivered only after adapter success; preserve pending events across delivery failure/restart; use at-least-once semantics when adapter success is followed by acknowledgement failure; and leave the monitoring loop alive on delivery failure.
- [x] Sprint 34G — durable delivery retry scheduling and backoff. Persist delivery failure count, last failure time, and next eligible attempt time; calculate retry delay in a pure Application policy with deterministic exponential backoff `1m -> 2m -> 4m -> 8m -> 16m -> 32m -> 60m` and a 60-minute cap; suppress retries before eligibility across restart; preserve all pending events; and keep successful acknowledgement semantics unchanged. `Migration018InterfaceDegradationDeliveryRetry` remains the current schema migration.
- [x] Sprint 34H — operator-facing delivery state and SMTP acceptance. Add read-only `delivery-status` output that classifies durable events as `Ready`, `Deferred`, or `Delivered` and surfaces failure/retry/delivery UTC evidence without reading SMTP secrets. Add `smtp-acceptance`, which sends a synthetic canary through the same SMTP adapter and environment-variable configuration without writing a synthetic outbox event. Prove adapter success against a controlled local SMTP relay, including MIME/base64 decoding of the UTF-8 body, and prove a deterministic secret-free failure diagnostic when SMTP configuration is absent. No schema migration is required.
- [x] Sprint 34I environment audit — read-only deployment acceptance attempt. Confirm `HEAD == origin/main` and a clean worktree; confirm target SMTP configuration is absent; scan seven discovered local databases and classify all seven as pre-outbox schema; leave the repository unchanged. This does not count as real target-relay acceptance and does not provide evidence for dead-letter/escalation policy.
- [x] Sprint 34J — stable legacy-schema diagnostics for `delivery-status`. Add a dedicated SQLite read-only connection path; preflight the complete delivery-status outbox schema before querying it; return stable `ERROR: DELIVERY_STATUS_SCHEMA_UNSUPPORTED` with exit code 7 for pre-outbox or incomplete schema; keep the selected database byte-identical; and preserve normal zero-event reads on a current schema. No migration is added.
- [x] Sprint 34K — actionable SMTP configuration readiness diagnostics. Add secret-free `smtp-readiness` evaluation for the existing `NETLOOM_SMTP_*` environment contract; report required-field, port, SSL and username/password-pair readiness without printing configured values; make `smtp-acceptance` use the same readiness preflight before any network attempt; preserve existing SMTP transport semantics. Technical acceptance passed with targeted modern 8/8 and full regression modern 96/96, legacy Unit 244/244, Integration 92/92 and Snapshot 7/7; implementation commit `765eb91e10b275dd73a3694f8deea5594586472e` is pushed.

### Committed sequence

This sequence is authoritative for the next product/UI work. The assistant does not invent or propose a different next Sprint while unchecked items remain here. Reordering is allowed only after a recurring real problem is recorded in `FRICTION_LOG.md`, the user explicitly approves the change, and the reason is recorded in `DECISIONS.md`. Blocking correctness, integrity, security or tooling fixes may interrupt the current work, but they do not become a new product Sprint and do not silently reorder this sequence.

- [x] UI foundation — preparation task, not a Sprint.
  - Operator outcome: the interface reads comfortably and new screens use shared design tokens instead of local `Brushes`, font sizes and spacing literals.
  - Completed: shared colors, typography, spacing/geometry tokens, control styles and Light/Dark palettes are in WPF resources; the current `MainWindow` and retained map visuals consume the shared foundation. The Light palette remains the current default; operator theme switching is not claimed yet.
  - Technical acceptance: deterministic RED 1/1, targeted Unit 4/4, full regression modern 96/96 + legacy Unit 248/248 + Integration 92/92 + Snapshot 7/7; implementation commit `7cd0d72dc0328ed847290a101363d9a3837dd86f` is pushed.
- [x] Sprint 35 — diagnostic panel for the selected network element.
  - Operator outcome: click the problem and immediately understand what happened and whom it affects.
  - Completed: device/link selection uses a transport-neutral `NetworkDiagnosticSnapshot` projected from the same coherent topology read-set as the map and alerts. Device diagnostics show readable identity/location, last-seen/resolved state, interfaces, admin/oper, STP and durable degradation. Link diagnostics show readable endpoints/ports, strength/freshness, last-seen/confirmed, media/speed, evidence with localized raw `Available` / `Expired` / `NotApplicable` state, and direction-neutral bridge/blast-radius impact from the existing graph safety analyzer. MAC/IP lookup selects the resolved device into the same panel; failed refresh keeps the last successful diagnostic snapshot as stale rather than inventing new state.
  - No new SQLite migration, monitoring semantics, incident history or causal outage claim was added.
  - Technical acceptance: deterministic RED 1/1; targeted Unit 5/5 + Integration 2/2 + modern 6/6; full regression modern 102/102 + legacy Unit 253/253 + Integration 94/94 + Snapshot 7/7. A recovery corrected only an orientation-sensitive blast-radius test expectation; product bytes were unchanged. Implementation commit `97d2ead90660cc8424f9fd383a5ee69117df13c3` is pushed.
- [x] Sprint 36 — interactive persistent map with calm semantic motion.
  - Operator outcome: arrange the map comfortably, keep the layout after restart, and see what actually changed without visual noise.
  - Completed: unlocked nodes can be dragged; locked nodes are persisted by stable `DeviceId`, visibly marked `Locked` / `Закреплён`, and reject drag until unlocked. Viewport zoom/pan and device positions survive Desktop restart through Application `IMapLayoutStore` and SQLite `Migration019MapLayout`.
  - Navigation acceptance: zoom range is `1%..500%`; middle-drag pans; `Show all` fits the visible topology back into the viewport; a very large virtual workspace allows practical movement far in every direction; compact Help replaces the long inline instruction.
  - Motion acceptance: map-change animation moved out of the primary toolbar into Map settings. `Normal` / `Reduced` / `Off` affect only semantic transitions: appearance/disappearance, freshness change, search focus and one new-alert pulse. A static stand is expected to show little or no motion because perpetual/decorative animation is intentionally absent.
  - Scope boundary: Sprint 36 does not add manual devices/links (Sprint 37), Location containers (Sprint 38), or the broader visual-language work (Sprint 39).
  - Technical acceptance: initial implementation commit `c9c6714b8663074be9864e8fb14f1c8355b76955`; operator-accepted navigation/virtual-workspace follow-up `1ff950e45f5a4d67ae72dfce1dfdecc316d9999a`. Latest regression evidence is modern 111/111, legacy Unit 266/266, Integration 98/98 and Snapshot 7/7.
- [ ] Sprint 37 — manual topology from the UI.
  - Operator outcome: draw an unmanaged device and cable that SNMP/discovery cannot see.
  - Source-audit baseline: `e10b5200dd12fbe8032ca890791c190a8e2ab418`. Existing backend semantics already provide `ManualTopologyFactory`, shared `devices` / `interfaces` / `physical_links`, protected manual deletion, manual-link protection, stable `DeviceId` / `PhysicalLinkId`, and map projection of manual elements.
  - Backend first: add one Application manual-topology command/read boundary. WPF receives only transport-neutral editor DTOs/commands and does not own Domain state or reference SQLite/Topology write implementations. `NetLoom.Desktop` remains the composition root.
  - Manual devices: create/edit/remove name, supported category and notes; persist `DiscoveryOrigin = Manual` and `MonitoringCapability = None`; never infer `Offline` merely because the device cannot be polled.
  - Manual ports: create/edit/remove named virtual ports on a manual device; persist `ifIndex = null`, `IsManual = true`, and optional media override.
  - Manual links: create links between existing devices and optional existing interfaces, including automatic ↔ manual endpoints; persist `PhysicalLinkStrength.Manual`, media type and notes. Edit link metadata without mutating canonical cable identity; changing endpoints is remove + create because a rewired cable is a new PhysicalLink identity.
  - Operator actions write a bounded manual `ObservationKind.Manual` / `SourceAddress = User` audit observation through an Application persistence boundary; manual observations remain excluded from raw-protocol retention.
  - Deletion safety: connected manual interfaces/devices cannot be deleted until their manual links are removed; automatic elements are never editable/deletable through the manual-topology UI.
  - UI: add one localized manual-topology editor reachable from the main map. It manages manual devices, ports and links, uses shared design resources, surfaces validation errors in operator language, and refreshes the existing coherent topology snapshot after a successful write.
  - Persistence: no new SQLite migration is planned. Sprint 37 reuses Migration009 materialized topology, existing `observations`, and Migration019 map layout.
  - Scope boundary: Location-container editing remains Sprint 38; node shape/size and broader map visual language remain Sprint 39; monitoring controls remain Sprint 40.
  - Acceptance: RED→GREEN backend command tests where practical; integration coverage for create/edit/delete/protection and restart persistence; localized WPF contract/interaction coverage; forced solution build; full regression; operator acceptance on the realistic stand including Desktop close/reopen.
- [ ] Sprint 38 — Locations on the map.
  - Operator outcome: read the physical object by site/building/room/rack boundaries instead of a flat graph.
  - Render movable/resizable/collapsible/lockable Location containers and preserve their layout.
- [ ] Sprint 39 — visual language of the map.
  - Operator outcome: understand at a glance what is trustworthy, stale, blocked, degraded or risky.
  - Make information hierarchy, node/link styling and map modes visually consistent for physical topology, active STP tree, confidence, freshness, degradation, rings and failure boundaries.
- [ ] Sprint 40 — monitoring control from the UI.
  - Operator outcome: start, stop, poll now and refresh topology without leaving the main window.
  - Expose current monitoring state, last successful poll/update time and the existing scheduling/policy controls through the operator UI.

### Next candidates — not commitments

- [ ] Real target-relay acceptance only when a real deployment SMTP configuration and an operator need exist.
- [ ] Dead-letter/escalation policy only if real relay/retry evidence shows a concrete terminal-failure or operator-response need.
- [ ] Routing by event/kind/severity only when an actual notification-routing need appears.
- [ ] Production configuration boundary and protected secrets for installed deployments.

### Roadmap — direction, not scheduled backlog commitment

- Runtime LTS migration together with first production release/deployment readiness.
- Offline/self-contained packaging, service install, upgrade/migration, WAL-safe backup/restore, manifests and diagnostics.
- HTTP API with localhost-by-default remote security boundary.
- Web client only after the API boundary is accepted and a real use case exists.
- Probable failure-boundary localization with structural blast radius kept separate from observed outage scope.
- Additional industrial protection protocols without false `Unprotected` conclusions from missing STP evidence; unsupported/insufficient evidence remains `Unresolved` / `UnsupportedProtectionEvidence`.
- Future Site/Probe identity if distributed monitoring becomes necessary.

The roadmap does not supersede the priority rule: recurring real friction beats speculative product work.
