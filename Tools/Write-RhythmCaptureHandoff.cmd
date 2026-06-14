@echo off
setlocal
for %%I in ("%~dp0..") do set "PROJECT_ROOT=%%~fI"
set "NEXT_ACTION_PATH=%PROJECT_ROOT%\Logs\RhythmValidation\rhythm_next_action_last.json"
set "HANDOFF_PATH=%PROJECT_ROOT%\Logs\RhythmValidation\rhythm_capture_handoff_last.md"
call "%~dp0Write-RhythmNextAction.cmd"
if errorlevel 1 exit /b %ERRORLEVEL%
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Write-RhythmCaptureHandoff.ps1" -NextActionJsonPath "%NEXT_ACTION_PATH%" -OutputPath "%HANDOFF_PATH%" %*
exit /b %ERRORLEVEL%
