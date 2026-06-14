@echo off
setlocal
for %%I in ("%~dp0..") do set "PROJECT_ROOT=%%~fI"
set "RHYTHM_NEXT_ACTION_PATH=%PROJECT_ROOT%\Logs\RhythmValidation\rhythm_next_action_last.json"
set "STATIC_PREFLIGHT_PATH=%PROJECT_ROOT%\Logs\ReleaseSoak\local_static_preflight_last_summary.json"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Get-AutonomousSafeTask.ps1" -RhythmNextActionJsonPath "%RHYTHM_NEXT_ACTION_PATH%" -StaticPreflightJsonPath "%STATIC_PREFLIGHT_PATH%" %*
exit /b %ERRORLEVEL%
