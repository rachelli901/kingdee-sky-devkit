@echo off
chcp 65001 >nul
rem L1 code-level case runner entry (62 cases, TC-L1-001..062)
rem Usage: run-cases.bat [-All | -Case TC-L1-001 | -Group A | -List]
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-cases.ps1" %*
exit /b %ERRORLEVEL%