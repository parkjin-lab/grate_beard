@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Test-AutonomousHeartbeatWriter.ps1" %*
exit /b %ERRORLEVEL%
