# Package script for Sonic Hybrid Ultimate - Creates single-folder distribution
# PowerShell version for Windows

$ErrorActionPreference = "Stop"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Sonic Hybrid Ultimate - Package Builder" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$PROJECT_ROOT = $PSScriptRoot
$OUTPUT_DIR = Join-Path $PROJECT_ROOT "dist\SonicHybridUltimate"
$BUILD_DIR = Join-Path $PROJECT_ROOT "Hybrid-RSDK-Main\build"
$CLIENT_DIR = Join-Path $PROJECT_ROOT "Custom-Client"
$RID = "win-x64"

Write-Host "Platform: Windows ($RID)" -ForegroundColor Green
Write-Host ""

# Step 1: Build native libraries
Write-Host "Step 1: Building native libraries..." -ForegroundColor Yellow
Write-Host "-----------------------------------" -ForegroundColor Yellow
Set-Location (Join-Path $PROJECT_ROOT "Hybrid-RSDK-Main")
if (-not (Test-Path "build")) {
    New-Item -ItemType Directory -Path "build" | Out-Null
}
Set-Location "build"
cmake .. -DCMAKE_BUILD_TYPE=Release
cmake --build . --config Release
Write-Host "✓ Native libraries built" -ForegroundColor Green
Write-Host ""

# Step 2: Build C# build tools
Write-Host "Step 2: Building C# build tools..." -ForegroundColor Yellow
Write-Host "-----------------------------------" -ForegroundColor Yellow
Set-Location (Join-Path $PROJECT_ROOT "Hybrid-RSDK-Main")
dotnet build SonicHybridRsdk.sln -c Release
Write-Host "✓ C# build tools compiled" -ForegroundColor Green
Write-Host ""

# Step 3: Build Custom Client
Write-Host "Step 3: Building Custom Client..." -ForegroundColor Yellow
Write-Host "-----------------------------------" -ForegroundColor Yellow
Set-Location $CLIENT_DIR
dotnet publish -c Release -r $RID --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true
Write-Host "✓ Custom Client built" -ForegroundColor Green
Write-Host ""

# Step 4: Create distribution directory
Write-Host "Step 4: Creating distribution directory..." -ForegroundColor Yellow
Write-Host "-----------------------------------" -ForegroundColor Yellow
if (Test-Path $OUTPUT_DIR) {
    Remove-Item -Path $OUTPUT_DIR -Recurse -Force
}
New-Item -ItemType Directory -Path $OUTPUT_DIR | Out-Null
New-Item -ItemType Directory -Path (Join-Path $OUTPUT_DIR "GameData") | Out-Null
Write-Host "✓ Distribution directory created: $OUTPUT_DIR" -ForegroundColor Green
Write-Host ""

# Step 5: Copy executable and runtime
Write-Host "Step 5: Copying executable..." -ForegroundColor Yellow
Write-Host "-----------------------------------" -ForegroundColor Yellow
$PUBLISH_DIR = Join-Path $CLIENT_DIR "bin\Release\net6.0-windows\$RID\publish"
if (-not (Test-Path $PUBLISH_DIR)) {
    $PUBLISH_DIR = Join-Path $CLIENT_DIR "bin\Release\net6.0-windows\publish"
}

$EXE_PATH = Join-Path $PUBLISH_DIR "SonicHybrid.exe"
if (Test-Path $EXE_PATH) {
    Copy-Item $EXE_PATH $OUTPUT_DIR
    Write-Host "✓ Copied SonicHybrid.exe" -ForegroundColor Green
} else {
    Write-Host "ERROR: SonicHybrid.exe not found in $PUBLISH_DIR" -ForegroundColor Red
    exit 1
}

# Copy all DLLs from publish directory
Get-ChildItem -Path $PUBLISH_DIR -Filter "*.dll" | ForEach-Object {
    Copy-Item $_.FullName $OUTPUT_DIR
}
Write-Host "✓ Copied .NET runtime libraries" -ForegroundColor Green
Write-Host ""

# Step 6: Copy native libraries
Write-Host "Step 6: Copying native libraries..." -ForegroundColor Yellow
Write-Host "-----------------------------------" -ForegroundColor Yellow

# Copy RSDK libraries
$RSDK_LIB = Join-Path $BUILD_DIR "lib\RSDKv4.dll"
if (Test-Path $RSDK_LIB) {
    Copy-Item $RSDK_LIB $OUTPUT_DIR
    Write-Host "✓ Copied RSDKv4.dll" -ForegroundColor Green
}

# Copy OxygenEngine library
$OXYGEN_LIB = Join-Path $BUILD_DIR "lib\OxygenEngine.dll"
if (Test-Path $OXYGEN_LIB) {
    Copy-Item $OXYGEN_LIB $OUTPUT_DIR
    Write-Host "✓ Copied OxygenEngine.dll" -ForegroundColor Green
}

