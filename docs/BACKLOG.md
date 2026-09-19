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

## Execution map after Sprint 38

The realistic stand gate, Sprint 33A, Sprint 33B, the post-Sprint friction review, Sprint 34A through Sprint 34K, the UI foundation preparation task, Sprint 35, Sprint 36, Sprint 37, Sprint 38, the pre-Sprint-39 `MainWindow` decomposition preparation, and Sprint 39 are complete. The current WPF client combines coherent selected-element diagnostics, an operator-arrangeable persistent physical map, operator-managed manual devices/ports/cables, persistent hierarchical Location containers, and an operator-accepted visual language for confidence, freshness, forwarding/STP state and node degradation. Sprint 39 passed focused operator reacceptance after remediation of state differentiation, fast state focus, focus-reset context preservation and selected-element-centered wheel zoom. A blocking post-Sprint test-semantics cleanup then removed source-text pseudo-contract coverage without changing product behavior. Sprint 40 is now the first unchecked committed product item.

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
- [x] Sprint 37 — manual topology from the UI.
  - Operator outcome: draw an unmanaged device and cable that SNMP/discovery cannot see.
  - Completed command boundary: one Application manual-topology service exposes transport-neutral editor models/commands while `NetLoom.Desktop` remains the composition root. WPF does not own Domain topology state and does not reference SQLite/Topology write implementations.
  - Completed manual topology behavior: manual devices, virtual ports and manual cables use the shared `devices` / `interfaces` / `physical_links` graph; automatic devices/interfaces may be cable endpoints but remain read-only in the editor; operator actions use bounded `ObservationKind.Manual` / `SourceAddress = User` evidence; coherent refresh makes successful writes visible on the existing map and persisted manual topology survives Desktop restart.
  - Deletion/editing acceptance: double-click/context-menu editing and `Delete` are discoverable on the map; destructive removal asks for confirmation; only a real referencing cable blocks removal of a manual device/interface; rewiring remains remove + create rather than mutating canonical PhysicalLink identity.
  - Truthful diagnostics remediation: automatic interface presentation uses observed IF-MIB `ifName` / `ifAlias` / `ifIndex` / `ifType` and never synthesizes vendor-specific port names from speed/media. Management address is shown only as observed metadata and never becomes DeviceId. Node cards size to measured content, and selecting a cable no longer moves the viewport as a side effect.
  - Persistence: the manual graph itself still reuses the existing materialized topology tables and Migration019 layout state. Final operator-remediation adds `Migration020InterfaceIdentityAndManagementAddress`, which adds nullable `devices.management_address` and `interfaces.if_type` so truthful observed identity/address data can reach map diagnostics.
  - Scope boundary: Location-container editing remains Sprint 38; broader map visual-language work remains Sprint 39; monitoring controls remain Sprint 40.
  - Acceptance: backend implementation `dce375ff0c7e35a520ad31aeb5bd95fa105f580a`; initial WPF editor `2f8c7a9439749e9209cb1a06ec340e3b24bb6a6a`; final root-cause remediation `e94538d173c4bea4fa5bf4c3a650a270b8e65906`. The pass-4 repository runner completed forced build, cause-oriented targeted checks, full regression, exact boundary/blob proof and push; focused operator acceptance then passed on the realistic stand, including restart persistence and the previously failed interaction/diagnostic scenarios.
- [x] Sprint 38 — Locations on the map.
  - Operator outcome: read the physical object by site/building/room/rack boundaries instead of a flat graph.
  - Completed hierarchy behavior: `ParentLocationId` defines containment; moving a Location carries its physical subtree, child drag and parent resize preserve containment, collapse hides the subtree, and lock/collapse/layout state survives restart.
  - Completed editor/service behavior: Browse is read-only until explicit Edit; repeated Create works in one open editor; `Create → Create → Edit` remains valid; reparent is supported; missing parent, cycles and non-leaf deletion are rejected by the Application service.
  - Startup remediation: after the initial refresh the existing `FitTopologyToViewport()` establishes a useful viewport, so restart does not require a manual `Show all` just to find the configured topology.
  - Acceptance: architecture redesign commit `8e98f4f174b6caa6a7d0d88b09f68feda0f20db0`; operator-remediation commit `9a8127715ffaf2a19091d1903d02251e8ef8df01`. RED evidence covered the real Create button path and stale startup viewport; forced build, targeted Sprint 38 Unit/Integration, full regression, text-integrity and exact repository-boundary/blob proofs passed. Focused operator reacceptance then passed both original observations.
