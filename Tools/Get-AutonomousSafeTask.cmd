@echo off
setlocal
for %%I in ("%~dp0..") do set "PROJECT_ROOT=%%~fI"
set "RHYTHM_NEXT_ACTION_PATH=%PROJECT_ROOT%\Logs\RhythmValidation\rhythm_next_action_last.json"
set "STATIC_PREFLIGHT_PATH=%PROJECT_ROOT%\Logs\ReleaseSoak\local_static_preflight_last_summary.json"
set "SAFE_TASK_PATH=%PROJECT_ROOT%\Logs\Autonomous\autonomous_safe_task_last.json"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Get-AutonomousSafeTask.ps1" -RhythmNextActionJsonPath "%RHYTHM_NEXT_ACTION_PATH%" -StaticPreflightJsonPath "%STATIC_PREFLIGHT_PATH%" -OutputJsonPath "%SAFE_TASK_PATH%" %*
exit /b %ERRORLEVEL%
