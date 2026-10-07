# Хук Stop для Claude Code: проверка кодировки перед завершением хода агента.
# Если tools/Check-TextEncoding.ps1 нашёл ошибки, хук возвращает код 2 и агент продолжает работу, чтобы их исправить.
# Один и тот же набор ошибок блокирует завершение только один раз, чтобы сессия не зациклилась.

$ErrorActionPreference = "Continue"

$root = $env:CLAUDE_PROJECT_DIR
if ([string]::IsNullOrWhiteSpace($root))
{
    $root = Split-Path -Parent $PSScriptRoot
}

# Без изменений в рабочей копии проверять нечего.
$changes = & git -C $root status --porcelain 2>$null
if (-not $changes)
{
    exit 0
}

$checkScript = Join-Path $root "tools\Check-TextEncoding.ps1"
$output = & powershell -NoProfile -ExecutionPolicy Bypass -File $checkScript -Root $root 2>&1 | Out-String
$checkExit = $LASTEXITCODE
if ($checkExit -eq 0)
{
    exit 0
}

# Тот же набор ошибок агент уже видел: повторно завершение не блокируем.
$bytes = [Text.Encoding]::UTF8.GetBytes($output)
$stream = New-Object IO.MemoryStream (, $bytes)
$hash = (Get-FileHash -InputStream $stream -Algorithm SHA256).Hash
$stateFile = Join-Path $env:TEMP "netloom-encoding-hook.txt"
$previous = ""
if (Test-Path -LiteralPath $stateFile)
{
    $previous = (Get-Content -LiteralPath $stateFile -Raw).Trim()
}
if ($previous -eq $hash)
{
    exit 0
}
Set-Content -LiteralPath $stateFile -Value $hash

[Console]::Error.WriteLine("Check-TextEncoding failed (exit $checkExit). Fix these issues before finishing the turn:")
[Console]::Error.WriteLine($output)
exit 2