- [x] MainWindow decomposition — preparation task, not a Sprint.
  - Preparation outcome: current WPF behavior is unchanged while the next map work is reviewable and locally understandable.
  - Completed split: diagnostics/selection panel and lookup/search moved to focused partial files in `f4fce6b55ca2cfbd92760ddaeeb3354d4d361073`; alerts moved in `e85e1f896c230f8e8684d8072e9f0cc6343bd830`; map/viewport/input/rendering moved to `MainWindow.Map.*` partial files in `c8201f1cedb46ede574d85bb975d9c744a148626`.
  - `MainWindow.xaml.cs` was reduced from roughly 8.5k lines to roughly 0.9k lines without changing UI behavior, XAML contracts, persistence/schema, localization semantics, monitoring semantics or dependencies.
  - Acceptance: each step used forced build, full regression, text-integrity, exact source-boundary/blob proof and diff review; the final map split was verified as a byte-for-byte move-only decomposition of the original map block.
- [x] Sprint 39 — visual language of the map.
  - Operator outcome: understand at a glance what is trustworthy, stale, blocked, degraded or risky.
  - Link trust/freshness grammar uses existing `MapLink` evidence through independent channels: `High` / `Medium` / `Low` confidence controls solid / long-dash / short-dash line grammar, while `Fresh` / `Aging` / `Stale` controls opacity. Selection is a separate interaction layer and preserves the underlying dash/freshness semantics.
  - Existing STP/ring/forwarding-safety evidence is consumed directly by presentation rather than inferred from geometry. Confirmed forwarding-cycle or `Broken` evidence is Critical; degraded ring protection or `Disabled` is Degraded; `Blocking` is Blocked; `Listening` / `Learning` is Transition; confirmed `Forwarding` is visually healthy; unresolved/unknown evidence stays neutral.
  - Node degradation is truthful and independent from link state: only existing `DeviceDiagnostic` evidence with at least one `Degraded` interface produces degraded-node styling. `Healthy`, `Unknown`, missing diagnostic/interface evidence and link state do not synthesize node health.
  - Holistic operator remediation strengthened state differentiation with compact state-dependent stroke widths and distinct semantic colors, and added `Map settings → Focus by state` for all problems / Critical / Degraded / Blocked / Transition / degraded nodes. Healthy forwarding links stay out of the all-problems focus.
  - Interaction remediation preserves operator context: clearing state focus removes only the focus/dimming layer and does not change pan/zoom or selection; wheel zoom with a selected node, link or Location keeps the selected element centered; with no selection, cursor-centric wheel zoom remains unchanged.
  - Acceptance: incremental implementation commits `4c9104ce2879a3159802e16b93951a516360c55b`, `7066ca0c4fb07d660448eb0a3a898d25e8135879` and `5cdb5423b4083eb9dfab8cf899b24f4313d077e7`; operator-remediation commit `551472caf2e767c92025fa266d03f356807e697b`. Forced build, targeted Sprint 39 checks, full regression, text-integrity, exact repository-boundary/blob proofs and focused operator reacceptance passed.
  - Sprint 39 adds no migration; `Migration020InterfaceIdentityAndManagementAddress` remains the current schema migration.
- [x] Post-Sprint-39 test-semantics cleanup — blocking correctness/tooling task, not a Sprint.
  - Removed source-text pseudo-contract tests that searched C#/XAML/SQL for method names, event handlers, implementation fragments or incidental constants instead of proving behavior.
  - Persistence coverage now uses real production-boundary round-trip behavior; UI-only behavior that cannot be reliably automated is operator-acceptance evidence rather than substring coverage.
  - `AI_DEVELOPMENT_RULES.md` now requires `input → production action → observable result`, distinguishes integrity/static-analysis checks from behavioral coverage, and keeps one operator outcome under one Sprint number without `A/B/C` slicing.
  - Acceptance: cleanup commit `7b78b1818dca3a0048f4d3d93a77255ed8309866`; full Unit/Integration/Modern/Snapshot regression, repository text-integrity, exact 9-file boundary/blob/deletion proof and push passed with `HEAD == origin/main` and a clean worktree.
- [ ] Sprint 40 — monitoring control from the UI.
  - Operator outcome: start, stop, poll now and refresh topology without leaving the main window.
  - Expose current monitoring state, last successful poll/update time and the existing scheduling/policy controls through the operator UI.

### Next candidates — not commitments

- [ ] Extend the existing Sprint 36 calm semantic motion to one-shot operational/degradation state transitions only when a state actually changes. Preserve `Normal` / `Reduced` / `Off`, avoid perpetual/decorative animation, and keep direct pan/zoom immediate.
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
