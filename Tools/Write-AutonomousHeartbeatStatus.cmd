@echo off
setlocal
for %%I in ("%~dp0..") do set "PROJECT_ROOT=%%~fI"
set "RHYTHM_NEXT_ACTION_PATH=%PROJECT_ROOT%\Logs\RhythmValidation\rhythm_next_action_last.json"
set "STATIC_PREFLIGHT_PATH=%PROJECT_ROOT%\Logs\ReleaseSoak\local_static_preflight_last_summary.json"
set "STATUS_PATH=%PROJECT_ROOT%\Logs\Autonomous\autonomous_heartbeat_status_last.md"
call "%~dp0Write-RhythmCaptureHandoff.cmd"
if errorlevel 1 exit /b %ERRORLEVEL%
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Write-AutonomousHeartbeatStatus.ps1" -RhythmNextActionJsonPath "%RHYTHM_NEXT_ACTION_PATH%" -StaticPreflightJsonPath "%STATIC_PREFLIGHT_PATH%" -OutputPath "%STATUS_PATH%" %*
exit /b %ERRORLEVEL%
