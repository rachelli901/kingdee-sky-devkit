@echo off
rem Author: rachelli901 <rachelli901@users.noreply.github.com>
rem Repository: https://github.com/rachelli901/kingdee-sky-devkit

setlocal enabledelayedexpansion
rem ============================================================
rem  Copy Kingdee.BOS.* assemblies from a licensed K3Cloud server
rem  into this lib folder so KingdeeSky.Plugins can compile.
rem ============================================================

set "DST=%~dp0"
set "FILES=Kingdee.BOS.dll Kingdee.BOS.Core.dll Kingdee.BOS.App.dll Kingdee.BOS.Orm.dll"

echo ============================================================
echo  KingdeeSky.DevKit - copy Kingdee BOS DLLs into lib
echo ============================================================
echo.
echo  Typical site bin path:
echo    C:\Program Files (x86)\Kingdee\K3Cloud\WebSite\bin
echo.
set /p "SRCDIR=Paste the K3Cloud WebSite\bin path, then press Enter: "

if not exist "%SRCDIR%\Kingdee.BOS.Core.dll" (
    echo.
    echo [ERROR] Kingdee.BOS.Core.dll not found in:
    echo         "%SRCDIR%"
    echo Please confirm the site bin path and run again.
    echo.
    pause
    exit /b 1
)

set /a OK=0
for %%F in (%FILES%) do (
    if exist "%SRCDIR%\%%F" (
        copy /Y "%SRCDIR%\%%F" "%DST%%%F" >nul 2>nul
        if !errorlevel! equ 0 (
            echo   [OK]      %%F
            set /a OK+=1
        ) else (
            echo   [FAILED]  %%F  ^(access denied?^)
        )
    ) else (
        echo   [MISSING] %%F
    )
)

echo.
echo Copied %OK% of 4 DLLs to:
echo   %DST%
echo.
if %OK% equ 4 (
    echo Done. Now open KingdeeSky.DevKit.sln and rebuild KingdeeSky.Plugins.
) else (
    echo Some DLLs are missing. Check the site bin folder and try again.
)
echo.
pause
