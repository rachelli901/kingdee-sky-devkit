@echo off
rem Author: rachelli901 <lir30481@gmail.com>
rem Repository: https://github.com/rachelli901/kingdee-sky-devkit

chcp 65001 >nul
rem Static secret scanner entry point (Windows). Wraps eng\scan-secrets.ps1.
rem Scope is the repository root (all version-controlled files), so the runnable
rem demo, the browser console sources and the deploy deliverables are gated too.
rem Usage: scan-secrets.bat [-Root <path>] [-IncludeTests]
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0eng\scan-secrets.ps1" -Root "%~dp0." %*
exit /b %ERRORLEVEL%