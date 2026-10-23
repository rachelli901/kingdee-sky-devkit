@echo off
chcp 65001 >nul
rem Static secret scanner entry point (Windows). Wraps eng\scan-secrets.ps1.
rem Usage: scan-secrets.bat [-Root <path>] [-IncludeTests]
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0eng\scan-secrets.ps1" %*
exit /b %ERRORLEVEL%