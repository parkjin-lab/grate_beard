@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Test-AutonomousHeartbeatStatus.ps1" %*
exit /b %ERRORLEVEL%
