[CmdletBinding()]
param(
    [ValidateSet("attack", "survival")]
    [string]$BuildDirection = "attack",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [int]$SnapshotIntervalSeconds = 20,
    [int]$LogStallSeconds = 90,
    [int]$RepeatThreshold = 5,
    [bool]$ShowRunnerTrace = $true,
    [bool]$CleanPreviousArtifacts = $true,

    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [Console]::OutputEncoding

$script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$script:Platform = "x64"
$script:TargetFramework = "net8.0-windows10.0.17763.0"
$script:OutputDir = Join-Path $script:RepoRoot "src\bin\$script:Platform\$Configuration\$script:TargetFramework"
$script:SupervisionRoot = Join-Path $script:OutputDir "assets\supervision"
$script:RunId = Get-Date -Format "yyyyMMdd_HHmmss"
$script:RunDir = Join-Path $script:SupervisionRoot "watch_runs\$script:RunId"
$script:SnapshotsDir = Join-Path $script:RunDir "snapshots"
$script:RunnerPidFile = Join-Path $script:RunDir "runner.pid"
$script:StopRequestFile = Join-Path $script:RunDir "stop.requested"
$script:CurrentRunPointerFile = Join-Path $script:SupervisionRoot "current_run.txt"
$script:WatcherLog = Join-Path $script:RunDir "watcher.log"
$script:BuildLog = Join-Path $script:RunDir "build.log"
$script:SnapshotCliLog = Join-Path $script:RunDir "snapshot_cli.log"
$script:RunnerStdout = Join-Path $script:RunDir "runner_stdout.log"
$script:RunnerStderr = Join-Path $script:RunDir "runner_stderr.log"
$script:LatestLog = Join-Path $script:OutputDir "assets\logs\latest.log"
$script:Runner = $null
$script:RecentSnapshotPaths = @()
$script:LastSeenLatestWriteUtc = [datetime]::MinValue
$script:LastLogProgressUtc = [datetime]::UtcNow
$script:LastPrintedLatestLineCount = 0

function Write-BootstrapWatcherMessage {
    param([string]$Message)

    $line = "[{0}] [Watcher] {1}" -f (Get-Date -Format "HH:mm:ss"), $Message
    Write-Host $line
}

function Append-Utf8Line {
    param(
        [string]$Path,
        [string]$Line
    )

    Add-Content -Path $Path -Value $Line -Encoding UTF8
}

function Write-WatcherLog {
    param([string]$Message)

    $line = "[{0}] [Watcher] {1}" -f (Get-Date -Format "HH:mm:ss"), $Message
    Write-Host $line
    Append-Utf8Line -Path $script:WatcherLog -Line $line
}

function Write-RunnerTrace {
    param([string]$Message)

    $line = "[{0}] [Trace] {1}" -f (Get-Date -Format "HH:mm:ss"), $Message
    Write-Host $line
    Append-Utf8Line -Path $script:WatcherLog -Line $line
}

function Copy-IfExists {
    param(
        [string]$SourcePath,
        [string]$DestinationPath
    )

    if (Test-Path $SourcePath) {
        Copy-Item -Path $SourcePath -Destination $DestinationPath -Force
    }
}

function Set-CurrentRunPointer {
    $script:RunDir | Set-Content -Path $script:CurrentRunPointerFile -Encoding UTF8
}

function Clear-CurrentRunPointer {
    if (-not (Test-Path $script:CurrentRunPointerFile)) {
        return
    }

    $currentValue = (Get-Content -Path $script:CurrentRunPointerFile -Raw -ErrorAction SilentlyContinue).Trim()
    if ($currentValue -eq $script:RunDir) {
        Remove-Item -Path $script:CurrentRunPointerFile -Force -ErrorAction SilentlyContinue
    }
}

function Clear-PreviousWatcherArtifacts {
    $watchRunsDir = Join-Path $script:SupervisionRoot "watch_runs"
    $logsDir = Join-Path $script:OutputDir "assets\logs"
    $removed = @()

    if (Test-Path $watchRunsDir) {
        Remove-Item -Path $watchRunsDir -Recurse -Force
        $removed += $watchRunsDir
    }

    if (Test-Path $script:CurrentRunPointerFile) {
        Remove-Item -Path $script:CurrentRunPointerFile -Force -ErrorAction SilentlyContinue
        $removed += $script:CurrentRunPointerFile
    }

    if (Test-Path $script:LatestLog) {
        Remove-Item -Path $script:LatestLog -Force -ErrorAction SilentlyContinue
        $removed += $script:LatestLog
    }

    if (Test-Path $logsDir) {
        $raceLogs = Get-ChildItem -Path $logsDir -Filter "*_cli_race_auto.log" -File -ErrorAction SilentlyContinue
        foreach ($raceLog in $raceLogs) {
            Remove-Item -Path $raceLog.FullName -Force -ErrorAction SilentlyContinue
            $removed += $raceLog.FullName
        }
    }

    if ($removed.Count -eq 0) {
        Write-BootstrapWatcherMessage "Cleanup: no previous watcher artifacts found."
        return
    }

    Write-BootstrapWatcherMessage "Cleanup: removed $($removed.Count) previous watcher artifacts."
}

function Register-SnapshotPath {
    param([string]$Path)

    if (-not $Path) {
        return
    }

    $script:RecentSnapshotPaths += $Path
    if ($script:RecentSnapshotPaths.Count -gt 6) {
        $script:RecentSnapshotPaths = @($script:RecentSnapshotPaths | Select-Object -Last 6)
    }
}

function Invoke-Snapshot {
    param([string]$Tag)

    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $path = Join-Path $script:SnapshotsDir ("{0}_{1}.png" -f $timestamp, $Tag)
    $arguments = @($script:DllPath, "--snapshot", $path)
    $output = & $script:DotnetExe @arguments 2>&1

    if ($output) {
        $output | Add-Content -Path $script:SnapshotCliLog -Encoding UTF8
    }

    if ($LASTEXITCODE -ne 0) {
        Write-WatcherLog "Snapshot command failed (tag=$Tag, exit=$LASTEXITCODE)."
        return $null
    }

    if (-not (Test-Path $path)) {
        Write-WatcherLog "Snapshot command completed but file is missing: $path"
        return $null
    }

    Register-SnapshotPath -Path $path
    Write-WatcherLog "Snapshot captured: $path"
    return $path
}

function Get-RecentLogLines {
    if (-not (Test-Path $script:LatestLog)) {
        return @()
    }

    return @(Get-Content -Path $script:LatestLog -Tail 200 -Encoding UTF8 -ErrorAction SilentlyContinue)
}

function Get-NewLatestLogLines {
    if (-not (Test-Path $script:LatestLog)) {
        return @()
    }

    $lines = @(Get-Content -Path $script:LatestLog -Encoding UTF8 -ErrorAction SilentlyContinue)
    if ($script:LastPrintedLatestLineCount -gt $lines.Count) {
        $script:LastPrintedLatestLineCount = 0
    }

    if ($lines.Count -le $script:LastPrintedLatestLineCount) {
        return @()
    }

    $start = $script:LastPrintedLatestLineCount
    $newLines = @($lines[$start..($lines.Count - 1)])
    $script:LastPrintedLatestLineCount = $lines.Count
    return $newLines
}

function Convert-ToAsciiSummary {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return ""
    }

    $chars = foreach ($ch in $Text.ToCharArray()) {
        $code = [int][char]$ch
        if ($code -ge 32 -and $code -le 126) {
            $ch
        }
        else {
            ' '
        }
    }

    return ((-join $chars) -replace '\s+', ' ').Trim()
}

