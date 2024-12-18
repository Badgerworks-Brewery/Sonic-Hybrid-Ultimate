# Main build script for Sonic Hybrid Ultimate
param(
    [switch]$Release = $false,
    [switch]$Clean = $false
)

$ErrorActionPreference = "Stop"
$rootDir = $PSScriptRoot
$buildType = if ($Release) { "Release" } else { "Debug" }

function Write-Header($text) {
    Write-Host "`n=== $text ===" -ForegroundColor Cyan
}

function Verify-Dependencies {
    Write-Header "Checking Dependencies"
    
    # Check .NET SDK
    $dotnetVersion = (dotnet --version)
    if (-not $?) {
        throw ".NET SDK not found. Please install .NET 6.0 or later."
    }
    Write-Host ".NET SDK Version: $dotnetVersion"

    # Check CMake
    $cmakeVersion = (cmake --version | Select-Object -First 1)
    if (-not $?) {
        throw "CMake not found. Please install CMake 3.15 or later."
    }
    Write-Host "CMake Version: $cmakeVersion"

    # Check Visual Studio
    $vsPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath
    if (-not $vsPath) {
        throw "Visual Studio not found. Please install Visual Studio 2022 with C++ support."
    }
    Write-Host "Visual Studio Path: $vsPath"
}

function Setup-Directories {
    Write-Header "Setting up directories"
    
    # Create necessary directories
    $dirs = @(
        "Hybrid-RSDK Main/Data",
        "Hybrid-RSDK Main/build",
        "Custom Client/bin",
        "Sonic 3 AIR Main/Oxygen"
    )

    foreach ($dir in $dirs) {
        $path = Join-Path $rootDir $dir
        if (-not (Test-Path $path)) {
            New-Item -ItemType Directory -Path $path -Force
            Write-Host "Created directory: $path"
        }
    }
}

function Build-CustomClient {
    Write-Header "Building Custom Client"
    Push-Location "$rootDir/Custom Client"
    try {
        if ($Clean) {
            dotnet clean
        }
        dotnet build -c $buildType
        
        # Copy RSDK and Oxygen DLLs to output
        $outputDir = "bin/$buildType/net6.0-windows"
        Copy-Item -Path "$rootDir/Hybrid-RSDK Main/build/bin/*.dll" -Destination $outputDir -Force
        Copy-Item -Path "$rootDir/Sonic 3 AIR Main/Oxygen/*.dll" -Destination $outputDir -Force
    }
    finally {
        Pop-Location
    }
}

function Build-HybridRSDK {
    Write-Header "Building Hybrid RSDK"
    Push-Location "$rootDir/Hybrid-RSDK Main"
    try {
        $buildDir = "build"
        if ($Clean -and (Test-Path $buildDir)) {
            Remove-Item -Recurse -Force $buildDir
        }
        
        cmake -B $buildDir -DCMAKE_BUILD_TYPE=$buildType
        cmake --build $buildDir --config $buildType
    }
    finally {
        Pop-Location
    }
}

function Write-Instructions {
    Write-Host "`nBuild complete! Follow these steps to run:" -ForegroundColor Green
    Write-Host "1. Place your game files in 'Hybrid-RSDK Main/Data':"
    Write-Host "   - Sonic 1's Data.rsdk as 'sonic1.rsdk'"
    Write-Host "   - Sonic 2's Data.rsdk as 'sonic2.rsdk'"
    Write-Host "   - Sonic CD's Data.rsdk as 'soniccd.rsdk'"
    Write-Host "   - Sonic 3&K ROM as 'sonic3.bin'"
    Write-Host "2. Run the client:"
    Write-Host "   cd 'Custom Client/bin/$buildType/net6.0-windows'"
    Write-Host "   ./SonicHybridUltimate.exe`n"
}

try {
    # Main build process
    Verify-Dependencies
    Setup-Directories
    
    if ($Clean) {
        Write-Header "Cleaning previous builds"
    }

    # Build components
    Build-HybridRSDK  # Build RSDK first as Custom Client depends on it
    Build-CustomClient
    Write-Instructions
}
catch {
    Write-Host "Build failed: $_" -ForegroundColor Red
    exit 1
}
