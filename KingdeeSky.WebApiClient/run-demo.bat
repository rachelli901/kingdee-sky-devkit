@echo off
rem Author: rachelli901 <rachelli901@users.noreply.github.com>
rem Repository: https://github.com/rachelli901/kingdee-sky-devkit

rem ============================================================
rem  KingdeeSky.WebApiClient Demo Runner (Windows)
rem  1) build the demo client from KingdeeSky.DevKit/src (app/ is a build output)
rem  2) start the local mock server on port 18080 if not already listening
rem  3) run the .NET Framework WebApi client demo
rem  Requirements: Python 3 available as `python`, .NET Framework 4.8.
rem ============================================================
setlocal
cd /d "%~dp0"

echo [1/3] Building demo client from DevKit source ...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-demo.ps1"
if errorlevel 1 (
    echo Build failed.
    pause
    exit /b 1
)

rem Login password shared by the mock server and the client. Never a literal in
rem the repo: the environment wins, otherwise mock_server.py stores a generated
rem value in the gitignored .demo-credential next to this script.
call :resolveCred
if defined KINGDEE_PASSWORD set "KINGDEE_MOCK_PASSWORD=%KINGDEE_PASSWORD%"

netstat -ano 2>nul | findstr /R /I /C:":18080 " | findstr /I "LISTENING" >nul
if errorlevel 1 (
    echo [2/3] Starting mock server on port 18080 ...
    start "KingdeeMock" cmd /k "cd /d %~dp0 && python mock_server.py 18080"
    timeout /t 2 /nobreak >nul
) else (
    echo [2/3] Mock server already listening on port 18080, reuse it.
)

rem mock_server.py writes .demo-credential when it had to generate the password.
call :resolveCred
if not defined KINGDEE_PASSWORD (
    echo ERROR: no demo credential available; set KINGDEE_PASSWORD, or start mock_server.py once.
    pause
    exit /b 1
)

echo [3/3] Starting WebApi client demo ...
cd /d "%~dp0app"
KingdeeSky.WebApiClient.exe

echo.
echo Demo finished. You can stop the mock server window (KingdeeMock) anytime.
pause
endlocal
exit /b 0

:resolveCred
if defined KINGDEE_PASSWORD exit /b 0
if exist ".demo-credential" set /p KINGDEE_PASSWORD=<".demo-credential"
exit /b 0