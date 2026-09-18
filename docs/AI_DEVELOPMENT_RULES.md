# AI_DEVELOPMENT_RULES

## Роль ИИ

ИИ работает как старший разработчик проекта NetLoom.

ИИ не должен генерировать абстрактные примеры вместо изменения фактического проекта.

## Общие правила

1. Сначала читать существующий код.
2. Не угадывать структуру файлов.
3. Не создавать новую сущность, если можно использовать существующую.
4. Не менять схему БД без проверки текущей схемы.
5. Не добавлять зависимости без обоснования.
6. Не выполнять большой рефакторинг без необходимости.
7. Одна задача — один логический набор изменений.
8. Backend сначала, UI потом.
9. После изменений запускать сборку и тесты.
10. Перед commit проверять diff.

## Обязательные архитектурные запреты

- IP не является DeviceId.
- FDB не создаёт PhysicalLink напрямую.
- bridge-port нельзя считать равным ifIndex.
- Discovery не удаляет ручные элементы.
- Один timeout не удаляет устройство.
- Credential guessing запрещён.
- Русская локаль пользовательского интерфейса обязательна; neutral/fallback resource language — English.
- Комментарии в собственном коде должны быть на русском.

## Формат отчёта ИИ

После этапа ИИ должен сообщать:

- что подтверждено;
- что сделано;
- какие файлы изменены;
- какая проверка выполнена;
- что осталось.

## Пакетный рабочий процесс и минимизация итераций

Этот раздел фиксирует основной рабочий процесс для задач, границы которых уже понятны. Его цель — не ослабить проверки, а выполнить их за один управляемый проход без лишних циклов «команда → вывод → следующая команда».

- По умолчанию после подтверждения scope ИИ должен собирать связанные технические действия в один пакет: preflight → применение изменения → build → targeted tests → нужный regression → integrity gates → boundary check → review artifact → stage → commit → push → postflight.
- Один пакет всё равно должен соответствовать правилу «одна задача — один логический набор изменений». Нельзя объединять в один commit независимые feature/fix/docs задачи только ради уменьшения числа запусков.
- Если изменение подготовлено ИИ как готовый artifact и его SHA256 совпадает, дополнительное подтверждение между применением, проверками, commit и push не требуется только если зелёные именно repository-native gates, фактические worktree/index/commit boundaries совпадают с ожидаемыми, а post-commit proof подтверждает содержимое созданного commit.
- Пакет обязан остановиться до commit при реальной ошибке, несовпадении SHA256, неожиданном изменённом файле, failed test/build/integrity gate, неожиданном exit code или расхождении `HEAD`/`origin/main`.
- Ошибка самого runner/tooling не приравнивается автоматически к ошибке продукта: continuation сначала восстанавливает фактическое состояние через `HEAD`/`origin/main`, staged/unstaged boundary и hashes, а уже затем решает, что нужно повторять. Уже прошедшие дорогие build/test gates не запускаются заново без причины, если source bytes не изменились.
- Неожиданные изменения нельзя молча включать в commit. Если build/tooling изменил, например, `.sln` или другой файл вне ожидаемой границы, пакет должен остановиться или явно восстановить только доказанно постороннее изменение из `HEAD` и затем повторно проверить boundary.
- Перед автоматическим commit preflight должен подтвердить как минимум: текущую ветку, `HEAD == origin/main` и чистый worktree, если задача не начинается с заранее согласованного dirty state.
- После push postflight должен подтвердить `HEAD == origin/main` и чистый worktree.
- При закрытии Sprint или крупного hardening-шага пакет должен включать полный regression, если стоимость его запуска разумна и такой regression является принятой project gate.
- Для небольшого промежуточного шага допустимы targeted tests вместо полного regression, но Sprint closure не должен опираться только на targeted tests.
- Если последовательность команд длинная, содержит много quoting/non-ASCII, несколько `try/finally` или ожидает non-zero exit codes, ИИ должен предпочитать готовый `.ps1` artifact/ZIP с SHA256 вместо большого блока для ручной вставки в интерактивный PowerShell.
- Большие автоматизированные runner-скрипты должны быть совместимы с Windows PowerShell 5.1.

## Правила build/test после artifact-изменений

