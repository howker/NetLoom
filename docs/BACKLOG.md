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
- [ ] SFP/DDM optical health — candidate after the parallel real-hardware capability audit; not part of the current committed sequence.
- [ ] SNMP trap ingestion.
- [ ] Syslog ingestion.
- [x] Export site diagram — PNG completed in Sprint 44; PDF remains a later candidate only on real request.
- [x] Inventory and CSV export — completed in Sprint 44.
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
- [x] Multi-device scheduler — completed in Sprint 43: one Engine multi-target host with bounded parallelism, per-device cadence, startup jitter and backpressure.
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

The realistic stand gate, Sprint 33A, Sprint 33B, the post-Sprint friction review, Sprint 34A through Sprint 34K, the UI foundation preparation task, Sprint 35, Sprint 36, Sprint 37, Sprint 38, the pre-Sprint-39 `MainWindow` decomposition preparation, Sprint 39, Sprint 40, Sprint 41, Sprint 42, Sprint 43, Sprint 44, and Sprint 45 are complete.
The current WPF client combines coherent selected-element diagnostics, an operator-arrangeable persistent physical map, operator-managed manual devices/ports/cables, persistent hierarchical Location containers, monitoring control, glance-readable topology semantics, operator-driven discovery, one-Engine multi-target monitoring, and coherent operator export of the full site diagram plus inventory.
Sprint 44 closed bounded PNG plus UTF-8 BOM CSV export from one canonical `TopologyExportSnapshot`, with the operator action inside existing Map settings. Sprint 45 is now the first unchecked committed product item.
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
- [x] Sprint 40 — monitoring control from the UI.
  - Operator outcome: start, stop, poll now and refresh topology without leaving the main window.
  - Completed architecture: `NetLoom.Engine` remains the only polling/scheduling host; `NetLoom.Application` exposes the transport-neutral monitoring-control contract; `NetLoom.Desktop` owns the child Engine process and protects it with cooperative stdin control plus a Windows kill-on-close Job Object; WPF consumes only the control contract.
  - Completed operator semantics: the selected device keeps its stable `DeviceId`; the editable target address is pre-filled from observed `management_address` when available but may be entered manually for the first poll/session override, and that manual value is not persisted as observed topology metadata. A running session keeps its active target when map selection changes. `Refresh topology` continues through the existing `TopologyRefreshCoordinator`. Polling policy and manual target overrides remain session-only.
  - Acceptance: monitoring-control foundation `375f036d04947e83c89ab180bcb74f912a069085`; Desktop process adapter `30563b44fda3253f5f101934616b4f97ad7898d5`; WPF monitoring controls `2df981f6766de0864ce6021eff60ca6f700d1c42`. Closure regression passed Modern 135/135, Unit 304/304, Integration 111/111 and Snapshots 7/7, repository text-integrity, `linux-x64` publish and a real WSL `runtime-smoke`. Operator acceptance on an isolated 8-device / 7-link stand passed Start, Poll now, selection changes without active-target drift, topology refresh, Stop, manual-address polling for a device without observed `management_address`, session-only reset after restart, and Desktop-close orphan prevention.
  - No schema migration was added. A repeated UI-friction observation remains open: long device names can be ellipsized in map cards; it is recorded for the `Glance-readable topology` candidate and does not invalidate the accepted monitoring outcome.

- [x] Sprint 41 — glance-readable topology.
  - Operator outcome: identify the device category and distinguish long device names at a glance without reading a dense card.
  - Completed card grammar: 160x56 cards show scalable vector category iconography plus the full device name with wrapping; dense technical metadata remains in the existing diagnostic panel. Category is still evidence-backed only for the real NetLoom categories `Unknown`, `MediaConverter`, `UnmanagedSwitch`, `OpticalConverter`, and `PassiveNetworkEquipment`.
  - Completed state/selection grammar: the left stripe shows evidence-backed node degradation state (`Healthy`/normal = success, `Degraded` = warning, incomplete/unknown evidence = neutral). Selection is an interaction layer, not a health state: a selected node uses the blue selection stripe plus a full blue card outline, then returns to its underlying state presentation when selection is cleared.
  - Completed link readability: the neutral base link is thicker and higher-contrast with rounded caps, while existing confidence dash, freshness opacity and evidence-backed `Forwarding` / `Degraded` / `Critical` / `Blocked` / `Transition` colors remain independent. No directed arrows or generic invented online/offline state were added.
  - Regression evidence covers long-name layout/category icon behavior, truthful node-state presentation and reconciliation using actual card geometry rather than stale fixed-width constants. Operator acceptance on the 8-device / 7-link stand initially rejected the missing state stripe/selection distinction and weak links at 66% zoom; the remediated card and link language was then accepted with no remaining remarks.
  - Closure: forced full solution build, full Modern/Unit/Integration/Snapshot regression, repository text-integrity, `git diff --check`, exact source/index/commit/HEAD-tree proofs and push all passed with `HEAD == origin/main` and a clean worktree. No schema migration or new dependency was added.

- [x] Sprint 42 — safe operator-driven network discovery.
  - Operator outcome: enter an authorized IPv4 start/end range plus subnet mask, choose one explicit SNMP profile, watch progress and discovered candidates, stop discovery, and see newly discovered devices appear on the live map without restarting the client.
  - `NetLoom.Engine` remains the only discovery host. WPF uses the existing Desktop discovery-control boundary and receives incremental progress/candidate events; it does not own a second scanner/runtime path.
  - The operator range is validated as IPv4, ordered, bounded to 4096 addresses, and contained by the entered subnet mask. The Engine retains legacy CIDR support internally, while the accepted WPF surface uses start address / end address / subnet mask.
  - Discovery keeps the existing conservative defaults: one explicitly selected SNMP profile, no credential guessing or profile iteration, bounded management-oriented TCP probes, cancellation, and inter-address delay/rate limiting with an operator warning.
  - Discovered candidates are materialized through the existing topology repository. Existing automatic devices are reconciled by observed management address and retain stable `DeviceId`; manual devices are not overwritten. Discovery does not create `PhysicalLink`; links remain LLDP/CDP/manual evidence.
  - After materialization the existing topology refresh/reconciliation path is reused. The new device is automatically selected, centered at native 100% map scale, and visibly pulses for several seconds; this focus behavior does not add a second viewport/navigation model.
  - Acceptance: operator acceptance passed on the isolated synthetic discovery stand, including live appearance, automatic selection/focus and visible pulse. Final technical regression passed Modern 156/156, Unit 319/319, Integration 113/113 and Snapshots 7/7. Exact 21-file review/index/commit/blob proofs passed; technical commit `c49cdd306e66b077ab3e44675ce8e79102b97907` is pushed with `HEAD == origin/main` and a clean worktree.
  - No SQLite migration or new production dependency was added.

