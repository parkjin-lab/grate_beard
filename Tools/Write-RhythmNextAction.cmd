@echo off
setlocal
for %%I in ("%~dp0..") do set "PROJECT_ROOT=%%~fI"
set "SUMMARY_PATH=%PROJECT_ROOT%\Logs\RhythmValidation\rhythm_snapshot_summary_last.json"
set "NEXT_ACTION_PATH=%PROJECT_ROOT%\Logs\RhythmValidation\rhythm_next_action_last.json"
call "%~dp0Write-RhythmSnapshotSummary.cmd"
if errorlevel 1 exit /b %ERRORLEVEL%
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Get-RhythmNextAction.ps1" -SummaryJsonPath "%SUMMARY_PATH%" -OutputJsonPath "%NEXT_ACTION_PATH%" %*
exit /b %ERRORLEVEL%