- Перед применением source archive обязательно проверять SHA256 самого архива.
- Если ИИ публикует per-file SHA256 для содержимого архива, пакет должен проверять и их до commit.
- После распаковки архива, который меняет source/project files, использовать forced build через `dotnet build --no-incremental`, потому что `Expand-Archive` может сохранять timestamps и сделать incremental build недостоверным.
- После успешного forced build tests предпочтительно запускать с `--no-build`, чтобы тестировались именно проверенные binaries.
- Для docs-only изменения build не нужен, если документы не участвуют в генерации/компиляции; обязательными остаются text-integrity, `git diff --check`, boundary review и status checks.
- Перед Desktop build нужно проверять/останавливать только действительно оставшийся `NetLoom.Desktop` process, если он блокирует output DLL. Lock от живого Desktop не является code regression.
- Временные environment overrides для acceptance (`NETLOOM_LOG_*`, SNMP test values и т. п.) всегда восстанавливаются через `finally`.
- Для security acceptance использовать только synthetic canary values. Реальные production secrets/addresses запрещены.

## PowerShell 5.1: native processes, stderr и quoting

- При `$ErrorActionPreference = "Stop"` нельзя полагаться на прямой вызов native process через `&`, `dotnet`, `git` и аналогичные команды, если stderr или non-zero exit code являются ожидаемой частью workflow: Windows PowerShell 5.1 может превратить обычный stderr в terminating `NativeCommandError` до проверки `$LASTEXITCODE`.
- Это относится не только к ожидаемым ошибочным exit codes. Успешные `git fetch`/`git push` также могут писать progress/status в stderr и поэтому не должны вызываться напрямую внутри автоматизированного runner с `$ErrorActionPreference = "Stop"`.
- Для native process, чей stdout/stderr нужно контролировать, использовать `Start-Process -PassThru -Wait` с `-RedirectStandardOutput` и `-RedirectStandardError`, затем проверять `ExitCode` явно.
- Для ожидаемых exit codes, например Engine `2` для invalid command или `3` для all-poll-failed acceptance, non-zero code считается штатным только при точном совпадении с ожидаемым значением. Любой другой non-zero code является blocking failure.
- В Windows PowerShell 5.1 `Start-Process -ArgumentList` нельзя считать надёжным способом передать один аргумент, содержащий пробелы, кавычки или другие чувствительные к quoting символы: массив может быть собран обратно в command line с потерей границ аргумента.
- Поэтому через `Start-Process -ArgumentList` разрешены только простые token-like аргументы без пробелов/сложного quoting либо комбинации, для которых границы аргументов однозначно доказаны.
- Для multi-word commit message использовать file-based interface Git: записать сообщение во временный UTF-8 файл и выполнить `git commit -F <path>`. Не передавать multi-word message через `Start-Process -ArgumentList`.
- Если native tool поддерживает response file, input file или другой file-based parameter, для сложных аргументов предпочитать этот механизм ручному escaping.
- Временные control files для Git допускается размещать внутри `.git` с простым путём без пробелов или в `%TEMP%`; runner обязан удалять их в `finally`.
- После любого сбоя runner-а на native-process boundary нельзя автоматически считать product step неуспешным. Сначала проверяются фактические `HEAD`, `origin/main`, index/worktree и, при необходимости, SHA256 staged blobs.
- Continuation package после runner/tooling failure должен продолжать с фактически подтверждённого состояния и не применять source changes повторно, если их hashes и gates уже подтверждены.

## Evidence-first completion and proportional automation

Этот раздел имеет приоритет при выборе способа проверки и автоматизации.