- [x] Sprint 43 — multi-target monitoring in one Engine.
  - Operator outcome: NetLoom continuously monitors the eligible target set instead of only one selected target.
  - Completed process/runtime boundary: Desktop owns one Engine `schedule-set` process; targets retain stable `DeviceId` plus management address, and the scheduler provides per-target cadence, deterministic startup jitter, bounded concurrency, drop-on-busy backpressure, cancellation that stops new scheduling, and no overlapping poll for the same target.
  - Completed failure isolation: Health is probed first for each scheduled target; timeout/socket failure fast-fails only that target cycle, while other targets continue. Poll-slot release is decoupled from maintenance/delivery work so slow completion work does not hold scheduler concurrency.
  - Completed WPF behavior: Start monitors all snapshot-eligible targets with stable `DeviceId` and valid management address; Poll now wakes the running target set, while the stopped-state action remains selected-target only. Startup auto-fit remains active across later startup `SizeChanged` events until explicit operator map interaction, so restart no longer requires `Show all` after the viewport settles.
  - Measured acceptance policy: on the controlled 15-target Sprint 43 stand, `startup-jitter=15s` with concurrency `1` completed 4/15 and skipped 11 for backpressure, concurrency `2` completed 11/15 and skipped 4, and concurrency `4` completed 15/15 with 0 backpressure skips. A control run with concurrency `4` and jitter `0` completed only 4/15 and skipped 11, so the accepted WPF policy is `max-concurrency=4` plus `startup-jitter=15s`. Real-network verification remains part of Sprint 45 rather than being inferred from the synthetic stand.
  - Acceptance: the 15-target database contained 10 reachable loopback targets and 5 intentionally unavailable TEST-NET targets; the live WPF session remained `Работает`, kept 15 available/active targets, and continued successful polling despite unavailable targets. Startup viewport reacceptance passed after root-cause remediation commit `b1af6437c4c424e4b750fb8da18732e1eed43789`; measured-policy commit `af78cd05a5fa90023f1372d4e679d8c7805c6583` is pushed. Final closure gates passed forced solution build, Modern 173/173, Unit 335/335, Integration 113/113, Snapshots 7/7, repository text-integrity, exact two-file policy boundary/blob proof, `HEAD == origin/main`, and a clean worktree.
  - No SQLite migration, credential/profile redesign, or new production dependency was added.

- [x] Sprint 44 — export the site diagram and inventory.
  - Operator outcome: export a readable site diagram and equipment/interface list that can be handed to a colleague/customer after an assessment.
  - Completed coherent boundary: PNG and CSV are generated from one `TopologyExportSnapshot`; the export uses canonical topology/diagnostic data plus persisted map/Location layout rather than the live virtual Canvas or a second topology interpretation.
  - Completed PNG behavior: render a separate bounded full-site diagram from actual topology/Location bounds plus margin, preserve aspect ratio, cap final pixel/memory budget, ignore temporary viewport pan/zoom/selection/search/focus layers, and expand collapsed Locations only in the export model.
  - Completed CSV behavior: export device/interface inventory as UTF-8 with BOM for Russian Windows/Excel use from the same snapshot used by PNG.
  - Completed operator surface: `Map settings -> Export site diagram and inventory...` saves sibling `.png` and `.csv` files with one basename; no permanent toolbar control was added. PNG overwrite uses the save dialog prompt, and an existing sibling CSV receives its own Yes/No overwrite confirmation before either file is written.
  - Technical acceptance: canonical snapshot boundary `701824d`, CSV exporter `9163d3b`, bounded PNG renderer `122c8375dba51551e2b3a865c270adc914daa1a4`, and operator export surface `834f0eba9db6ead0a7daa64a59ec4e322f7caa44` are pushed. Final regression passed Modern 183/183, Unit 349/349, Integration 113/113, Snapshots 7/7; text-integrity, exact boundaries, index/HEAD blob proofs, push, `HEAD == origin/main`, and clean postflight passed.
  - Operator acceptance: a real WPF export produced a readable 1730x950 PNG plus matching UTF-8 BOM CSV in one action. The exported visual matched the current map presentation; pre-existing on-map card overlaps were not introduced by export. The supplied CSV contained 22 columns, 23 data rows and 17 unique devices.
  - No SQLite migration or new production dependency was added. PDF, SVG and draw.io remain outside Sprint 44 acceptance.
- [x] Sprint 45 — full acceptance on the author's real network.
  - Operator outcome completed on the field site: discovery, topology materialization, multi-target monitoring, diagnostics and export were exercised against the real environment without development-only topology shortcuts.
  - Field baseline: 55 devices, 275 interfaces and 10 physical links; platform acceptance included Windows Server 2012 R2 in the actual unpatched state present on site.
  - Closure and field evidence: `docs/sprint45-field-acceptance.md`. Navigation/UX friction from the field session is recorded in `FRICTION_LOG.md` and drives Sprints 46–49.
  - Closure baseline: `f475fec`; Sprint 45 closed with 543 tests and 21 migrations.

