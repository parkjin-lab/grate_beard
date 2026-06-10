@echo off
setlocal
set "PROJECT_ROOT=%~dp0.."
set "SUMMARY_PATH=%PROJECT_ROOT%\Logs\RhythmValidation\rhythm_snapshot_summary_last.json"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Summarize-RhythmSnapshots.ps1" -OutputJsonPath "%SUMMARY_PATH%" %*
exit /b %ERRORLEVEL%
