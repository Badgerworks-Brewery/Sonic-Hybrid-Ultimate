# Main build script for Sonic Hybrid Ultimate
param(
    [switch]$Release = $false,
    [switch]$Clean = $false,
    [switch]$SkipNative = $false
)

$ErrorActionPreference = "Stop"
$rootDir = $PSScriptRoot
$buildType = if ($Release) { "Release" } else { "Debug" }

function Write-Header($text) {
    Write-Host "`n=== $text ===" -ForegroundColor Cyan
}

function Initialize-VSEnvironment {
    Write-Header "Initializing Visual Studio Environment"
    
    # Try multiple possible Visual Studio paths
    $vsInstallPaths = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\Community",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\Professional",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\Enterprise"
    )

    $vcvarsall = $null
    foreach ($vsPath in $vsInstallPaths) {
        $testPath = Join-Path $vsPath "VC\Auxiliary\Build\vcvarsall.bat"
        if (Test-Path $testPath) {
            $vcvarsall = $testPath
            break
        }
    }

    if (-not $vcvarsall) {
        Write-Host "Visual Studio 2022 with C++ support not found." -ForegroundColor Yellow
        Write-Host "You can:"
        Write-Host "1. Install Visual Studio 2022 with C++ workload from: https://visualstudio.microsoft.com/"
        Write-Host "2. Run this script with -SkipNative to skip building native components"
        if (-not $SkipNative) {
            throw "Cannot build native components without Visual Studio. Install Visual Studio or use -SkipNative."
        }
        return $false
    }

    Write-Host "Found Visual Studio at: $vcvarsall"
    
    # Import VS environment variables
    $tempFile = [System.IO.Path]::GetTempFileName()
    cmd /c "call `"$vcvarsall`" amd64 && set > `"$tempFile`""
    Get-Content $tempFile | ForEach-Object {
        if ($_ -match "^(.*?)=(.*)$") {
            $name = $matches[1]
            $value = $matches[2]
            [System.Environment]::SetEnvironmentVariable($name, $value, [System.EnvironmentVariableTarget]::Process)
        }
    }
    Remove-Item $tempFile
    return $true
}

function Verify-Dependencies {
    Write-Header "Checking Dependencies"
    
    # Check .NET SDK
    $dotnetVersion = (dotnet --version)
    if (-not $?) {
        throw ".NET SDK not found. Please install .NET 6.0 or later."
    }
    Write-Host ".NET SDK Version: $dotnetVersion"

    # Check CMake if building native components
    if (-not $SkipNative) {
        $cmakeVersion = (cmake --version | Select-Object -First 1)
        if (-not $?) {
            throw "CMake not found. Please install CMake 3.15 or later."
        }
        Write-Host "CMake Version: $cmakeVersion"
    }
}

function Setup-Directories {
    Write-Header "Setting up directories"
    
    # Create necessary directories
    $dirs = @(
        "Hybrid-RSDK Main/Data",
        "Custom Client/bin"
    )

    # Add native build directories if not skipping
    if (-not $SkipNative) {
        $dirs += @(
            "Hybrid-RSDK Main/build",
            "Sonic 3 AIR Main/Oxygen"
        )
    }

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
        
        # Copy RSDK and Oxygen DLLs to output if they exist
        $outputDir = "bin/$buildType/net6.0-windows"
        if (-not $SkipNative) {
            if (Test-Path "$rootDir/Hybrid-RSDK Main/build/bin") {
                Copy-Item -Path "$rootDir/Hybrid-RSDK Main/build/bin/*.dll" -Destination $outputDir -Force
            }
            if (Test-Path "$rootDir/Sonic 3 AIR Main/Oxygen") {
                Copy-Item -Path "$rootDir/Sonic 3 AIR Main/Oxygen/*.dll" -Destination $outputDir -Force
            }
        }
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
        
        # Configure with Visual Studio generator
        cmake -B $buildDir -G "Visual Studio 17 2022" -A x64 -DCMAKE_BUILD_TYPE=$buildType
        
        # Build using CMake --build
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
    
    if ($SkipNative) {
        Write-Host "Note: Native components were skipped. Install Visual Studio 2022 with C++ support to enable full functionality." -ForegroundColor Yellow
    }
}

try {
    # Main build process
    Verify-Dependencies
    $hasVS = Initialize-VSEnvironment
    Setup-Directories
    
    if ($Clean) {
        Write-Header "Cleaning previous builds"
    }

    # Build components
    if (-not $SkipNative -and $hasVS) {
        Build-HybridRSDK  # Build RSDK first as Custom Client depends on it
    }
    Build-CustomClient
    Write-Instructions
}
catch {
    Write-Host "Build failed: $_" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace
    exit 1
}