- [x] Sprint 46 — new shell and shared operator context.
  - Closed by owner decision on 2026-10-07; closure record in `docs/PROJECT_STATE.md` («Sprint 46 closure»). The on-site check on the real network and Windows Server 2012 R2 is done outside the Sprint when the site is available.
  - Operator outcome: the operator understands where they are, which SNMP profile is active and what the system is doing while the current working view receives the main window width.
  - ADR-083 is the canonical shell structure. The old permanent 198 px rail + 380 px section sidebar + 342 px inspector layout is superseded. The old `obolochka` / `paneli` mockups are no longer acceptance references.
  - Step 1 — documentation and checkpoint: commit the current `src`/`tests` state; add ADR-083, replace `UI_DESIGN_RULES.md`, add all 16 `netloom-v2-*` Light/Dark mockups, and update the Sprint plan, backlog, AI rules and friction log in a separate documentation commit.
  - Step 2 — shell framework only, using mockups 1, 2, 4, 5, 6, 7 and 8: 56 px icon rail (`Карта`, `Оборудование`, `Предупреждения`, `Обнаружение`, settings at the bottom); top row with breadcrumbs, Ctrl+K global search, monitoring state + Start/Stop and active SNMP-profile chip; one central view at a time; inspector 320–520 px with collapse behavior; editors as central views; neutral normal state; operator term `Размещения`.
  - Inspector defaults from ADR-083: open on `Карта` and `Оборудование`, collapsed on `Предупреждения` and `Обнаружение`, hidden on `Настройки`. On narrow windows the map stays at least 60% of window width; if a column inspector violates this, it becomes an overlay drawer.
  - ADR-082 remains binding: Sprint 46 uses already persisted data only and does not add collection, storage semantics or SQLite migrations merely to populate the UI. `Модель или описание` uses already stored `sysDescr` when available; missing evidence stays missing.
  - Normal state is neutral. Red/yellow are deviations only. Device/link state and active-alert severity remain separate. The unresolved blocker where a card shows yellow/problem semantics while Inspector and the warnings count provide no explainable active cause remains a Sprint 46 closure blocker.
  - Monitoring progress `N / total`, current target and cycle counters remain Sprint 47. Sprint 46 shows only the always-visible monitoring state and Start/Stop surface required by ADR-083.
  - Access-profile resolution must be completed before the §9 gallery/style pass, using the ADR-083 amendment:
    - [x] restore a valid persisted `UiShellState.AccessProfileId`;
    - [x] preserve the existing `0 profiles` state (`Добавить профиль`; no new empty-state pattern);
    - [x] with exactly one profile and no valid saved selection, auto-select it and persist it through the same `UiShellState` path as manual selection, including restart coverage;
    - [x] with `2+ profiles` and no valid saved selection, including a deleted saved profile, keep `SelectedIndex = -1`, show `Выберите профиль`, and never silently choose the first profile;
    - [x] while the profile is unresolved, monitoring start and discovery start are disabled with an operator explanation; profile-independent views remain usable;
    - [x] the placeholder comes from the shared `ComboBox` style and has the same Light/Dark semantics; this targeted style state is a gallery prerequisite, not the full §§4–§8 style pass;
    - [x] §9 gallery covers `0 profiles`, `1 profile auto-selected`, and `2+ profiles unresolved` with the disabled profile-dependent start actions visible.
  - Step 2 acceptance is owner review, but only from a committed state: provide commit hash, exact changed-file list, `сделано — файлы` / `не сделано` for every requested item, and screenshots of all seven implemented views in Light/Dark at 1920 and 1366. If Segoe UI does not fit where Carlito did in the mockups, apply ADR-083 §5 narrow-window behavior rather than clipping.
  - Step 3 starts only after shell-framework acceptance: rebuild the §9 state gallery on the new framework; after gallery acceptance establish the shared §§4–§8 style/accessibility system; only then fix remaining Pass 3 defects that were not eliminated by ADR-083.
  - ADR-083 eliminates five old-shell Pass 3 items as local-remediation tasks: the monitoring section panel, the narrow equipment list, map bleed around editor overlays, export hidden in settings, and a separate Search section.
  - Remaining product/UI obligations still include the event occurrence-time contract, explainable state/severity grammar, stale wording, evidence-only description/identity presentation, restart viewport, PNG/CSV export, MAC/IP lookup and the real Windows Server 2012 R2 delivery fix before the next field visit.
  - Final acceptance follows `docs/sprint46-ui-ux-redesign.md`, ADR-083 and `docs/UI_DESIGN_RULES.md` §10 on the field database / ~55-device workflow and real Windows Server 2012 R2.

- [x] Sprint 47 — visible monitoring progress.
  - Accepted and closed by the owner on 2026-10-07; closure record in `docs/PROJECT_STATE.md` («Sprint 47 closure»). Commits `072ea51`, `1ae7cdf`.
  - Operator outcome: one glance shows whether polling is running, the current device, completed/remaining work, errors and whether the cycle finished.
  - States: stopped / starting / running cycle / stopping / error.
  - Show `N / total`, progress bar, current device and address, successful/error/remaining counts, and cycle start/end time.
  - Cycle events go to the bottom event strip.
  - Context actions: `Poll selected` when stopped; `Start cycle now` when the scheduler is running.
  - The Sprint 46 top-row monitoring state is extended with cycle progress; Sprint 47 does not reintroduce a permanent monitoring section/panel.
  - If separate ICMP / SNMP / TCP availability cannot be presented from data already persisted by Sprint 46, the required new collection/projection belongs here with visible polling results; Sprint 46 must not add a new collection path just to fill Inspector fields.
  - Unreachable-device alert, moved from Sprint 46 by owner decision on 2026-10-07 (field check `docs/sprint46-field-check.md` Г3): a managed device that stops answering polls raises an alert with the time of the last successful poll; today it is visible only by the age of its data.
  - Implementation record 2026-10-07:
    - Cycle progress: `MonitoringCycleTracker` (Application) builds the cycle from per-device Engine markers — the multi-target scheduler has no global cycle; cycle k ends when every device of the set has had its k-th attempt (polled or skipped by backpressure). Desktop publishes current cycle, last completed cycle and per-device outcomes in `MonitoringControlSnapshot`.
    - Top row: the existing monitoring state becomes a button — «Опрос: N / всего» with a thin neutral progress bar during a cycle; details on click: current device and address, succeeded / failed / skipped / remaining, start and finish, devices that did not answer; `Запустить цикл сейчас` (POLL_NOW) while the scheduler runs, `Опросить выбранное устройство` when stopped. No new permanent top-row element.
    - Event strip: one «Цикл опроса завершён» event (latest only); the strip now shows only events that fit entirely (`Shell/FitStackPanel`) — the third event used to run under «Все события».
    - Unreachable device: `TopologyAlertKind.DeviceUnreachable` (device-scoped, Warning) after 2 consecutive polls without any successful step (`MonitoringAlertProjection`), merged into the shared alert snapshot — cards, rail count, events, Equipment, map card and Inspector use one source. The failure streak survives a monitoring restart within the app session; failed polls are not persisted, so an app restart starts clean.
    - Owner decision 2026-10-07: threshold 2 polls and Warning severity confirmed; separate ICMP / TCP availability (monitoring polls SNMP only today) is done in Sprint 47.
    - ICMP / TCP availability: after every SNMP poll Engine checks ICMP and TCP 22/80/443 with the discovery probe (`MonitoringAvailabilityChecker`) and appends `icmp=… tcpChecked=… tcpOpen=…` to the poll-completed line; a probe failure is «not checked», never «unavailable». Availability is not a poll step: answering ping does not make an SNMP poll successful. The Inspector state line uses the ТЗ §10 vocabulary while monitoring runs — «Доступен» (SNMP answered), «Частично доступен» (ICMP only), «Недоступен» — with ICMP / SNMP / TCP rows in «Сведения»; the unreachable-device reason says whether the device still answers ICMP. Session-only, like the other cycle data.

