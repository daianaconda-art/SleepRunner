# SleepRunner Agent Notes

These instructions apply to the whole repository.

## Runtime loop after changes

When a task changes runtime behavior, scripts, automation flow, UI behavior, or anything that the user will immediately run:

1. Stop any running `SleepRunner` GUI, watcher, or `dotnet run` instance before rebuilding.
2. Rebuild from the project entrypoint the user uses.
3. Start a fresh run from that new build.
4. Check the new runtime result in the latest log.

## Build and run source of truth

For final verification, use the same project entrypoint the user uses:

`dotnet run --project .\src\SleepRunner.csproj`

Do not rely on stale binaries or `--skip-build` for the final rerun after code changes.

If a quick compile-only check is needed during iteration, `dotnet build .\src\SleepRunner.csproj` is fine, but the final rerun must still come from `dotnet run --project .\src\SleepRunner.csproj`.

## Log check

After the fresh rerun, inspect the newly written latest runtime log before concluding the task. The usual location is:

`src/bin/x64/Debug/net8.0-windows10.0.17763.0/assets/logs/latest.log`

If the run path writes logs somewhere else, use the actual newest runtime log instead.
