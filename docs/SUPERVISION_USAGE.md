# Supervision MVP

This is the first-stage supervision flow for race autorun.

The design is intentionally conservative:

- `SleepRunner --race-auto` remains the only component that drives the game.
- `scripts/watch-race.ps1` is an external watchdog.
- The watchdog does not try to fix anything automatically.
- On suspicious behavior, the watchdog stops the runner and creates an incident pack for analysis.

## Directory Layout

Runtime output is separated under the built app directory:

```text
bin/x64/<Configuration>/net8.0-windows10.0.17763.0/
  assets/
    logs/
      latest.log
      <session>.log
    supervision/
      snapshots/
        <manual snapshot>.png
      watch_runs/
        <run-id>/
          watcher.log
          build.log
          runner_stdout.log
          runner_stderr.log
          snapshot_cli.log
          snapshots/
      incidents/
        <incident-id>/
          incident.json
          latest.log
          latest.tail.log
          runner_session.log
          runner_stdout.log
          runner_stderr.log
          watcher.log
          snapshots/
```

## New CLI

`--snapshot`

Captures the current game window without touching `latest.log`.

Examples:

```powershell
dotnet run -c Debug -- --snapshot
dotnet run -c Debug -- --snapshot "E:\Code\SleepRunner\bin\x64\Debug\net8.0-windows10.0.17763.0\assets\supervision\snapshots\manual_test.png"
```

## Watcher Script

Script path:

```text
scripts/watch-race.ps1
```

Manual stop helper:

```text
scripts/stop-watch-race.ps1
```

The watcher:

1. Builds the project unless `-SkipBuild` is used.
2. Starts `--race-auto`.
3. Polls `latest.log`.
4. Takes periodic snapshots by calling `--snapshot`.
5. Stops the runner and writes an incident pack when one of the configured conditions is met.
6. Prints a live English trace of key runner decisions by default.

## First-Stage Stop Conditions

- Runner process exits with a non-zero exit code.
- `latest.log` does not advance for too long.
- The same `Decision preview` repeats too many times in a row.

These are only the first guardrails. Screenshot diffing and richer incident analysis can be added later.

## Example Commands

Attack build:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\watch-race.ps1
```

Survival build:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\watch-race.ps1 -BuildDirection survival
```

Reuse existing binaries:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\watch-race.ps1 -SkipBuild
```

Manual stop from another PowerShell window:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\stop-watch-race.ps1
```

Tune the first-stage thresholds:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\watch-race.ps1 `
  -SnapshotIntervalSeconds 15 `
  -LogStallSeconds 120 `
  -RepeatThreshold 6
```

Disable live runner trace output:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\watch-race.ps1 -ShowRunnerTrace $false
```

## Notes

- Run the watcher from a normal interactive Windows PowerShell session.
- Keep the game window on the interactive desktop.
- The watcher only stops the runner process by PID. It does not kill the game process.
- To stop a running watcher, prefer `stop-watch-race.ps1` instead of just closing the watcher window.
