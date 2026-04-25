[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$platform = "x64"
$targetFramework = "net8.0-windows10.0.17763.0"
$supervisionRoot = Join-Path $repoRoot "src\bin\$platform\$Configuration\$targetFramework\assets\supervision"
$currentRunPointerFile = Join-Path $supervisionRoot "current_run.txt"

if (-not (Test-Path $currentRunPointerFile)) {
    Write-Output "No active watcher run found."
    exit 0
}

$runDir = (Get-Content -Path $currentRunPointerFile -Raw).Trim()
if ([string]::IsNullOrWhiteSpace($runDir) -or -not (Test-Path $runDir)) {
    Write-Output "Watcher pointer exists but run directory is missing: $runDir"
    exit 1
}

$stopRequestFile = Join-Path $runDir "stop.requested"
$runnerPidFile = Join-Path $runDir "runner.pid"

New-Item -ItemType File -Path $stopRequestFile -Force | Out-Null
Write-Output "Stop request written: $stopRequestFile"

if (Test-Path $runnerPidFile) {
    $runnerPid = (Get-Content -Path $runnerPidFile -Raw).Trim()
    if (-not [string]::IsNullOrWhiteSpace($runnerPid)) {
        Write-Output "Runner PID: $runnerPid"
    }
}

Write-Output "Watcher should stop the runner within a few seconds."