function Convert-DecisionPreviewSummary {
    param([string]$Decision)

    if ($Decision -match 'Event: \[([^\]]+)\].*option (\d+)/(\d+) at \(([0-9.]+),([0-9.]+)\)') {
        return "Decision: event=$($Matches[1]) option=$($Matches[2])/$($Matches[3]) at=($($Matches[4]),$($Matches[5]))"
    }

    if ($Decision -match 'Event: \[([^\]]+)\].*fallback option (\d+)/(\d+)') {
        return "Decision: event=$($Matches[1]) fallback-option=$($Matches[2])/$($Matches[3])"
    }

    $ascii = Convert-ToAsciiSummary $Decision
    if (-not [string]::IsNullOrWhiteSpace($ascii)) {
        return "Decision: $ascii"
    }

    return "Decision: [non-ascii summary unavailable]"
}

function Get-TradeTraceMessage {
    # 集中识别 Race:Trade 子系统的 FLOW/buy 关键日志，配合 watcher 统一输出
    # 必须定义在 Get-TraceMessageFromLogLine 之前，避免 Set-StrictMode 下的前向引用失败
    param([string]$Line)

    if ($Line -match '\[Race:Trade\] FLOW Stage hit: (.+)$') {
        return "Trade stage: HIT $($Matches[1])"
    }

    if ($Line -match '\[Race:Trade\] FLOW Handle start: tradeVisited=(\w+), tradeClickY=([0-9.]+), appraiseClickY=([0-9.]+)') {
        return "Trade handle: visited=$($Matches[1]) tradeY=$($Matches[2]) appraiseY=$($Matches[3])"
    }

    if ($Line -match '\[Race:Trade\] FLOW Phase=([^:]+): (.+)$') {
        return "Trade phase=$($Matches[1]): $($Matches[2])"
    }

    if ($Line -match '\[Race:Trade\] FLOW exit-trade r(\d+): (.+)$') {
        return "Trade exit r$($Matches[1]): $($Matches[2])"
    }

    if ($Line -match '\[Race:Trade\] FLOW exit-trade: (.+)$') {
        return "Trade exit: $($Matches[1])"
    }

    if ($Line -match '\[Race:Trade\] FLOW DONE: (.+)$') {
        return "Trade done: $($Matches[1])"
    }

    if ($Line -match '\[Race:Trade\] Trade executor: detected budget=(\d+)') {
        return "Trade budget: $($Matches[1])"
    }

    if ($Line -match '\[Race:Trade\] Trade executor: budget not recognized') {
        return "Trade budget: unknown (no-budget filter)"
    }

    if ($Line -match '\[Race:Trade\] Trade offer\[(\d+)\]: price=(\d+), strengthGain=(\d+), staminaRecover=(\d+), strengthMatch=(\w+), staminaMatch=(\w+), potentialPoint=(\w+), mustBuy=(\w+),') {
        return "Trade offer #$($Matches[1]): price=$($Matches[2]) str=+$($Matches[3]) sta=+$($Matches[4]) mustBuy=$($Matches[8]) potential=$($Matches[7])"
    }

    if ($Line -match '\[Race:Trade\] Trade executor queue\[(\d+)\]: slot=(\d+), price=(\d+), strength=(\d+), reason=(.+)$') {
        return "Trade buy queue[$($Matches[1])]: slot=$($Matches[2]) price=$($Matches[3]) str=+$($Matches[4]) reason=$($Matches[5])"
    }

    if ($Line -match '\[Race:Trade\] Trade executor: no affordable must-buy item\.') {
        return "Trade buy queue: <empty> (nothing to buy)"
    }

    if ($Line -match '\[Race:Trade\] Trade executor decision: choose slot=(\d+), price=(\d+), gain=(\d+), reason=(.+)$') {
        return "Trade buy decision: slot=$($Matches[1]) price=$($Matches[2]) reason=$($Matches[4])"
    }

    if ($Line -match '\[Race:Trade\] Trade executor: buy clicked at \(([0-9.]+),([0-9.]+)\), attempt=(\d+), text=''(.+)'', accepted=(\w+)') {
        return "Trade buy click: at=($($Matches[1]),$($Matches[2])) accepted=$($Matches[5])"
    }

    if ($Line -match '\[Race:Trade\] Trade executor finished: bought=(\w+)') {
        return "Trade executor finished: bought=$($Matches[1])"
    }

    if ($Line -match '\[Race:Trade\] Step1: trade screen verified on attempt (\d+)') {
        return "Trade enter: VERIFIED on attempt $($Matches[1])"
    }

    if ($Line -match '\[Race:Trade\] Step3: still in stage menu after commission click') {
        return "Trade appraise click: still on menu (will retry)"
    }

    return $null
}

