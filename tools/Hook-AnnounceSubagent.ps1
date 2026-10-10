# Хук PreToolUse для Claude Code: перед каждым вызовом субагента показывает владельцу, кто будет выполнять работу.
# Для Codex дополнительно показывает модель и уровень рассуждений из ~/.codex/config.toml, чтобы дорогая модель не включилась незаметно.

$ErrorActionPreference = "SilentlyContinue"
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$inputEncoding = New-Object System.Text.UTF8Encoding($false)
$reader = New-Object System.IO.StreamReader([Console]::OpenStandardInput(), $inputEncoding)
$raw = $reader.ReadToEnd()

$agent = ""
try
{
    $data = $raw | ConvertFrom-Json
    $agent = [string]$data.tool_input.subagent_type
}
catch
{
    exit 0
}

if ([string]::IsNullOrWhiteSpace($agent))
{
    $agent = "general-purpose"
}

if ($agent -like "*codex*")
{
    $model = "?"
    $effort = "?"
    $config = Join-Path $env:USERPROFILE ".codex\config.toml"
    if (Test-Path -LiteralPath $config)
    {
        foreach ($line in [IO.File]::ReadAllLines($config))
        {
            if ($line -match '^model = "(.*)"') { $model = $Matches[1] }
            if ($line -match '^model_reasoning_effort = "(.*)"') { $effort = $Matches[1] }
        }
    }
    $text = "Исполнитель: Codex ($model, $effort) — лимит ChatGPT"
    if ($model -ne "gpt-6-sol" -or $effort -ne "medium")
    {
        $text = $text + ". Внимание: ожидается gpt-6-sol / medium"
    }
}
elseif ($agent -eq "editor")
{
    $text = "Исполнитель: editor (Claude Sonnet) — расходует лимит Claude, Codex не используется"
}
else
{
    $text = "Субагент: $agent (лимит Claude)"
}

# Та же строка пишется в журнал artifacts/autopilot/executor-log.txt: по нему видно, кто делал каждую правку.
$root = $env:CLAUDE_PROJECT_DIR
if ([string]::IsNullOrWhiteSpace($root)) { $root = Split-Path -Parent $PSScriptRoot }
$logDir = Join-Path $root "artifacts\autopilot"
if (-not (Test-Path -LiteralPath $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }
$stamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
[IO.File]::AppendAllText((Join-Path $logDir "executor-log.txt"), "$stamp  $text" + [Environment]::NewLine, (New-Object System.Text.UTF8Encoding($false)))

$payload = @{ systemMessage = $text } | ConvertTo-Json -Compress
[Console]::Out.Write($payload)
exit 0
