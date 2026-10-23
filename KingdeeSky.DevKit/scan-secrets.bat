@echo off
rem Author: rachelli901 <rachelli901@users.noreply.github.com>
rem Repository: https://github.com/rachelli901/kingdee-sky-devkit

chcp 65001 >nul
rem Static secret scanner entry point (Windows). Wraps eng\scan-secrets.ps1.
rem Usage: scan-secrets.bat [-Root <path>] [-IncludeTests]
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0eng\scan-secrets.ps1" %*
exit /b %ERRORLEVEL%