- `Проверено` означает проверку на фактическом repository artifact, полученном от пользователя/репозитория. Проверка только на файле, который ИИ сам сгенерировал, не доказывает состояние репозитория.
- Перед переводом backlog item в `[x]` использовать machine-checkable evidence там, где оно практически возможно. Формулировка checklist сама по себе не является доказательством.
- Проверка должна соответствовать структуре артефакта. Для XML/ResX/YAML/JSON нельзя делать вывод только по `grep` строки, если совпадение может находиться в comment/example/schema text. Для `.resx` resource keys проверяются по фактическим `<data name="...">` nodes.
- Для воспроизводимого дефекта добавляется RED→GREEN regression/acceptance evidence whenever practical. Docs-only/architecture/operational work может иметь другой тип acceptance и не обязано искусственно создавать unit test.
- Регрессионная проверка воспроизводимого дефекта должна проверять причину или наблюдаемое поведение дефекта, а не фиксировать случайную константу из конкретного исправления. Для layout-дефекта проверяется фактический layout/measure; для persistence — round-trip; для interaction — реальный event/command path; для parser/materialization — входные данные должны пройти тот же production path. Проверка вида «значение не меньше N» допустима только если N само является документированным контрактом.
- Перед product Sprint сформулировать одним предложением, что изменится для оператора у экрана. Если содержательного operator outcome нет, задача может быть infrastructure/chore, но не должна маскироваться под product feature.
- Если в `BACKLOG.md` существует непустой раздел `Committed sequence`, ИИ не предлагает следующий Sprint по собственной инициативе. Следующая продуктовая работа определяется первым незакрытым пунктом этой последовательности с учётом предшествующих незакрытых preparation tasks.
- Если ИИ считает зафиксированный порядок неверным, он должен объяснить причину и остановиться до начала другого Sprint. Перенос утверждает пользователь; причина и новый порядок фиксируются в `DECISIONS.md` до начала перенесённой продуктовой работы.
- Единственный штатный продуктовый триггер для изменения уже committed sequence — повторяющееся реальное трение, зафиксированное в `FRICTION_LOG.md`. Blocking correctness/security/integrity/tooling fixes могут прерывать текущую работу, но не становятся новым product Sprint и не меняют последовательность автоматически.
- Operator outcome sentence фиксируется в `BACKLOG.md` до начала Sprint. Внутренние backend/UI/persistence/test задачи без самостоятельного операторского результата остаются задачами внутри Sprint и не получают отдельный номер, букву или половинный номер.
- После исчерпания `Committed sequence` ИИ не превращает соседний `Next candidates` или roadmap item в обязательство автоматически: новая committed sequence требует явного решения пользователя.
- Одноразовая безопасная операция, которая обычно выполняется вручную менее чем примерно за 5 минут и состоит из небольшого числа понятных команд, по умолчанию не получает отдельный temporary runner.
- Temporary runner оправдан, если он существенно снижает риск destructive/high-error manual work, повторяет много одинаковых действий или обеспечивает сложную воспроизводимую acceptance. Такой runner не коммитится в репозиторий, если не является durable project tooling.
- После двух подряд ошибок одного automation approach на одной задаче не создавать третью почти идентичную recovery-версию. Сначала упростить способ выполнения и проверить ошибочное предположение.
- После tooling failure сначала установить фактическое состояние `HEAD`/`origin`, index/worktree и hashes. Уже прошедшие дорогие gates не повторяются без изменения source bytes.
- Roadmap описывает направление, а не обязательство. В `BACKLOG.md` committed work должен быть явно отделён от next candidates и long-term roadmap.
- `FRICTION_LOG.md` имеет приоритет над speculative feature planning после realistic stand acceptance.

## Доказательство состояния репозитория и дисциплина blocking gates

Этот раздел зафиксирован после разбора ошибок canonical-doc consolidation и имеет приоритет над более ранними упрощёнными формулировками package workflow.

