@echo off
REM build_native_libs.bat - Build native libraries for Windows (simple wrapper)

echo Building native libraries for Sonic Hybrid Ultimate...
echo.

REM Check if PowerShell is available
where powershell >nul 2>nul
if %errorlevel% neq 0 (
    echo ERROR: PowerShell is not available. Please install PowerShell or run the build manually.
    pause
    exit /b 1
)

REM Run the PowerShell build script
powershell -ExecutionPolicy Bypass -File "build_native_libs.ps1"

if %errorlevel% neq 0 (
    echo.
    echo BUILD FAILED! Please check the error messages above.
    echo.
    echo Common solutions:
    echo - Install Visual Studio or Build Tools for Visual Studio
    echo - Install CMake
    echo - Ensure all dependencies are available
    pause
    exit /b 1
)

echo.
echo BUILD COMPLETED SUCCESSFULLY!
echo.
echo You can now build and run the Custom-Client C# application.
echo.
pause