# Copy all dependency libraries from build/bin/Release
$BIN_RELEASE = Join-Path $BUILD_DIR "bin\Release"
if (Test-Path $BIN_RELEASE) {
    Get-ChildItem -Path $BIN_RELEASE -Filter "*.dll" | ForEach-Object {
        Copy-Item $_.FullName $OUTPUT_DIR -Force
    }
    Write-Host "✓ Copied dependency libraries from bin/Release" -ForegroundColor Green
}

Write-Host ""

# Step 6.5: Copy Sonic 3 AIR executable and data files
Write-Host "Step 6.5: Copying Sonic 3 AIR files..." -ForegroundColor Yellow
Write-Host "-----------------------------------" -ForegroundColor Yellow

# Copy Sonic 3 AIR executable from build
$SONIC3AIR_EXE = Join-Path $BUILD_DIR "bin\sonic3air.exe"
if (Test-Path $SONIC3AIR_EXE) {
    Copy-Item $SONIC3AIR_EXE $OUTPUT_DIR
    Write-Host "✓ Copied sonic3air.exe" -ForegroundColor Green
} else {
    # Try alternative location
    $SONIC3AIR_EXE = Join-Path $BUILD_DIR "sonic3air\sonic3air_windows.exe"
    if (Test-Path $SONIC3AIR_EXE) {
        Copy-Item $SONIC3AIR_EXE (Join-Path $OUTPUT_DIR "sonic3air.exe")
        Write-Host "✓ Copied sonic3air.exe (from alternate location)" -ForegroundColor Green
    } else {
        Write-Host "⚠ sonic3air.exe not found - Sonic 3 will run in stub mode" -ForegroundColor Yellow
    }
}

# Copy Sonic 3 AIR data files
$S3AIR_SOURCE = Join-Path $PROJECT_ROOT "vendor\sonic3air\Oxygen\sonic3air"
$S3AIR_DATA = Join-Path $S3AIR_SOURCE "data"
$S3AIR_SCRIPTS = Join-Path $S3AIR_SOURCE "scripts"
$S3AIR_INTERNAL = Join-Path $S3AIR_SOURCE "___internal"

if (Test-Path $S3AIR_DATA) {
    $TARGET_DATA = Join-Path $OUTPUT_DIR "data"
    if (-not (Test-Path $TARGET_DATA)) {
        New-Item -ItemType Directory -Path $TARGET_DATA | Out-Null
    }
    Copy-Item -Path "$S3AIR_DATA\*" -Destination $TARGET_DATA -Recurse -Force
    Write-Host "✓ Copied Sonic 3 AIR data files" -ForegroundColor Green
}

if (Test-Path $S3AIR_SCRIPTS) {
    $TARGET_SCRIPTS = Join-Path $OUTPUT_DIR "scripts"
    if (-not (Test-Path $TARGET_SCRIPTS)) {
        New-Item -ItemType Directory -Path $TARGET_SCRIPTS | Out-Null
    }
    Copy-Item -Path "$S3AIR_SCRIPTS\*" -Destination $TARGET_SCRIPTS -Recurse -Force
    Write-Host "✓ Copied Sonic 3 AIR scripts" -ForegroundColor Green
}

if (Test-Path $S3AIR_INTERNAL) {
    $TARGET_INTERNAL = Join-Path $OUTPUT_DIR "___internal"
    if (-not (Test-Path $TARGET_INTERNAL)) {
        New-Item -ItemType Directory -Path $TARGET_INTERNAL | Out-Null
    }
    Copy-Item -Path "$S3AIR_INTERNAL\*" -Destination $TARGET_INTERNAL -Recurse -Force
    Write-Host "✓ Copied Sonic 3 AIR internal files" -ForegroundColor Green
}

Write-Host ""

# Step 7: Create GameData readme
Write-Host "Step 7: Creating GameData readme..." -ForegroundColor Yellow
Write-Host "-----------------------------------" -ForegroundColor Yellow
$README_CONTENT = @"
Sonic Hybrid Ultimate - Game Data Folder
=========================================

Place your legally obtained game files here:

Required Files:
  - sonic1.rsdk   (from Sonic 1 mobile/remaster)
  - sonic2.rsdk   (from Sonic 2 mobile/remaster)
  - soniccd.rsdk  (from Sonic CD mobile/remaster)
  - sonic3.bin    (Sonic 3 & Knuckles ROM - your legally obtained copy)

How to obtain these files:
  1. Purchase the games from Steam, Google Play, or App Store
  2. Extract the .rsdk files from your purchased games
  3. For Sonic 3, you need a Sonic 3 & Knuckles ROM file