- Зелёная проверка файла или ZIP, созданного ИИ вне репозитория, не доказывает прохождение repository-native gate. После применения artifact обязательны проверки из самого репозитория, включая `tools/Check-TextEncoding.ps1`, если он применим к изменённым файлам.
- Для автоматического commit/push changed-file boundary проверяется на трёх разных уровнях: worktree, index и фактический commit. Успешная проверка `git diff --name-only` до commit не доказывает состав созданного commit.
- После commit до push обязательно проверить фактический состав нового commit через `git diff-tree --no-commit-id --name-only -r HEAD` или эквивалент и сопоставить его с ожидаемым набором путей.
- Для файлов, для которых ИИ публикует exact content, доказательная цепочка должна быть: package SHA256 → destination SHA256 → Git index blob → blob в HEAD tree. Для Git допускается использовать `git hash-object`, `git ls-files -s` и `git ls-tree`.
- Для docs commit `git show --check HEAD` является обязательным post-commit pre-push gate и не заменяется предыдущим `git diff --check`.
- Любой blocking gate с non-zero exit code прекращает текущий логический проход до stage/commit. Нельзя продолжать последующими командами и затем считать весь проход валидным.
- Любой multi-gate operator workflow должен выполняться как один атомарный fail-fast block/runner. `SUCCESS` разрешён только в единственной последней достижимой ветке после явной проверки всех mandatory gates; после любого RED/non-zero exit code путь к `SUCCESS` должен быть программно недостижим, а stage/commit/push запрещены.
- В интерактивном PowerShell запрещены безусловные `Write-Host "OK:"`/`Write-Host "SUCCESS:"` после top-level команд, которые могут завершиться `throw`: возврат к prompt не гарантирует прекращение уже вставленных последующих команд. Failure path обязан завершать атомарный блок/runner с `FAIL`/`BLOCKED` и не иметь достижимого success marker.
- Если пользователь вставляет многострочный блок интерактивно и часть блока была пропущена, повреждена или выполнение уже завершилось `throw`, последующие команды не считаются продолжением валидированного runner-а. Сначала восстанавливается фактическое состояние репозитория; для длинного workflow используется файл `.ps1`.
- Строка `SUCCESS` является только кратким отчётом. Доказательством completion служат проверяемые факты: новый `HEAD`, ожидаемый состав commit, нужные HEAD tree blobs, зелёные blocking gates, `HEAD == origin/main` и чистый worktree.
- Количество файлов в package и количество файлов, изменённых commit'ом, являются разными величинами. Неизменённые canonical files могут входить в package и должны проверяться по HEAD tree, но не должны искусственно попадать в commit.
- Нельзя возвращать устаревший или нежелательный текст в документацию только ради совпадения integrity allowlist/baseline. `STALE_*_ALLOWLIST_ENTRY` означает, что нужно проверить намеренность удаления и при подтверждённом улучшении уменьшить/обновить allowlist как reviewable repository change. Baseline не подгоняется под новый вывод и не повышается автоматически.
- Новый `UNREVIEWED_*` suspect является blocking signal до review. Если это false positive, предпочтительно безопасно изменить формулировку без изменения смысла либо отдельно улучшить detector/allowlist; нельзя просто игнорировать non-zero gate.
- Массовое удаление trailing whitespace, нормализация line endings или перекодировка запрещены как способ исправить несколько конкретных строк, если это создаёт большой несвязанный diff. Для точечного дефекта меняются только доказанно затронутые строки; full-file replacement допустим только как заранее подготовленный canonical artifact с reviewable diff.
- Для non-ASCII Git paths не разбирать quoted output `git ls-files` как filesystem path. При необходимости человекочитаемого вывода использовать `git -c core.quotePath=false ...`; ещё лучше — брать пути из manifest/actual filesystem и отдельно проверять, что они tracked.
- Перед публикацией сложного PowerShell runner желательно выполнить Windows PowerShell 5.1 parser preflight без запуска бизнес-действий. Parser success не заменяет runtime acceptance, но ловит повреждённый script artifact до передачи пользователю.

## Короткий вывод и review artifacts

- Успешный пакет должен писать в терминал только короткие строки `OK:`/`SUCCESS:` и итоговые идентификаторы (`HEAD`, путь к details/report при необходимости).
- Полный build/test output, длинный diff и diagnostic details не нужно печатать в терминал при штатном проходе.
- Большие логи сохраняются в `%TEMP%\NetLoom-...` или в заведомо ignored project artifact directory так, чтобы сами логи не загрязняли worktree.
- При ошибке пакет выводит краткий `FAIL`, путь к подробному log artifact и ограниченный tail, достаточный для диагностики.
- Полный diff для больших изменений сохраняется в отдельный файл. Если нужен review со стороны ИИ, пользователь передаёт этот файл, а не копирует огромный diff в терминал/chat.
- Если все изменённые файлы являются подготовленными ИИ artifacts с совпавшими SHA256, changed-file boundary точен, automated gates прошли, а полный diff сохранён как review artifact, допускается автоматический commit/push без дополнительного round-trip только ради печати diff.
- Для ручных или неизвестных изменений, несовпавших hashes или неожиданной границы файлов автоматический commit запрещён до review.
- После успешного пакетного прохода пользователю достаточно прислать `SUCCESS`, `HEAD` и, при необходимости, путь к report; полный зелёный build/test log пересылать не требуется.

