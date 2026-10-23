@echo off
rem ============================================================
rem  KingdeeSky.WebApiClient Demo Runner (Windows)
rem  Starts the local mock server (port 18080) if not running,
rem  then launches the .NET Framework WebApi client demo.
rem  Requirements: Python 3 available as `python`.
rem ============================================================
setlocal

netstat -ano 2>nul | findstr ":18080" | findstr "LISTENING" >nul
if errorlevel 1 (
    echo [1/2] Starting mock server on port 18080 ...
    start "KingdeeMock" cmd /k "cd /d %~dp0 && python mock_server.py 18080"
    timeout /t 2 /nobreak >nul
) else (
    echo [1/2] Mock server already running on port 18080, reuse it.
)

echo [2/2] Starting WebApi client demo ...
cd /d "%~dp0app"
KingdeeSky.WebApiClient.exe

echo.
echo Demo finished. You can stop the mock server window (KingdeeMock) anytime.
pause
endlocal