function Get-TraceMessageFromLogLine {
    param([string]$Line)

    if ($Line -match '\[Race\] >>> Handler matched: (.+)$') {
        $handler = Convert-ToAsciiSummary $Matches[1]
        if ([string]::IsNullOrWhiteSpace($handler)) {
            return "Step: handler matched"
        }

        return "Step: handler matched -> $handler"
    }

    if ($Line -match '\[Race\] Decision preview: (.+)$') {
        return Convert-DecisionPreviewSummary $Matches[1]
    }

    if ($Line -match '\[Race\] AUTO: \*\*\* MANUAL INTERVENTION NEEDED \*\*\*') {
        return "Auto mode: manual intervention needed"
    }

    if ($Line -match '\[Race:Event\] Matched: \[([^\]]+)\]') {
        return "Event matched: id=$($Matches[1])"
    }

    if ($Line -match '\[Race:Event\] UNKNOWN EVENT:') {
        return "Event matched: UNKNOWN -> manual wait"
    }

    if ($Line -match '\[Race:Event\] Option click calibrate: option=(\d+/\d+), y=([0-9.]+), rawY=([0-9.]+), score=(-?\d+), rawPx=\((\d+),(\d+)\), resolvedPx=\((\d+),(\d+)\)') {
        return "Event row OCR: resolved option=$($Matches[1]) y=$($Matches[2]) rawY=$($Matches[3]) rawPx=($($Matches[5]),$($Matches[6])) resolvedPx=($($Matches[7]),$($Matches[8])) score=$($Matches[4])"
    }

    if ($Line -match '\[Race:Event\] Option click calibrate: fallback option=(\d+/\d+), y=([0-9.]+), bestScore=(-?\d+), oppositeHits=(\d+), fallbackPx=\((\d+),(\d+)\), bestRawPx=\((\d+),(\d+)\)') {
        return "Event row OCR: FALLBACK option=$($Matches[1]) y=$($Matches[2]) fallbackPx=($($Matches[5]),$($Matches[6])) bestRawPx=($($Matches[7]),$($Matches[8])) score=$($Matches[3]) oppositeHits=$($Matches[4])"
    }

    if ($Line -match '\[Race:Event\] Auto-selecting option (\d+)/(\d+) at \(([0-9.]+), ([0-9.]+)\)') {
        return "Event click plan: option=$($Matches[1])/$($Matches[2]) at=($($Matches[3]),$($Matches[4]))"
    }

    if ($Line -match '\[Race:Event\] .*click(?: primary| fallback)? option (\d+)/(\d+) target pct=\(([0-9.]+),([0-9.]+)\) px=\((\d+),(\d+)\)') {
        return "Event click target: option=$($Matches[1])/$($Matches[2]) pct=($($Matches[3]),$($Matches[4])) px=($($Matches[5]),$($Matches[6]))"
    }

    if ($Line -match '\[Race:Event\] .*screen changed after') {
        return "Event change-check: screen changed"
    }

    if ($Line -match '\[Race:Event\] .*no screen change after primary') {
        return "Event change-check: no screen change after primary click"
    }

    if ($Line -match '\[Race:Event\] .*retry same option at anchor Y=([0-9.]+) \(delta=([0-9.]+), px=\((\d+),(\d+)\), shot=(\d+)x(\d+)\)') {
        return "Event retry plan: anchorY=$($Matches[1]) delta=$($Matches[2]) px=($($Matches[3]),$($Matches[4]))"
    }

    if ($Line -match '\[Race:Event\] .*alternate Y is too close \(([0-9.]+), delta=([0-9.]+)\)') {
        return "Event retry plan: skipped anchorY=$($Matches[1]) delta=$($Matches[2])"
    }

    if ($Line -match '\[Race:Event\] .*fallback option also kept same event text') {
        return "Event change-check: fallback kept same event text"
    }

    if ($Line -match '\[Race:Event\] .*conservative retry also kept same event text') {
        return "Event change-check: conservative retry kept same event text"
    }

    if ($Line -match '\[Race:TrainingSelect\] Decision:') {
        $ascii = Convert-ToAsciiSummary ($Line -replace '^.*\[Race:TrainingSelect\]\s*', '')
        if (-not [string]::IsNullOrWhiteSpace($ascii)) {
            return "Training decision: $ascii"
        }
    }

    if ($Line -match '\[Race:MainMenu\] Main menu click resolve: prefer=([a-z]+), y=([0-9.]+), score=(-?\d+)') {
        return "Main menu route: prefer=$($Matches[1]) y=$($Matches[2]) score=$($Matches[3])"
    }

    # Trade flow trace 单独抽到 Get-TradeTraceMessage 中维护，避免主函数过长导致解析问题
    # 显式初始化兜底：strict mode 下如果调用失败，$tradeTrace 仍可被安全读取
    $tradeTrace = $null
    $tradeTrace = Get-TradeTraceMessage $Line
    if ($null -ne $tradeTrace) {
        return $tradeTrace
    }

    return $null
}