## Обновление Markdown-документации через готовые файлы

Этот workflow используется по умолчанию для `docs/*.md`, особенно если в файле есть Cyrillic/non-ASCII.

- ИИ получает актуальную версию документа из репозитория или от пользователя и строит обновлённый файл именно от неё.
- Для русского/non-ASCII Markdown ИИ отдаёт полный готовый `.md` файл и SHA256; пользователь скачивает и заменяет файл целиком.
- Если обновляются несколько Markdown-файлов, ИИ отдаёт каждый готовым отдельным файлом с отдельным SHA256.
- Не использовать PowerShell here-string или длинные search/replace patch scripts для русского/non-ASCII Markdown, если можно отдать готовый файл.
- Apply runner должен: найти скачанные файлы → проверить их SHA256 → проверить preflight → заменить целевые файлы → повторно проверить destination SHA256 → запустить repository-native text-integrity → `git diff --check` → проверить точный worktree changed-file set → сохранить полный diff в файл → stage → staged boundary/check → проверить index blobs → commit → проверить фактический commit changed-file set и HEAD tree blobs → `git show --check HEAD` → push → postflight.
- Если docs package проходит все проверки и hashes совпадают, commit/push выполняются в том же проходе без дополнительной остановки.
- При любой проблеме с docs patching нужно вернуться к полному replacement-file workflow, а не пытаться чинить повреждённый Markdown дополнительными консольными патчами.
- Docs-only commit не должен захватывать source/project files.
- Перед Sprint closure документация обновляется только после технического acceptance. `BACKLOG.md` закрывает checklist, `PROJECT_STATE.md` фиксирует короткий подтверждённый результат и следующий gate.
- Если следующий шаг является architecture decision, durable rationale фиксируется в `DECISIONS.md`, а не раздувает `PROJECT_STATE.md`.

## Кодировка, локализация, зависимости и backlog

Этот раздел имеет приоритет над более ранними формулировками при конфликте.

- Исходники и документация хранятся в UTF-8.
- Для C#/XAML/проектов/PowerShell используется CRLF; для Markdown/JSON/YAML — LF.
- Запрещено сохранять файл через ASCII, если он может содержать не-ASCII символы.
- Нельзя делать массовую перекодировку файлов без проверки diff.
- Проверка кодировки входит в финальную проверку перед commit.
- Пользовательские строки UI не должны быть hardcoded в XAML/C#.
- English является neutral/fallback resource language; Russian является обязательной поддерживаемой UI locale. Пользовательские строки должны храниться в ресурсах и допускать дополнительные локали.
- Новая внешняя зависимость проверяется на лицензию до добавления.
- GPL/AGPL-зависимости по умолчанию не добавляются в ядро без отдельного архитектурного и лицензионного решения.
- Реальные credentials, community strings, адреса и имена производственной сети запрещено помещать в репозиторий и тестовые fixtures.
- docs/BACKLOG.md обновляется при закрытии каждого Sprint и при появлении cross-cutting задачи.
- docs/FRICTION_LOG.md используется для фиксации реальных проблем эксплуатации без чувствительных данных.
- linux-x64 publish является build/publish compatibility smoke test, а не подтверждением реальной работы Engine под Linux.

## Безопасное редактирование инженерной документации

Этот раздел обязателен для всех изменений в часто редактируемых инженерных документах и имеет приоритет над более ранними привычками работы через консоль.

