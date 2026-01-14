@echo off
REM Sonic Hybrid Ultimate Launcher (Windows)
REM This script runs the unified Sonic 1 + CD + 2 hybrid experience

setlocal

set SCRIPT_DIR=%~dp0
set DATA_FILE=%SCRIPT_DIR%Data.rsdk
set ENGINE_BINARY=%SCRIPT_DIR%..\..\build\bin\Release\rsdkv4.exe

echo ===================================================================
echo   Sonic Hybrid Ultimate Launcher
echo ===================================================================
echo.

REM Check if hybrid data exists
if not exist "%DATA_FILE%" (
    echo X Error: Hybrid data file not found!
    echo.
    echo Expected: %DATA_FILE%
    echo.
    echo To generate the hybrid data:
    echo   1. Place soniccd.rsdk, sonic1.rsdk, sonic2.rsdk in ..\rsdk-source-data\
    echo   2. Run the build process (build_all.ps1 or cmake --build)
    echo.
    echo See ..\rsdk-source-data\README.md for detailed instructions.
    pause
    exit /b 1
)

echo + Found hybrid data: %DATA_FILE%

REM Check if engine binary exists
if not exist "%ENGINE_BINARY%" (
    echo X Error: RSDKv4 engine not found!
    echo.
    echo Expected: %ENGINE_BINARY%
    echo.
    echo Please build the project first.
    pause
    exit /b 1
)

echo + Found RSDKv4 engine: %ENGINE_BINARY%
echo.
echo Starting Sonic Hybrid Ultimate...
echo   (Sonic 1 -^> Sonic CD -^> Sonic 2)
echo.

REM Change to the data directory so the engine can find Data.rsdk
cd /d "%SCRIPT_DIR%"

REM Run the engine
"%ENGINE_BINARY%"

echo.
echo Game exited.
pause