- [x] Sprint 48 — discovery with explainable results and an inbox.
  - Accepted and closed by the owner on 2026-10-08; closure record in `docs/PROJECT_STATE.md` («Sprint 48 closure»). Merges `bd6970d`, `527ec13`.
  - Operator outcome: after discovery the operator can tell what is new, changed, ambiguous, missing, excluded or failed, why it happened, and can resolve results in bulk; a profile can be validated before use.
  - Inbox groups: New, Changed, Ambiguous, Missing, Excluded, Error.
  - Required bulk actions: accept selected, ignore, mark unmanaged, assign placement (`Размещение` in operator-facing UI).
  - Every row shows a concrete reason and retry context; `Missing` means a known device was not found in this run, and `Excluded` names the matching profile rule.
  - New devices appear on the map immediately as unconfirmed until the operator resolves them in the inbox.
  - Profile validation reports availability and MIB coverage for sysName/sysObjectID, IF-MIB, LLDP-MIB, BRIDGE-MIB and Q-BRIDGE-MIB where applicable.
  - Discovery progress shows only phases that actually exist: ICMP, TCP and SNMP.
  - Moved from Sprint 46 by owner decision on 2026-10-07 (field check `docs/sprint46-field-check.md`):
    - Г1 — device category from evidence: LLDP system capabilities (bridge, router) and sysObjectID become a category instead of «Неизвестно» for every device.
    - Г2 — sysDescr received during discovery is persisted, so model/description appears in Equipment and the Inspector and the Equipment filter finds devices by model.
    - Г4 — one-sided LLDP (the neighbour reports the link, the device does not) is shown as an explained evidence gap or alert, not as a plain confirmed link.
  - Owner decisions 2026-10-07:
    - Unconfirmed devices are polled by monitoring right away; the «unconfirmed» mark stays until the operator resolves the row in the inbox.
    - «Ignore» hides the device from the map and from monitoring; later runs list it under Excluded with the reason «ignored by the operator <date>», and the decision can be undone.
  - Owner decisions 2026-10-08 (on the autopilot report): confirmed as implemented — «mark unmanaged» turns SNMP polling off and confirms the device without changing its category; an address error is reported only when the address answered ICMP/TCP or the failure is not a timeout; a running monitoring set restarts once after a discovery run that added devices; Excluded rows have no «Проверить снова» (ignored rows have «Отменить игнорирование»); Ambiguous covers an address owned by several devices and a sysName already used by a device on another address. The profile exclusion rules editor goes to Sprint 51.
  - Implementation decisions (from the rules, not owner questions):
    - Inbox groups follow this backlog (New, Changed, Ambiguous, Missing, Excluded, Error). «Ready / partial» from `sprint46-ui-ux-redesign.md` is a completeness mark with a reason on New and Changed rows; «known, unchanged» is a count in the run summary.
    - Devices that exist before the migration are confirmed; a manually set category is never overwritten by evidence.
    - Г4 is explained in the link Inspector as an evidence gap, not raised as an alert: normal state stays neutral.
  - Implementation record 2026-10-07:
    - Г2: `Migration022DeviceSystemIdentity` adds `devices.sys_description` and `devices.sys_object_id`. Discovery stores them; an LLDP neighbour's `lldpRemSysDesc` fills them for a known automatic device. A save that does not carry them never erases them (`COALESCE`, like the management address). «Модель / описание» shows the operator's vendor/model, otherwise the first line of sysDescr; the full sysDescr and sysObjectID are in «Технические детали»; the Equipment filter finds the model.
    - Г1: LLDP capabilities were stored as the SNMP text form, which lost the BITS (0x20 bridge is a space, 0x08 router a control character). They are now stored as hex (`28:00`), and the device's own `lldpLocSysCapEnabled` is collected. New categories `Switch` / `Router`: bridge (with or without router) — switch, router alone — router; only automatic devices get a category from evidence, a manual category is never overwritten; a neighbour report does not change the device's last-poll time.
    - Existing field databases: categories appear after the next monitoring poll; the model appears from LLDP neighbours during monitoring or after the next discovery run.
    - Tests: `Sprint48SystemIdentityTests` (Unit), `Sprint48DeviceSystemIdentityTests` (Integration); field check F03 now checks the model filter and categories instead of noting the gap.
    - Run summary: after a run completes, stops or faults, «Последний запуск» shows start time, duration, checked «N из M адресов» and found «K, SNMP ответили: L» (hidden while a run is active). Session-only; storing runs belongs to the inbox. The per-address error count waits for Engine to report errors per address. Tests: `Sprint48DiscoveryRunSummaryTests` (part of `Sprint42WpfDiscoveryPanelTests`), gallery `Sprint48UiStateGalleryTests.cs` → `artifacts/ui-state-gallery/sprint48-discovery/`.
    - Global search hint (§4): at a 1100 px window «Устройство, IP или MAC» was cut by an ellipsis with no tooltip (gallery finding since Sprint 46, never recorded). The hint now switches to the short «Поиск» when the full one does not fit; fixed, not deferred.
    - Gallery clipping check: a clipped text counts as covered when the tooltip is on the text or on the control that owns it (for example the profile selector: text inside the ComboBox template, tooltip on the ComboBox). Before, frame 08b reported «Единственный профиль · SNMP v2c» although the full name was in the selector's tooltip.
    - Г4: the link diagnostic carries which ends report the link over LLDP (`DiagnosticLldpReporting`, from the `device:<id>|` side mark of each LLDP evidence row). The link Inspector shows «Сообщают о связи» (both ends / only <device>) and, for a one-sided link, the first «Основания» row is an evidence gap naming the silent device and the possible causes (LLDP disabled or unsupported, device not polled). No alert, neutral text. Manual links and legacy evidence without a side mark show nothing. Tests: `Sprint48OneSidedLldpTests`, gallery `sprint48-g4`, field check F15 (kb-sw-03 ↔ kb-sw-04).
    - Discovery phases: Engine reports the phase it is about to run for each address (`NETLOOM_DISCOVERY state=phase … phase=icmp|tcp|snmp step=N steps=M`); TCP exists only when ports are configured. The discovery card shows «Этап: SNMP · 3 из 3», and «Текущий адрес» is now the address being probed (it used to be the last finished one). Tests: `DiscoveryEngineTests`, `Sprint42DesktopDiscoveryControlTests`, `DiscoveryPanelShowsCurrentPhaseAndClearsItAfterAddress`, gallery `sprint48-phases`.
    - Per-address errors: `SnmpTransportFailure.Authentication` added; SNMPv3 usmStats reports (unsupportedSecLevels, unknownUserNames, wrongDigests, decryptionErrors) and error status noAccess/authorizationError are authentication errors (`SnmpFailureClassifier`). Rule (implementation decision, owner to confirm): an address error is reported when SNMP gave no inventory and the address answered ICMP or TCP, or the failure is not a timeout/unsupported credentials — a silent address with a timeout means «no device». Engine adds `snmpError=` to the candidate line (absent = none for older Engines). The run summary shows «Ошибок», the row «SNMP: ошибка аутентификации · профиль … · время». Tests: `DiscoveryEngineTests`, `Sprint48SnmpFailureClassificationTests`, Modern marker tests, `ErrorCandidateShowsReasonAndCountsInRunSummary`, gallery `sprint48-errors`.
    - Run journal: `Migration023DiscoveryRuns` (`discovery_runs`, `discovery_run_results`, `discovery_run_result_changes`; last 50 runs kept). `DiscoveryRunJournal` classifies each address before materialization — New / Changed (fields: name, description, sysObjectID, interface count) / Ambiguous (management address shared by several devices — nothing is applied; or the sysName already belongs to a device at another address) / Missing (known device in the range not found by a completed run; stopped runs report no Missing) / Excluded (profile rule) / Error / KnownUnchanged, with completeness ready or partial (no SNMP answer, no sysName, IF-MIB without interfaces) for new and changed rows. The window starts, records and finishes runs through the journal; «Последний запуск» (with «Ошибок» and «Без изменений») comes from the database after a restart; a run cut by closing the app is marked `DISCOVERY_INTERRUPTED`. Tests: `Sprint48DiscoveryRunRepositoryTests`, `Sprint48DiscoveryRunJournalTests` (Integration), `Sprint48DiscoveryExclusionRuleTests`, window tests in `Sprint48DiscoveryRunSummaryTests`.
    - Unconfirmed devices: `Migration024DeviceConfirmation` (`devices.is_unconfirmed`, existing devices confirmed). Only devices created by a discovery run are unconfirmed (LLDP-created and manual ones are not); a later save never clears the mark — only `IDeviceConfirmationStore.SetUnconfirmed` does (the inbox, item 7). The map card shows «Не подтверждено» on its second line, Equipment shows it under the name (the description column is hidden at narrow widths), the device Inspector starts with «Подтверждение». Monitoring polls them as any map node; a monitoring target set that is running is restarted once after a discovery run that added devices (Engine cannot change the set on the fly). Tests: `Sprint48DeviceConfirmationTests` (Integration and Unit), `DiscoveryRunThatAddsDevicesRestartsRunningMonitoringSet`, gallery `sprint48-unconfirmed`.
    - Fixed while checking the gallery (§4, found at 1100 px, never recorded): Equipment headers «СВЯЗЕЙ» and «ОБНОВЛЕНО» broke in the middle of the word; the columns now have minimum widths (tokens).
    - Open, СРЕДНЯЯ (§4, found at 1100 px, never recorded): in Equipment the filter row (field 300 px + segmented filters) is wider than the list at a 1100 px window with the Inspector open, so «Ручные · N» is cut by the panel edge. Needs a narrow-width layout (field shrinks or filters wrap) — not part of Sprint 48 scope; gallery `sprint46-pass3/15-equipment-list`, `sprint48-unconfirmed/36`.
    - Inbox: the right side of «Обнаружение» shows the latest run grouped New / Changed / Ambiguous / Missing / Excluded / Error (empty groups hidden, «known, unchanged» only a count), rows sorted by numeric IPv4, each with a concrete reason, completeness for New/Changed, the changed fields for Changed, the profile rule for Excluded and the last answer age for Missing. «Проверить снова» runs one address (/32) and replaces the row inside the same run without changing the run's time or counts of checked addresses; Excluded rows have no retry (implementation decision). Layout without a mockup: row cards like «Предупреждения», collapsible groups. Tests: `Sprint48DiscoveryInboxProjectionTests`, `Sprint48DiscoveryInboxWindowTests`, gallery `sprint48-inbox`.
    - Bulk actions: rows are selected per row or per group and resolved with «Принять» (confirm the device; for Missing only acknowledge), «Игнорировать» (`Migration025DeviceIgnore`, `devices.ignored_utc`: hidden from the map, Equipment and monitoring; later runs do not probe the address and list it under Excluded as «Игнорировано оператором <date>»; «Отменить игнорирование» undoes it), «Отметить как неуправляемое» (implementation decision, owner to confirm: monitoring capability None like manual devices — stays on the map, no SNMP polling — and confirmed; category unchanged) and «Назначить размещение…» (placement dialog, confirmed). Rows keep their resolution; actions apply only where `DiscoveryInboxActions.CanApply` allows and report skipped rows. Tests: `Sprint48DiscoveryInboxActionsTests` (Integration, Unit), `Sprint48DiscoveryInboxActionsWindowTests`, gallery `sprint48-inbox-actions`.
    - Profile check before saving: the SNMP profile dialog checks a device address — availability (ICMP time), sysName/sysObjectID, IF-MIB (interface count), LLDP-MIB (neighbour count, or present without neighbours), BRIDGE-MIB, Q-BRIDGE-MIB (full / partial / absent); an authentication failure stops the rest («не проверено»); BRIDGE-MIB and Q-BRIDGE-MIB absence is neutral (the device may not be a switch). `SnmpProfileChecker` (Application) runs in Engine `check-profile`; the typed community goes only through the process environment, is cleared after the check and is never shown; the check neither saves nor selects the profile. Tests: `Sprint48SnmpProfileCheckerTests`, `Sprint48ProfileCheckHostingTests` (Modern), `Sprint48ProfileCheckWindowTests`, gallery `sprint48-profile-check`.
    - Owner review 2026-10-08: after a bulk action empties the selection the button that ran it keeps keyboard focus but is inactive — disabled look and disabled for UI Automation (`InertState` / `InertButton`, WPF cannot keep focus on an element with `IsEnabled=false`); it becomes an ordinary disabled button once focus leaves. The result «Принято: N» is shown next to «Выбрано: N» above the action buttons, not in the form message on the left. «Текущий адрес» and «Этап» are hidden once a run is no longer starting, running or stopping. The device Inspector aligns the long «Подтверждение» explanation to the left (`DiagnosticFieldRow.IsProse`). Tests: `Sprint48DiscoveryInboxActionsWindowTests`, `DiscoveryPanelShowsCurrentPhaseAndClearsItAfterAddress`, `UnconfirmedDeviceShowsMarkOnMapEquipmentAndInspector`; gallery re-shot.
    - Found while preparing the inbox: profile exclusion rules (`access_profile_exclusions`) were never read — Engine discovery always passed an empty exclusion list, and no UI creates rules. The inbox reads them and Engine skips them (`--exclude`); a rules editor is not in Sprint 48.
    - Fixed (§8, keyboard focus lost on refresh): the keyboard-only pass intermittently reported two event-strip buttons in «Настройки», dark theme, as unreachable by Tab, and once «focus lands on an invisible element: MainWindow». Cause: every 5 s refresh tick re-assigned `ShellEventList.ItemsSource` with a new array even when the events were unchanged, so the strip buttons were recreated and the focused one vanished (focus fell to the window); a walk spanning a tick saw old buttons, the reachability check new ones. `RenderShellEventList` now keeps the strip when the rows are the same objects and, when they change, keeps focus on the same event or moves it to «Все события». Deterministic reproduction: `Sprint48EventStripFocusTests` (fails before the fix). The `FitStackPanel` suspicion was wrong: it only sets tab navigation on hidden items.

