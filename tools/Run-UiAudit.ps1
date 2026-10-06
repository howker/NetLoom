# Builds NetLoom, runs the Sprint 46 shell, gallery and live UI audit tests,
# and writes the full console output to artifacts\ui-audit-run\run.txt
# (UTF-8) so the result can be read without copying the terminal.
# Screenshots and findings stay in artifacts\ui-audit (written by the audit test).
param(
    [string]$Filter = "FullyQualifiedName~Sprint46ShellFoundationTests|FullyQualifiedName~Sprint46UiStateGalleryTests|TestCategory=LiveUiAudit"
)

# dotnet writes UTF-8; decode it as UTF-8 so Russian output is readable in the log.
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

$logDir = Join-Path $root "artifacts\ui-audit-run"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$log = Join-Path $logDir "run.txt"
Set-Content -Path $log -Value ("=== NetLoom UI audit run " + (Get-Date -Format s)) -Encoding UTF8

function Invoke-Logged([string]$title, [scriptblock]$command) {
    Add-Content -Path $log -Value ("=== " + $title) -Encoding UTF8
    & $command 2>&1 | ForEach-Object {
        $line = "$_"
        Write-Host $line
        Add-Content -Path $log -Value $line -Encoding UTF8
    }
    return $LASTEXITCODE
}

$buildExit = Invoke-Logged "build" { dotnet build NetLoom.sln -c Debug }
$testExit = -1
if ($buildExit -eq 0) {
    $testExit = Invoke-Logged "test" {
        dotnet test tests\NetLoom.Tests.Unit\NetLoom.Tests.Unit.csproj -c Debug --no-build --filter $Filter
    }
}

Add-Content -Path $log -Value ("BUILD_EXIT=" + $buildExit) -Encoding UTF8
Add-Content -Path $log -Value ("TEST_EXIT=" + $testExit) -Encoding UTF8
Write-Host ""
Write-Host ("BUILD_EXIT=" + $buildExit + "  TEST_EXIT=" + $testExit)
Write-Host ("Log: " + $log)