function Emit-NewRunnerTrace {
    if (-not $ShowRunnerTrace) {
        return
    }

    foreach ($line in Get-NewLatestLogLines) {
        $message = Get-TraceMessageFromLogLine $line
        if ($message) {
            Write-RunnerTrace $message
        }
    }
}

function Get-LastDecisionInfo {
    param([string[]]$Lines)

    if (-not $Lines -or $Lines.Count -eq 0) {
        return $null
    }

    $decisionIndexes = @()
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -match "\[Race\] Decision preview: (.+)$") {
            $decisionIndexes += $i
        }
    }

    if ($decisionIndexes.Count -eq 0) {
        return $null
    }

    $lastDecisionIndex = $decisionIndexes[-1]
    $lastDecision = ([regex]::Match($Lines[$lastDecisionIndex], "\[Race\] Decision preview: (.+)$")).Groups[1].Value.Trim()

    $repeatCount = 1
    for ($i = $decisionIndexes.Count - 2; $i -ge 0; $i--) {
        $candidateIndex = $decisionIndexes[$i]
        $candidateDecision = ([regex]::Match($Lines[$candidateIndex], "\[Race\] Decision preview: (.+)$")).Groups[1].Value.Trim()
        if ($candidateDecision -eq $lastDecision) {
            $repeatCount++
            continue
        }

        break
    }

    $handler = $null
    for ($i = $lastDecisionIndex; $i -ge 0; $i--) {
        if ($Lines[$i] -match "\[Race\] >>> Handler matched: (.+)$") {
            $handler = $Matches[1].Trim()
            break
        }
    }

    return [pscustomobject]@{
        Handler = $handler
        Decision = $lastDecision
        RepeatCount = $repeatCount
    }
}

