@echo off
setlocal
for %%I in ("%~dp0..") do set "PROJECT_ROOT=%%~fI"
set "SUMMARY_PATH=%PROJECT_ROOT%\Logs\RhythmValidation\rhythm_snapshot_summary_last.json"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Summarize-RhythmSnapshots.ps1" -OutputJsonPath "%SUMMARY_PATH%" %*
exit /b %ERRORLEVEL%