The application will automatically detect and import game files from:
  - This folder
  - Your Documents, Desktop, and Downloads folders
  - Steam installation directories
  - Custom folders you configure (Tools → Manage Game Locations)

Once you have placed files here or in any other location, run SonicHybrid.exe!

Note: Due to copyright, we cannot provide these files. You must obtain them
legally from your own purchased copies of the games.
"@
Set-Content -Path (Join-Path $OUTPUT_DIR "GameData\README.txt") -Value $README_CONTENT
Write-Host "✓ Created GameData/README.txt" -ForegroundColor Green
Write-Host ""

# Step 8: Create launcher script
Write-Host "Step 8: Creating launcher script..." -ForegroundColor Yellow
Write-Host "-----------------------------------" -ForegroundColor Yellow
$BAT_CONTENT = @"
@echo off
REM Sonic Hybrid Ultimate launcher for Windows

cd /d "%~dp0"
SonicHybrid.exe
"@
Set-Content -Path (Join-Path $OUTPUT_DIR "run.bat") -Value $BAT_CONTENT
Write-Host "✓ Created run.bat" -ForegroundColor Green
Write-Host ""

# Step 9: Create usage documentation
Write-Host "Step 9: Creating usage documentation..." -ForegroundColor Yellow
Write-Host "-----------------------------------" -ForegroundColor Yellow
$MAIN_README = @"
========================================
   Sonic Hybrid Ultimate
========================================

Welcome to Sonic Hybrid Ultimate - A unified Sonic experience!

QUICK START:
------------
1. Run SonicHybrid.exe

2. The application will automatically scan for game files in:
   - GameData folder (this directory)
   - Your Documents, Desktop, and Downloads folders  
   - Steam installation directories
   - Custom folders (add via Tools → Manage Game Locations)

3. If no files are found, you'll be prompted to:
   - Browse for files when clicking a game button
   - Add custom search folders
   - Manually place files in the GameData folder

GAME FILES NEEDED:
------------------
You need legally obtained copies of:
  • Sonic the Hedgehog 1 (Data.rsdk → sonic1.rsdk)
  • Sonic the Hedgehog 2 (Data.rsdk → sonic2.rsdk)
  • Sonic CD (Data.rsdk → soniccd.rsdk)
  • Sonic 3 & Knuckles ROM (sonic3.bin)

These files are NOT included due to copyright.

AUTO-DETECTION:
---------------
The application automatically finds your game files!

Simply:
1. Have your .rsdk files anywhere on your PC
2. Run SonicHybrid.exe
3. The app will find and import them automatically

You can also add custom search folders:
  Tools → Manage Game Locations → Browse and Add Folder

CONTROLS:
---------
Use the in-game menu to configure controls.
Default keyboard controls are typically:
  Arrow Keys - Movement
  A/Z - Jump
  S/X - Action
  Enter - Pause

MENU OPTIONS:
-------------
  File → Exit: Close the application
  Tools → Manage Game Locations: Add/remove custom search paths
  Tools → Scan for Games Now: Re-scan all configured locations
  Help → About: Version and project information

TROUBLESHOOTING:
---------------
If the game doesn't start:
  1. Check the log window for errors
  2. Use Tools → Scan for Games Now to verify file detection
  3. Add custom folders via Tools → Manage Game Locations
  4. Manually place files in GameData folder

If you see "missing libraries" errors:
  1. Make sure all DLL files are in this folder
  2. Install Visual C++ Redistributable 2015-2022
  3. Re-download the complete package

For more information, visit:
https://github.com/Badgerworks-Brewery/Sonic-Hybrid-Ultimate

LEGAL:
------
Sonic Hybrid Ultimate is a fan project. Sonic and related characters
are property of SEGA. You must own legal copies of all games to use
this software.

This software is provided as-is without warranty. Use at your own risk.
"@
Set-Content -Path (Join-Path $OUTPUT_DIR "README.txt") -Value $MAIN_README
Write-Host "✓ Created README.txt" -ForegroundColor Green
Write-Host ""

# Final summary
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "✓ Package build complete!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Distribution created at:" -ForegroundColor Green
Write-Host "  $OUTPUT_DIR" -ForegroundColor White
Write-Host ""
Write-Host "Contents:" -ForegroundColor Green
Get-ChildItem -Path $OUTPUT_DIR | Format-Table Name, Length, LastWriteTime
Write-Host ""
Write-Host "To distribute:" -ForegroundColor Yellow
Write-Host "  1. Test the package by running SonicHybrid.exe from the dist folder" -ForegroundColor White
Write-Host "  2. Zip the SonicHybridUltimate folder" -ForegroundColor White
Write-Host "  3. Share with users (game files will be auto-detected)" -ForegroundColor White
Write-Host ""
Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
