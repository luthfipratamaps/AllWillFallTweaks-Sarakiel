@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1"
if errorlevel 1 (echo. & echo [ERROR] Operation failed. & pause & exit /b 1)
echo. & echo [OK] Done. & pause
