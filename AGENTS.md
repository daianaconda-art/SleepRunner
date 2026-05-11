# SleepRunner Agent Notes

These instructions apply to the whole repository.

## Runtime verification after changes

When a task changes runtime behavior, scripts, automation flow, UI behavior, or anything that the user will immediately run:

1. Do not stop a running `SleepRunner` GUI, watcher, or `dotnet run` instance unless the user explicitly asks for it.
2. Do not start a fresh runtime session unless the user explicitly asks for it.
3. Prefer code-level verification such as focused tests and `dotnet build` when appropriate.
4. Leave final hands-on runtime verification, including fresh runs and latest-log inspection, to the user.

## Build source of truth

For compile verification, use the project entrypoint the user uses:

`dotnet build .\src\SleepRunner.csproj`

If the user asks for a fresh runtime verification, use:

`dotnet run --project .\src\SleepRunner.csproj`

Do not rely on stale binaries or `--skip-build` when the user specifically requests a fresh runtime verification.

## Log check

Only inspect runtime logs after code changes when the user asks for runtime verification or log analysis. The usual location is:

`src/bin/x64/Debug/net8.0-windows10.0.17763.0/assets/logs/latest.log`

If the run path writes logs somewhere else, use the actual newest runtime log instead.