- [ ] Sprint 49 — readable large-site map.
  - Operator outcome: on 55+ devices the map remains readable without mass overlap, and the operator can see incomplete evidence and manual-versus-observed conflicts.
  - Primary principle: show less instead of building a universal layout engine.
  - Focus neighborhood around the selected node, with explicit `Expand up`, `Expand down` and `Whole site` actions.
  - Any large-site placement/layer navigation must fit the ADR-083 shell and must not reintroduce a permanent section sidebar; its exact Sprint 49 surface is decided together with focus-neighborhood and semantic zoom.
  - Four semantic zoom levels: distant, medium, close and detailed; device/link/placement detail changes by level.
  - Show a topology-quality line with reasons for incomplete data.
  - Present ADR-079 manual-versus-observed conflicts with explicit actions.
  - Preserve manual positions; automatic anti-overlap applies only to new nodes.
  - Keep parallel links separate; highlight both ports when a link is focused; support shortest-path inspection and placement-fit interactions described in the Sprint plan.
  - Selection history remains an optional convenience candidate here if it is not completed earlier. Ctrl+K search and the collapsible inspector are mandatory Sprint 46 shell behavior under ADR-083.
  - Keyboard path for map objects (UI_DESIGN_RULES §8), moved from Sprint 46 by owner decision on 2026-10-07 (mockup-gap act K4): nodes, links and placement collapse buttons take keyboard focus with the shared focus ring, Enter selects, arrows move between neighbours, layout changes have a keyboard alternative. Today map selection by keyboard goes through Equipment, Ctrl+K and "Показать на карте".

  - Implementation decisions (from the rules, not owner questions):
    - Link focus is the link under the mouse pointer, otherwise the selected link: its label (the two ports) stays visible at any zoom and is emphasized, every other link and its label is dimmed (`NetLoom.Map.LinkFocusDimmedOpacity` = 0.3); nodes are not dimmed. This follows the reference mockup `netloom-v2-1-karta`, where the selected link carries one «port ↔ port» label.
  - Implementation record 2026-10-08:
    - Parallel links: links with different LinkKeys between one pair of devices are offset perpendicular to the pair axis (`ParallelLinkLayout`, `NetLoom.Map.ParallelLinkSpacing` = 14; the spread is capped at 3/4 of the node height). Lane order is stable (by link identity) and does not depend on which end is stored first; labels of one pair avoid each other. The PNG export uses the same offsets. A single link stays centre to centre. Tests: `Sprint49ParallelLinkLayoutTests`, `Sprint49ParallelLinksMapTests`, `Sprint49ParallelLinkExportTests`, gallery `sprint49-parallel-links`.
    - Field stand: it is built as a discovery run a week ago, so after Sprint 48 every device was «Не подтверждено»; the builder now marks discovered devices accepted, as an operator would have.
    - Topology quality line: under the monitoring notice above the map (Map section only, not Alerts; nothing in the window top row) «Топология неполная: N» with a neutral glyph (ADR-081: insufficient data is neutral) and an expandable list grouped by reason — links only observed (`Observed`) or inferred (`Inferred`), one-sided LLDP (Sprint 48 Г4) and interfaces without ifIndex on non-manual devices (manual devices have none by design); «Показать» selects the link or device. A complete topology shows no line. The field stand has an Observed link and one-sided LLDP but no interface without ifIndex (the Sprint 45 field case), so that reason is covered by unit tests only. Fixed in review: the neutral glyph used the disabled-text brush, 2.8:1 on the muted strip (§6), now secondary text. Tests: `Sprint49TopologyQualityProjectionTests`, `Sprint49TopologyQualityLineTests`, gallery `sprint49-quality`.
    - Manual versus observed conflicts (ADR-079): a port used by a manual link and by an automatic link (Confirmed/Observed/Inferred) whose other end is a different device or interface is a conflict (`TopologyConflictProjection`); an automatic link with the same two ends confirms the manual one. Both labels start with «⚠» in the warning brush and stay visible at any zoom; the quality line lists «Расхождения ручной и наблюдаемой схемы»; the link Inspector starts with «Расхождение со схемой» (manual and observed versions, source and last seen) and three actions: «Открыть основания» (selects the observed link, Evidence tab), «Оставить ручную» (stored decision, `Migration026TopologyConflictAcknowledgements`, the pair is no longer reported; nothing in the topology changes) and «Изменить ручную» (switches the map to Edit mode explicitly, ADR-080, and opens the manual topology editor on the manual link). Nothing is repaired automatically. Tests: `Sprint49TopologyConflictProjectionTests`, `Sprint49TopologyConflictTests`, `Sprint49TopologyConflictAcknowledgementTests` (Integration), gallery `sprint49-conflict`.
    - Open, НИЗКАЯ (§1, found in the Sprint 49 gallery, since Sprint 46): the Inspector headline glues the relative time into a sentence («✓ Передаёт трафик · данные {0}»), so a fresh link reads «данные Только что» with a capital letter mid-sentence. The fix is whole phrases for the seven `*· данные {0}` templates (resx values must not start with a lowercase letter, so the «now» word cannot be a lowercase fragment); not part of Sprint 49 scope.
    - Locations (M2): one frame with a tab caption at the top-left corner as in `netloom-v2-1-karta` (title, lock, neutral collapse button; the description moved to the tab tooltip; the fill opacity no longer dims the caption text; a separate frame border brush keeps 3:1). Saved manual geometry is never moved or re-saved without an operator action: overlapping saved frames and a saved child frame outside its parent are listed in the quality line («Наложенные размещения»), not repaired. This replaces the Sprint 38 behaviour that silently contained a saved child inside its parent and saved it (test renamed to `PersistedInvalidChildStaysWhereSavedAndIsReportedWithoutMovingParentAnchor`). Still moved, because they are not saved geometry or follow an operator action: new nodes and frames without saved positions (placed on a free spot, `MapFreePlacement`), and an item whose parent location the operator changed in this session (moved inside the new parent and saved). Unsaved sibling frames are separated bottom-up unless their subtree holds saved geometry. Tests: `Sprint49LocationOverlapProjectionTests`, `Sprint49MapFreePlacementTests`, `Sprint49LocationFrameTests`, Sprint 38 location tests, gallery `sprint49-locations`.
    - Open, СРЕДНЯЯ (§5, M2 remainder): on the field stand, which has no saved frame geometry, automatic frames of sibling locations still intersect, because the projector lays out nodes of different locations interleaved and each frame grows around its own nodes. Needs a location-aware automatic node layout; manual geometry is not affected.
    - Link focus: hover and selection share one presentation path (`ApplyLinkFocusPresentation`), applied without re-placing labels; operational focus dimming multiplies with link focus. Fixed on the way: the test fixture `CriticalLinkSnapshot` had no diagnostic for its link, so selecting it was silently dropped («selected object is gone»); the old drawing still thickened the line, which hid it. Tests: `Sprint49LinkFocusTests`, gallery `sprint49-link-focus`.
