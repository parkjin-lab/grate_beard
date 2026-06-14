@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Test-RhythmNextAction.ps1" %*
exit /b %ERRORLEVEL%
