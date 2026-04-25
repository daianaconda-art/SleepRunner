@echo off
REM 一键启动 SleepRunner Race Console
REM 双击即可：PowerShell 会重 build x64 平台，再拉起 exe
REM 想跳过 build：在命令行里跑 `run-ui.cmd --skip-build`

setlocal
set SCRIPT_DIR=%~dp0

if /I "%~1"=="--skip-build" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%run-ui.ps1" -SkipBuild
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%run-ui.ps1"
)

REM build / start 失败时停一下，让用户看清错误
if errorlevel 1 (
    echo.
    echo [run-ui.cmd] script exited with errorlevel %errorlevel%
    pause
)

endlocal
