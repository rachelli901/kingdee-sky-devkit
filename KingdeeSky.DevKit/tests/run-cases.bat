@echo off
rem Author: rachelli901 <rachelli901@users.noreply.github.com>
rem Repository: https://github.com/rachelli901/kingdee-sky-devkit

chcp 65001 >nul
rem L1 code-level case runner entry (62 cases, TC-L1-001..062)
rem Usage: run-cases.bat [-All | -Case TC-L1-001 | -Group A | -List]
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-cases.ps1" %*
exit /b %ERRORLEVEL%