[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    # 跳过 build，直接拉起 exe（已知最新时用）
    [switch]$SkipBuild
)

# 一键启动 SleepRunner Race Console
# - build x64 平台（与 watch-race.ps1 同源，资源/模板路径一致）
# - 启动 SleepRunner.exe（无参数 → 自动弹 RaceMainWindow）

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [Console]::OutputEncoding

$script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$script:Platform = "x64"
$script:TargetFramework = "net8.0-windows10.0.17763.0"
$script:OutputDir = Join-Path $script:RepoRoot "src\bin\$script:Platform\$Configuration\$script:TargetFramework"
$script:Exe = Join-Path $script:OutputDir "SleepRunner.exe"
$script:Csproj = Join-Path $script:RepoRoot "src\SleepRunner.csproj"

function Write-Info($msg) {
    $timestamp = Get-Date -Format "HH:mm:ss"
    Write-Host "[$timestamp] [run-ui] $msg" -ForegroundColor Cyan
}

function Write-Warn($msg) {
    $timestamp = Get-Date -Format "HH:mm:ss"
    Write-Host "[$timestamp] [run-ui] $msg" -ForegroundColor Yellow
}

function Stop-RunningInstances {
    # 检查并提示已运行的 SleepRunner 实例（占用 exe 会让 build 失败）
    $procs = Get-Process -Name "SleepRunner" -ErrorAction SilentlyContinue
    if (-not $procs) { return }

    Write-Warn ("Detected {0} running SleepRunner instance(s):" -f $procs.Count)
    foreach ($p in $procs) {
        Write-Warn ("  PID={0}  StartTime={1}" -f $p.Id, $p.StartTime)
    }

    $answer = Read-Host "Stop them now? (y/N)"
    if ($answer -match '^[yY]') {
        foreach ($p in $procs) {
            try {
                Stop-Process -Id $p.Id -Force -ErrorAction Stop
                Write-Info ("  killed PID={0}" -f $p.Id)
            } catch {
                Write-Warn ("  failed to kill PID={0}: {1}" -f $p.Id, $_.Exception.Message)
            }
        }
        Start-Sleep -Milliseconds 500
    } else {
        Write-Warn "Build may fail because exe is locked. Aborting."
        exit 1
    }
}

function Invoke-Build {
    Write-Info "Building $Configuration|$script:Platform ..."
    Push-Location $script:RepoRoot
    try {
        $args = @(
            "build", $script:Csproj,
            "-c", $Configuration,
            "-p:Platform=$script:Platform",
            "-nologo",
            "-v:m"
        )
        & dotnet @args
        if ($LASTEXITCODE -ne 0) {
            Write-Warn "Build failed (exit $LASTEXITCODE). Aborting."
            exit $LASTEXITCODE
        }
        Write-Info "Build succeeded."
    } finally {
        Pop-Location
    }
}

function Start-Ui {
    if (-not (Test-Path $script:Exe)) {
        Write-Warn "exe not found: $script:Exe"
        Write-Warn "Run without -SkipBuild to build first."
        exit 1
    }

    Write-Info "Launching: $script:Exe"
    # 用 Start-Process 启动，脚本立即返回；exe 是 WinForms，不会阻塞控制台
    Start-Process -FilePath $script:Exe -WorkingDirectory $script:OutputDir
    Write-Info "Race Console window should open shortly."
}

# ---------- main ----------

Write-Host ""
Write-Host "============================================" -ForegroundColor Magenta
Write-Host "  SleepRunner Race Console - one-click run"  -ForegroundColor Magenta
Write-Host "============================================" -ForegroundColor Magenta
Write-Host ""

if (-not $SkipBuild) {
    Stop-RunningInstances
    Invoke-Build
} else {
    Write-Info "SkipBuild=on, using existing exe."
}

Start-Ui