function Get-RunnerSessionLogPath {
    if (-not (Test-Path $script:RunnerStdout)) {
        return $null
    }

    $match = Select-String -Path $script:RunnerStdout -Pattern "Session started: '.*' -> (.+)$" | Select-Object -Last 1
    if (-not $match) {
        return $null
    }

    return $match.Matches[0].Groups[1].Value.Trim()
}

function New-IncidentPack {
    param(
        [string]$Reason,
        [string]$Detail,
        $DecisionInfo,
        [string[]]$LastLines,
        [string]$PreStopSnapshot
    )

    $incidentTag = ($Reason -replace "[^A-Za-z0-9_-]", "_").Trim("_")
    if ([string]::IsNullOrWhiteSpace($incidentTag)) {
        $incidentTag = "incident"
    }

    $incidentId = "{0}_{1}" -f (Get-Date -Format "yyyyMMdd_HHmmss"), $incidentTag
    $incidentDir = Join-Path $script:SupervisionRoot "incidents\$incidentId"
    $incidentSnapshotsDir = Join-Path $incidentDir "snapshots"

    New-Item -ItemType Directory -Path $incidentDir -Force | Out-Null
    New-Item -ItemType Directory -Path $incidentSnapshotsDir -Force | Out-Null

    Copy-IfExists -SourcePath $script:WatcherLog -DestinationPath (Join-Path $incidentDir "watcher.log")
    Copy-IfExists -SourcePath $script:BuildLog -DestinationPath (Join-Path $incidentDir "build.log")
    Copy-IfExists -SourcePath $script:SnapshotCliLog -DestinationPath (Join-Path $incidentDir "snapshot_cli.log")
    Copy-IfExists -SourcePath $script:RunnerStdout -DestinationPath (Join-Path $incidentDir "runner_stdout.log")
    Copy-IfExists -SourcePath $script:RunnerStderr -DestinationPath (Join-Path $incidentDir "runner_stderr.log")
    Copy-IfExists -SourcePath $script:LatestLog -DestinationPath (Join-Path $incidentDir "latest.log")

    $runnerSessionLog = Get-RunnerSessionLogPath
    if ($runnerSessionLog -and (Test-Path $runnerSessionLog)) {
        Copy-Item -Path $runnerSessionLog -Destination (Join-Path $incidentDir "runner_session.log") -Force
    }

    if ($LastLines -and $LastLines.Count -gt 0) {
        $LastLines | Set-Content -Path (Join-Path $incidentDir "latest.tail.log") -Encoding UTF8
    }

    foreach ($snapshotPath in $script:RecentSnapshotPaths) {
        if (Test-Path $snapshotPath) {
            Copy-Item -Path $snapshotPath -Destination (Join-Path $incidentSnapshotsDir (Split-Path $snapshotPath -Leaf)) -Force
        }
    }

    if ($PreStopSnapshot -and (Test-Path $PreStopSnapshot)) {
        Copy-Item -Path $PreStopSnapshot -Destination (Join-Path $incidentSnapshotsDir "final_snapshot.png") -Force
    }

    $metadata = [ordered]@{
        incident_id = $incidentId
        run_id = $script:RunId
        reason = $Reason
        detail = $Detail
        triggered_at = (Get-Date).ToString("o")
        build_direction = $BuildDirection
        configuration = $Configuration
        runner_pid = if ($script:Runner) { $script:Runner.Id } else { $null }
        runner_exit_code = if ($script:Runner -and $script:Runner.HasExited) { $script:Runner.ExitCode } else { $null }
        latest_log = if (Test-Path $script:LatestLog) { $script:LatestLog } else { $null }
        runner_session_log = $runnerSessionLog
        last_handler = if ($DecisionInfo) { $DecisionInfo.Handler } else { $null }
        last_decision = if ($DecisionInfo) { $DecisionInfo.Decision } else { $null }
        repeat_count = if ($DecisionInfo) { $DecisionInfo.RepeatCount } else { $null }
    }

    $metadata | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $incidentDir "incident.json") -Encoding UTF8
    return $incidentDir
}