- [ ] Sprint 50 — network redundancy: rings and single points of failure.
  - Operator outcome: selecting a ring or a predicted single point of failure immediately explains the protection state and the topology impact in operator terms.
  - Ring view: selecting a ring dims unrelated topology and highlights every member link; show participants, STP root, blocked/alternate port, protection state and last topology change when trustworthy evidence exists.
  - Device/link impact: extend the existing link-level `PhysicalLinkFailureImpact` semantics to device-level predicted impact. For `Если связь/устройство пропадёт`, highlight devices predicted to be affected by the known physical topology and dim unrelated nodes; distinguish an available bypass path from a unique path/single point of failure. A bypass is valid only through links/ports that remain usable under the current known failure and STP state; never report `есть обходной путь` through the link being evaluated as failed or through a path currently blocked/unavailable.
  - Prediction and observation stay separate. Sprint 50 answers what the known topology predicts would be affected; it does not claim which devices are actually down right now.
  - Use existing protection semantics: Protected / Unprotected / Degraded / Unresolved / NotApplicable. Missing STP evidence is `Unresolved`, never automatically `Unprotected`.
  - Before implementation, verify whether `dot1dStpTopChanges` is actually collected and trustworthy enough for the “last topology change” field.
  - Ring history remains after the topology change journal, not in Sprint 50.
  - Rings closed through a pair of core switches, moved from Sprint 46 by owner decision on 2026-10-07 (field check `docs/sprint46-field-check.md` Г5): today «Кольцо без резерва» is evaluated only for a simple ring; a ring whose ends land on two different core switches is not analysed.

