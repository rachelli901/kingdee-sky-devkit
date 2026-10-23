@echo off
rem Author: rachelli901 <rachelli901@users.noreply.github.com>
rem Repository: https://github.com/rachelli901/kingdee-sky-devkit

chcp 65001 >nul
rem KingdeeSky.WebApiClient 测试门禁入口：编译并运行测试，失败时返回非 0
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-tests.ps1"
exit /b %ERRORLEVEL%