function Stop-RunnerForIncident {
    param(
        [string]$Reason,
        [string]$Detail,
        $DecisionInfo,
        [string[]]$LastLines
    )

    Write-WatcherLog "Incident triggered: $Reason"
    Write-WatcherLog "Incident detail: $Detail"

    $finalSnapshot = Invoke-Snapshot -Tag "incident_final"

    if ($script:Runner -and -not $script:Runner.HasExited) {
        Write-WatcherLog "Stopping runner PID $($script:Runner.Id)..."
        Stop-Process -Id $script:Runner.Id -Force
        $script:Runner.WaitForExit()
    }

    $incidentDir = New-IncidentPack -Reason $Reason -Detail $Detail -DecisionInfo $DecisionInfo -LastLines $LastLines -PreStopSnapshot $finalSnapshot
    Write-WatcherLog "Incident pack created: $incidentDir"
    return $incidentDir
}

function Stop-RunnerForManualStop {
    param([string]$Detail)

    Write-WatcherLog "Manual stop requested."
    Write-WatcherLog "Manual stop detail: $Detail"

    $finalSnapshot = Invoke-Snapshot -Tag "manual_stop_final"
    if ($script:Runner -and -not $script:Runner.HasExited) {
        Write-WatcherLog "Stopping runner PID $($script:Runner.Id) for manual stop..."
        Stop-Process -Id $script:Runner.Id -Force
        $script:Runner.WaitForExit()
    }

    $summary = [ordered]@{
        run_id = $script:RunId
        stopped_at = (Get-Date).ToString("o")
        detail = $Detail
        runner_pid = if ($script:Runner) { $script:Runner.Id } else { $null }
        final_snapshot = $finalSnapshot
    }

    $summary | ConvertTo-Json -Depth 3 | Set-Content -Path (Join-Path $script:RunDir "manual_stop.json") -Encoding UTF8
    Write-WatcherLog "Manual stop completed."
}

