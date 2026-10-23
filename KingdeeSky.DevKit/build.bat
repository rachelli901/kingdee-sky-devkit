@echo off
rem ============================================================
rem  KingdeeSky.DevKit - one-click production build
rem  delegates to eng\build.ps1 (compile -> test gate -> package)
rem  usage: build.bat            (default version from eng\version.txt)
rem         build.bat 1.1.0      (override version)
rem         build.bat 1.1.0 -SkipPlugins
rem ============================================================
chcp 65001 >nul
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0eng\build.ps1" %1 %2
exit /b %ERRORLEVEL%