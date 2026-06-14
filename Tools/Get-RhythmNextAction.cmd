@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Get-RhythmNextAction.ps1" %*
exit /b %ERRORLEVEL%