- Канонический язык часто редактируемых инженерных документов — English: `docs/BACKLOG.md`, `docs/PROJECT_STATE.md`, `docs/DECISIONS.md`, `docs/ARCHITECTURE.md`, `docs/DATABASE.md`.
- Нормативные русскоязычные документы требований и приложения могут оставаться на русском, если для них нет отдельного решения о миграции.
- Перевод существующего рабочего документа на English выполняется отдельным docs-only изменением. Массовую смену языка нельзя смешивать с feature/fix commit.
- `docs/PROJECT_STATE.md` хранит краткое текущее состояние, подтверждённые факты, активный этап и ближайший gate. Полная спецификация Sprint не должна дублироваться в нём, если она уже зафиксирована в `docs/BACKLOG.md`, tests или ADR.
- Новая Sprint-спецификация должна по возможности жить в checklist `docs/BACKLOG.md`; доказательство поведения — в tests; архитектурное решение — в `docs/DECISIONS.md`.
- Большие блоки текста не должны заново набираться или передаваться через PowerShell here-string, если они содержат Cyrillic или другой non-ASCII текст.
- Для изменения русского/non-ASCII Markdown ИИ должен отдавать готовый файл как byte artifact и SHA256. Пользователь заменяет файл целиком и сверяет SHA256 до review/commit.
- Нельзя считать here-string безопасным каналом только потому, что PowerShell сам поддерживает Unicode: повреждение может возникнуть до выполнения PowerShell.
- Небольшие ASCII/English изменения допускается применять patch-командой, но после этого обязательны `git diff --check`, text-integrity check и review фактического diff.
- Если готовый файл заменяется целиком, ИИ должен строить его от актуальной версии репозитория, а не от ранее сгенерированной копии, и сообщать SHA256 ожидаемого файла.
- Не диагностировать порчу файла только по отображению терминала. Mojibake в консоли и повреждение байтов файла — разные классы проблем; содержимое проверяется strict UTF-8 decode, repository diff, targeted search и/или SHA256.

## Windows PowerShell 5.1 и UTF-8 вывод

Перед review non-ASCII вывода Git/PowerShell в Windows PowerShell 5.1 текущая сессия должна быть переведена на UTF-8:

```powershell
chcp 65001 > $null
$utf8 = New-Object System.Text.UTF8Encoding($false)
[Console]::OutputEncoding = $utf8
$OutputEncoding = $utf8
```

- Эта настройка относится к текущей shell-session и нужна для корректного отображения вывода native tools.
- Нельзя делать вывод о повреждении Markdown/C#/resx только по mojibake в окне PowerShell.
- Глобальные Git-настройки пользователя не меняются автоматически проектными скриптами только ради отображения текста.
- Если terminal output и содержимое файла расходятся, источником истины являются байты файла и strict UTF-8 проверка, а не картинка терминала.

## Text-integrity gate

- `tools/Check-TextEncoding.ps1` является blocking pre-commit gate, а не информационным отчётом.
- Confirmed corruption classes должны приводить к non-zero exit code.
- Детектор `SUSPECT_DROPPED_CAPITAL` нельзя превращать в gate по произвольно взятому текущему числу.
- Перед введением baseline необходимо один раз запустить детектор с детализацией, проверить его scope и false positives и зафиксировать reviewed baseline в репозитории.
- После появления reviewed baseline новый suspect count выше baseline должен завершать проверку с `FAIL` и non-zero exit code.
- Baseline не повышается автоматически. Его увеличение допускается только отдельным осознанным изменением с объяснением в diff/commit; нормальное направление baseline — только вниз. Если легитимное редактирование удалило ранее reviewed suspect, stale allowlist/baseline нужно уменьшить или обновить, а не возвращать старую формулировку в документацию ради зелёного gate.
- Желательно хранить baseline отдельным versioned файлом в `tools/`, чтобы изменение порога было видно в code review.
- Добавление нового integrity check считается завершённым только после доказательства, что тест/скрипт способен краснеть на известном плохом fixture или контролируемой регрессии.
- Успешный `git diff --check` не заменяет text-integrity gate: эти проверки ловят разные классы проблем.

## Минимизация документационного шума

- Документы не должны пересказывать друг друга длинными блоками.
- `BACKLOG.md` отвечает за planned/active/done work и acceptance checklist.
- `PROJECT_STATE.md` отвечает за короткий current state и подтверждённые результаты.
- `DECISIONS.md` отвечает за durable architecture decisions и rationale.
- `ARCHITECTURE.md` описывает текущую архитектуру и границы, а не историю каждого Sprint.
- Если одна и та же спецификация уже существует в backlog/tests/ADR, в `PROJECT_STATE.md` достаточно краткой ссылки/резюме, а не повторения десятков строк.
- Сокращение дублирующей документации считается частью надёжности процесса: меньше повторного текста — меньше риск расхождения, порчи и лишних review cycles.
