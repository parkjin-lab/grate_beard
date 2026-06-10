@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Summarize-RhythmSnapshots.ps1" %*
exit /b %ERRORLEVEL%