- [ ] Sprint 51 — polling policies and profile templates.
  - Operator outcome: polling behavior can be assigned per device or placement, including an explicit no-active-polling policy.
  - Templates are policy only and never ship credentials: secure SNMPv3, industrial v2c, one-time audit, scheduled topology, no active polling.
  - Policies may vary polling cadence for health versus LLDP/CDP/FDB/ARP/STP work.
  - Passwords and community strings are never embedded in templates.
  - Profile exclusion rules editor, by owner decision on 2026-10-08 (Sprint 48 report): rules (`access_profile_exclusions`) are applied by discovery since Sprint 48 (Engine `--exclude`, Excluded rows in the inbox), but no UI creates, edits or removes them.

### Parallel evidence gathering — not a Sprint

- Optical capability audit on existing hardware: MikroTik CSS106 confirms SFP identity but has not yet confirmed Rx/Tx/temperature DDM; MOXA PT-7728 confirms SNMP/LLDP but no DDM surface has yet been found; MOXA EDS-408A-SS-SC uses fixed optical ports; unmanaged optical/copper converters are not expected to expose their own SNMP sensors. Use a small offline/read-only audit utility from the development machine if deeper private-MIB inspection is needed.
- Promote optical degradation into a future committed sequence only after real hardware exposes trustworthy sensor values and transceiver/port identity that can support a non-misleading time series.
- MOXA Turbo Ring/Turbo Chain is explicitly not planned for the current site: it is disabled on all known MOXA devices there. Revisit industrial protection adapters only if a real deployment enables such protection or another evidenced need appears.

### Next candidates — not commitments

The evidence-first demonstration/sales candidate strategy is recorded in `docs/NETLOOM_WOW_FEATURES.md`. It does not change the committed Sprints 46–51 sequence. Candidate ordering remains subordinate to Sprint 45 `FRICTION_LOG.md` evidence, explicit user approval, and the existing priority rule.

#### Priority after Sprint 51

This ordering is the next-candidate priority only; it does not become a committed sequence until the user explicitly commits it.

After Sprint 47, keep `Что отвалилось сейчас` as a candidate observed-state surface: it must be based on current monitoring evidence and must not be merged with Sprint 50 failure-impact prediction.

1. **Device diagnostics** — step-by-step checks from the inspector following the evidence model: management address → SNMP → identity → interfaces → LLDP → FDB → ARP → STP → physical topology, with `Open evidence` on the failing step.
2. **Dependent-alert suppression** with one incident card and downstream symptoms marked as suppressed/affected upstream.
3. **Network state** in three groups: structural risks, confirmed active problems, insufficient data.
4. **Ports view**: primary table with fast filters; compact per-switch matrix as a secondary view.
5. **Merge discovered device with manual object** as an explicit operator action.
6. **Topology change journal → time machine**. Capture before/after atomically in the same topology mutation transaction; audit all mutation paths before implementation.
7. **Logical tags** over the physical hierarchy.
8. **Visio export** as simple data plus a template; direct `.vsdx` generation remains out of scope.
9. **Fullscreen operator mode**.
10. **Maintenance / alert suppression** with a required reason and expiry.
11. **Deterministic demo stand with seeded failures**, also used as regression evidence.
12. **Raw observation retention review**; the current 24-hour window may be insufficient for some repair/replay workflows.
13. **Operator notes on devices and physical links** — requires an explicit persistent storage/write-path decision and is therefore outside Sprint 46.
14. **Правка прямо на карте** — кандидат только после Sprint 51 по ADR-083 и макету `netloom-v2-3-pravka-na-karte`: видимые `+ Устройство`, `+ Связь`, `+ Размещение`; те же действия через правый щелчок и `Shift+F10`; связь протягивается мышью или выбирается портом в инспекторе; ручные элементы — пунктиром и со значком руки; свойства — в инспекторе. Место в будущей последовательности определяется отдельно по ADR-078.

#### Market-entry direction — ADR-078, not commitments

- German localization as a DACH entry requirement.
- MRP (IEC 62439-2) for European industrial networks.
- PROFINET DCP for discovery of devices without usable IP configuration.
- EtherNet/IP for the US industrial market.
- A permanent free tier around 10–15 devices as a go-to-market candidate.

#### Кандидаты из полевого evidence Sprint 45

- [ ] Подавление зависимых предупреждений: один инцидент при отказе аплинка, симптомы за ним подавлены и отмечены «затронуто выше по течению». До реализации зафиксировать, что «выше по течению» означает «ближе к точке опроса Engine». Использовать существующий `PhysicalGraphSafetyAnalyzer`, не смешивая событие и инцидент.

  ```
  КРИТИЧНО  Аплинк недоступен                              2 мин 14 с
            SW-CORE Gi1/0/24 ↔ SW-B4 Gi0/1
            Затронуто: 18 устройств · Корпус Б
            Подавлено вторичных предупреждений: 17
            Изменение состояния порта: 11:41:03
            [Показать влияние] [Диагностика] [Принять]
  ```

