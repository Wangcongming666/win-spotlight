@echo off
cd /d "%~dp0"
where pwsh >nul 2>nul
if errorlevel 1 (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run.ps1"
) else (
  pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0run.ps1"
)
if errorlevel 1 pause