try {
    $dotnetCommand = Get-Command dotnet -ErrorAction Stop
    $script:DotnetExe = $dotnetCommand.Source
    $script:DllPath = Join-Path $script:OutputDir "SleepRunner.dll"

    New-Item -ItemType Directory -Path $script:SupervisionRoot -Force | Out-Null
    if ($CleanPreviousArtifacts) {
        Clear-PreviousWatcherArtifacts
    }

    New-Item -ItemType Directory -Path $script:SnapshotsDir -Force | Out-Null
    Set-CurrentRunPointer
    Write-WatcherLog "Repo root: $script:RepoRoot"
    Write-WatcherLog "Output dir: $script:OutputDir"
    Write-WatcherLog "Watch run id: $script:RunId"
    Write-WatcherLog "Runner trace: $(if ($ShowRunnerTrace) { 'ON' } else { 'OFF' })"

    if (-not $SkipBuild) {
        Write-WatcherLog "Building solution..."
        & $script:DotnetExe build (Join-Path $script:RepoRoot "SleepRunner.sln") -c $Configuration -p:Platform=$script:Platform 2>&1 |
            Tee-Object -FilePath $script:BuildLog | Out-Host

        if ($LASTEXITCODE -ne 0) {
            throw "Build failed. See $script:BuildLog"
        }
    }
    else {
        Write-WatcherLog "SkipBuild enabled; using existing binaries."
    }

    if (-not (Test-Path $script:DllPath)) {
        throw "Runner DLL not found: $script:DllPath"
    }

    if (Test-Path $script:StopRequestFile) {
        Write-WatcherLog "Manual stop was requested before runner start. Exiting watcher."
        return
    }

    $runnerArgs = @($script:DllPath, "--race-auto")
    if ($BuildDirection -eq "survival") {
        $runnerArgs += "survival"
    }

    Write-WatcherLog "Starting runner in autorun mode..."
    $script:Runner = Start-Process `
        -FilePath $script:DotnetExe `
        -ArgumentList $runnerArgs `
        -WorkingDirectory $script:RepoRoot `
        -RedirectStandardOutput $script:RunnerStdout `
        -RedirectStandardError $script:RunnerStderr `
        -PassThru `
        -WindowStyle Hidden

    Write-WatcherLog "Runner PID: $($script:Runner.Id)"
    $script:Runner.Id | Set-Content -Path $script:RunnerPidFile -Encoding UTF8

    Start-Sleep -Seconds 2
    $lastSnapshotAt = Get-Date
    Invoke-Snapshot -Tag "startup" | Out-Null
    Emit-NewRunnerTrace

    while ($true) {
        Start-Sleep -Seconds 2
        Emit-NewRunnerTrace

        if (Test-Path $script:StopRequestFile) {
            Stop-RunnerForManualStop -Detail "stop.requested file detected."
            break
        }

        if ($script:Runner.HasExited) {
            $lastLines = Get-RecentLogLines
            $decisionInfo = Get-LastDecisionInfo -Lines $lastLines

            if ($script:Runner.ExitCode -eq 0) {
                Write-WatcherLog "Runner exited cleanly with code 0."
                break
            }

            Stop-RunnerForIncident `
                -Reason "runner_exit" `
                -Detail "Runner exited unexpectedly with code $($script:Runner.ExitCode)." `
                -DecisionInfo $decisionInfo `
                -LastLines $lastLines | Out-Null
            break
        }

        if (Test-Path $script:LatestLog) {
            $latestItem = Get-Item $script:LatestLog
            if ($latestItem.LastWriteTimeUtc -gt $script:LastSeenLatestWriteUtc) {
                $script:LastSeenLatestWriteUtc = $latestItem.LastWriteTimeUtc
                $script:LastLogProgressUtc = [datetime]::UtcNow
            }

            $lastLines = Get-RecentLogLines
            $decisionInfo = Get-LastDecisionInfo -Lines $lastLines
            if ($decisionInfo -and $decisionInfo.RepeatCount -ge $RepeatThreshold) {
                $detail = "Repeated decision detected $($decisionInfo.RepeatCount)x. Handler='$($decisionInfo.Handler)', Decision='$($decisionInfo.Decision)'"
                Stop-RunnerForIncident `
                    -Reason "repeat_decision" `
                    -Detail $detail `
                    -DecisionInfo $decisionInfo `
                    -LastLines $lastLines | Out-Null
                break
            }
        }

        $staleSeconds = ([datetime]::UtcNow - $script:LastLogProgressUtc).TotalSeconds
        if ($staleSeconds -ge $LogStallSeconds) {
            $lastLines = Get-RecentLogLines
            $decisionInfo = Get-LastDecisionInfo -Lines $lastLines
            $detail = "latest.log has not advanced for {0:N0} seconds." -f $staleSeconds
            Stop-RunnerForIncident `
                -Reason "log_stall" `
                -Detail $detail `
                -DecisionInfo $decisionInfo `
                -LastLines $lastLines | Out-Null
            break
        }

        if (((Get-Date) - $lastSnapshotAt).TotalSeconds -ge $SnapshotIntervalSeconds) {
            Invoke-Snapshot -Tag "watch" | Out-Null
            $lastSnapshotAt = Get-Date
        }
    }

    Write-WatcherLog "Watcher finished."
}
catch {
    Write-Error $_
    exit 1
}
finally {
    Clear-CurrentRunPointer
}
