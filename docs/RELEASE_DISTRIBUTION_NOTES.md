# Release / Distribution Notes

> Internal maintainer note.
> This file records the current thinking around packaging, installation, and update delivery.

## Snapshot

- Date: 2026-04-21
- Project type: WinForms + .NET 8 desktop app
- Current local startup path: `scripts/run-ui.cmd` -> `scripts/run-ui.ps1`
- Current local workflow is developer-oriented, not end-user-oriented

## Current State

- `run-ui.cmd` / `run-ui.ps1` are development scripts.
- They build the project locally and then start the built exe from `src/bin/...`.
- This is convenient for maintainers, but it is not a good distribution model for end users.
- End users should receive published artifacts, not the source repo plus build instructions.

## Runtime Packaging Constraint

- The app depends on files under `assets/`, which are copied next to the built executable during build/publish.
- This means distribution should be treated as a published directory, not as a lone script and not as "just one random exe from bin".
- For the near term, assume the whole publish output directory must be shipped together.

## Recommended Release Stages

### Stage 1: Portable zip package

- Publish a self-contained `win-x64` output directory.
- Zip the entire publish folder.
- Users download, extract, and run `SleepRunner.exe`.
- Users manually replace the folder when a new version is released.

Recommended command:

```powershell
dotnet publish .\src\SleepRunner.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -o .\dist\win-x64
```

### Stage 2: Installer

- When the app starts being shared more broadly, add a proper installer.
- This gives users a more standard Windows installation experience and creates a cleaner path toward updates.

### Stage 3: Auto-update

- If releases become frequent, adopt an installer/updater framework such as Velopack.
- Do not base user updates on `run-ui.cmd`.

## Important Precondition Before Auto-Update

Current writable data is stored under the executable directory:

- `assets/config/user_settings.json`
- `assets/logs/`
- `assets/screenshots/`
- `assets/supervision/`

This is acceptable for local development, but it is not ideal for installers or auto-updaters because:

- installed app directories may not be writable
- updater workflows may replace the app directory as a whole

Before introducing installer-based updates, migrate writable user data to a user-specific directory such as:

```text
%LocalAppData%/SleepRunner/
```

Suggested split:

- app directory: exe, dlls, shipped assets, default profiles
- user data directory: settings, logs, screenshots, incidents, mutable runtime data

## Conclusion From This Round

- Lowest-cost sharing path right now: portable published folder / zip package
- Better long-term UX: installer + update framework
- Key prerequisite for reliable updates: separate program files from user-writable data

## Session Note

- No feature code was changed in this round.
- Work performed:
  - inspected startup scripts
  - inspected project packaging/resource layout
  - reviewed release/update options
  - attempted one `dotnet publish` run, which timed out and was not treated as release verification
