# NetLoom Backlog

тот файл дополняет Sprint-план из PROJECT_STATE.md и не заменяет его.

риоритет определяется сначала собственной эксплуатацией NetLoom, затем коммерческой подготовкой.

## P0 — нужен для собственного рабочего NetLoom

- [x] Sprint 15 — materialized graph + manual/unmanaged topology.
- [ ] одключить реальный backend pipeline к MapSnapshot и WPF, чтобы карта показывала живую сеть.
- [ ] NetLoom.Simulator: replay сохранённых raw SNMP varbind snapshots через настоящие parsers.
- [ ] Monitoring Runtime.
- [ ] Scheduler.
- [ ] Health monitoring.
- [ ] Interface monitoring.
- [ ] STP/RSTP Collector.
- [ ] STP tree projection.
- [ ] Physical ring detection.
- [ ] Ring protection analyzer.
- [ ] добный поиск MAC/IP до конкретного switch/interface.
- [ ] инимальные alerts для реально полезных topology/ring failures.

## P0 — архитектурные gates

- [ ] о массовой записи метрик в monitoring ввести отдельную abstraction metric/time-series storage.
- [ ] е складывать высокочастотные временные ряды в topology/configuration SQLite без отдельного решения.
- [ ] огда NetLoom.Engine начнёт выполнять реальные polling/runtime задачи, добавить настоящий Linux runtime smoke test.
- [ ] Simulator должен уметь воспроизводить raw protocol snapshots без обхода production parsers.

## P1 — полезно после появления рабочей карты

- [ ] Topology snapshots / машина времени.
- [ ] Diff карты до/после инцидента.
- [ ] SFP/DDM optical health.
- [ ] SNMP trap ingestion.
- [ ] Syslog ingestion.
- [ ] кспорт схемы объекта в PNG/PDF.
- [ ] нвентаризация и экспорт CSV.
- [ ] ыявление вероятного unmanaged switch по нескольким MAC за портом.

## Product readiness — дёшево сейчас

- [x] Transport-neutral contracts.
- [x] тдельный modern Engine.
- [x] UI business logic отделена от topology resolver.
- [x] Encoding policy через .editorconfig.
- [x] осстановлены повреждённые повреждённые кодировкой-комментарии в ARP/FDB/correlation/resolver и добавлен UTF-8 audit.
- [x] UI localization infrastructure.
- [x] еестр third-party лицензий.
- [x] Friction log.
- [ ] роверка лицензии каждой новой зависимости до merge.
- [ ] роверка репозитория на реальные credentials / production identifiers.

## отом, когда собственный NetLoom реально используется

- [ ] нглийская локаль UI.
- [ ] еревод документации и сайта.
- [ ] Web UI.
- [ ] Multi-user / roles.
- [ ] REST API для внешних клиентов.
- [ ] Installer / auto-update.
- [ ] Licensing / editions.
- [ ] Billing / merchant of record.
- [ ] оммерческие тарифы.
- [ ] емецкая и другие локали по реальному спросу.

## равило приоритета

овторяющаяся реальная проблема из FRICTION_LOG имеет приоритет над speculative product feature.