- [ ] Логические метки поверх физической иерархии: «АСУ ТП», «Видеонаблюдение», «Учёт», «Критично».
- [ ] Экспорт для Visio: простой формат данных плюс шаблон; прямую генерацию `.vsdx` не вводить без отдельной необходимости.
- [ ] Пересмотр окна хранения сырых наблюдений: 24 часа может быть недостаточно для ремонта из исходных BER-значений.
- [ ] Направление лицензирования: плоская цена за площадку как основной кандидат (рыночный сдвиг, отмеченный в исследовании сентября 2026); сравнивать прежде всего с локальными системами PRTG, OpManager и SolarWinds, поскольку закрытая сеть меняет конкурентный контекст.
- [ ] Вид портов: таблица интерфейсов как основной вид для сотен интерфейсов; быстрые фильтры поднят / опущен / administratively down / с ошибками / есть LLDP-сосед / нет соседа. Компактная матрица 24–48 портов — дополнительная сводка выбранного коммутатора во вкладке «Интерфейсы».
- [ ] Кнопка «Диагностика» в инспекторе: пошаговая проверка выбранного устройства `ping → SNMP → LLDP → FDB → ARP → STP → пересчёт топологии` с результатом каждого шага.
- [ ] Полноэкранный операторский режим: только карта и состояние без рабочих панелей.
- [ ] Обслуживание/подавление предупреждений с обязательной причиной и сроком, например «работы на подстанции до 16:00».
- [ ] История подключения MAC: где конечное устройство было подключено вчера и неделю назад; требует журнала изменений и идёт после него.
- [ ] Массовые действия и сохранённые виды карты — позже, при росте площадок.

Уже существующие кандидаты из `docs/NETLOOM_WOW_FEATURES.md` сохраняются: экран состояния сети в трёх группах, детерминированный демо-стенд, предпросмотр влияния физической связи, журнал изменений и read-only предпросмотр работ на L2-модели.
- [ ] Network state surface: present existing analysis in three operator-visible groups — structural risks, active confirmed problems, and insufficient data — without inventing missing evidence or adding the not-yet-implemented multi-MAC hidden-switch heuristic to the first version.
- [ ] Deterministic demonstration stand after the network-state surface: use controlled `snmpsim` fixtures, including a proven STP-state transition chain from `.snmprec` through observation/materialization/analyzer/alert transition, so the sales scenario also serves as behavioral regression evidence.
- [ ] Physical-link impact preview: highlight the portion of the known physical topology that becomes separated when a selected link is unavailable; do not present structural graph separation as guaranteed service/IP outage.
- [ ] Reversible topology change journal with bounded checkpoints, only after an audit proves all mutation paths needed for historical reconstruction. Capture before/after state atomically with the topology mutation; define historical-layout and evidence-history scope before implementation.
- [ ] Read-only L2 work preview: apply a sequence of planned removals/changes to a copy of the known physical graph and show structural impact conservatively. The first version does not predict STP, does not promise convergence time, and does not model L3 routing/ACL/NAT behavior.
- [ ] Portable deployment acceptance: verify Engine/Desktop/runtime/native SQLite/write paths and no-admin operation explicitly rather than treating a ZIP-on-USB package as automatically portable.
- [ ] Documentation-versus-reality comparison starting with structured CSV/device/address input; image/PDF/Visio recognition is a separate later problem.
- [ ] Link evidence panel: from a selected physical link, show the concrete discovery evidence, endpoints/ports, freshness and repeated-observation context already present in the model/read path.
- [ ] Optical degradation after hardware evidence: retain trustworthy transceiver sensor history only after the parallel audit confirms real Rx/Tx/temperature data plus stable port/transceiver identity; keep measured trends distinct from failure-date predictions and isolate vendor-private MIB support behind optional adapters.
- [ ] Industrial protection adapters only from real deployment evidence. Current-site MOXA Turbo Ring/Turbo Chain is disabled on all known devices, so no MOXA ring adapter is planned now; MRP or vendor-specific protection enters a future sequence only when actually enabled/needed.
- [ ] External operator validation: after the realistic stand is stable, run at least one usability/acceptance session with an engineer who did not build NetLoom; keep internal operator acceptance and external validation as distinct evidence.
- [ ] Persist operator monitoring preferences (target-address override and polling policy) across Desktop restarts after Sprint 40 acceptance establishes the final control surface; choose an application-configuration boundary without reclassifying manually entered addresses as observed topology metadata.
- [ ] Extend the existing Sprint 36 calm semantic motion to one-shot operational/degradation state transitions only when a state actually changes. Preserve `Normal` / `Reduced` / `Off`, avoid perpetual/decorative animation, and keep direct pan/zoom immediate.
- [ ] Real target-relay acceptance only when a real deployment SMTP configuration and an operator need exist.
- [ ] Dead-letter/escalation policy only if real relay/retry evidence shows a concrete terminal-failure or operator-response need.
- [ ] Routing by event/kind/severity only when an actual notification-routing need appears.
- [ ] Production configuration boundary and protected secrets for installed deployments.
- [ ] Open device from the selected-element panel through an external browser/SSH tool using the observed management address; do not build an embedded terminal without a real need.
- [ ] Ready-to-use alert presets with conservative defaults so first use does not require building a rule set from scratch.
- [ ] Device configuration backup with version history and line-by-line diff, only after a trustworthy device-specific configuration retrieval boundary is defined.
- [ ] Scheduled file reporting as a follow-up to Sprint 44 export when a real delivery/reporting workflow is requested.

### Не-цели

Сознательно не делаем в текущем направлении продукта:

- конструктор дашбордов;
- конструктор правил уровня Zabbix;
- полноценное управление конфигурациями;
- поиск первопричин с помощью ИИ;
- отрисовку передних панелей для сотен моделей оборудования;
- десятки алгоритмов раскладки графа;
- L3-топологию и моделирование маршрутизации;
- Числовые проценты достоверности связей.
- Numerical confidence percentages.
- Automatic map “correction” when observed topology conflicts with manual topology.
- Direct `.vsdx` generation.

### Roadmap — direction, not scheduled backlog commitment

- Runtime LTS migration together with first production release/deployment readiness.
- Offline/self-contained packaging, service install, upgrade/migration, WAL-safe backup/restore, manifests and diagnostics.
- HTTP API with localhost-by-default remote security boundary.
- Web client only after the API boundary is accepted and a real use case exists.
- Probable failure-boundary localization with structural blast radius kept separate from observed outage scope.
- Additional industrial protection protocols without false `Unprotected` conclusions from missing STP evidence; unsupported/insufficient evidence remains `Unresolved` / `UnsupportedProtectionEvidence`.
- Future Site/Probe identity if distributed monitoring becomes necessary.

The roadmap does not supersede the priority rule: recurring real friction beats speculative product work.
