# Sonic Hybrid Ultimate Build Script (Windows)
Write-Host "Building Sonic Hybrid Ultimate..." -ForegroundColor Green

# Check if we're in the right directory
if (-not (Test-Path "README.md")) {
    Write-Host "Error: Please run this script from the project root directory" -ForegroundColor Red
    exit 1
}

# Check for required tools
Write-Host "Checking for required tools..." -ForegroundColor Yellow
if (-not (Get-Command cmake -ErrorAction SilentlyContinue)) {
    Write-Host "Error: CMake is required but not installed." -ForegroundColor Red
    Write-Host "Please install CMake (https://cmake.org/download/)." -ForegroundColor Red
    exit 1
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "Error: .NET 6.0 SDK is required but not installed." -ForegroundColor Red
    Write-Host "Please install .NET 6.0 SDK (https://dotnet.microsoft.com/download/dotnet/6.0)." -ForegroundColor Red
    exit 1
}

# Ensure vcpkg is bootstrapped
Write-Host "Ensuring vcpkg is ready..." -ForegroundColor Yellow
$vcpkgRoot = Join-Path $PSScriptRoot "vcpkg"
if (-not (Test-Path $vcpkgRoot)) {
    Write-Host "Cloning vcpkg..." -ForegroundColor Cyan
    git clone https://github.com/microsoft/vcpkg.git $vcpkgRoot
}
Set-Location $vcpkgRoot
if (-not (Test-Path "bootstrap-vcpkg.bat")) {
    Write-Host "Error: vcpkg bootstrap script not found." -ForegroundColor Red
    exit 1
}
Write-Host "Bootstrapping vcpkg..." -ForegroundColor Cyan
& .\bootstrap-vcpkg.bat
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: vcpkg bootstrap failed" -ForegroundColor Red
    exit 1
}
Set-Location $PSScriptRoot

# Initialize submodules
# The RSDKv4 decompilation is the actual engine source (RSDKV4-Decompilation is a
# git submodule). Without this the CMake glob finds no sources and rsdk_core is empty.
Write-Host "Initializing submodules..." -ForegroundColor Yellow
git submodule update --init --recursive
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: git submodule update failed" -ForegroundColor Red
    exit 1
}

# Fetch RSDK decompilations
Write-Host "Fetching RSDK decompilations..." -ForegroundColor Yellow

if (-not (Test-Path "Hybrid-RSDK-Main/RSDKV4-Decompilation/RSDKv4")) {
    Write-Host "Error: Hybrid-RSDK-Main/RSDKV4-Decompilation is empty." -ForegroundColor Red
    Write-Host "The engine sources are a git submodule. Run:" -ForegroundColor Red
    Write-Host "  git submodule update --init --recursive" -ForegroundColor Red
    exit 1
}

# Build Hybrid RSDK engine
#
# Configure from the repository root rather than Hybrid-RSDK-Main/build:
#  - the root CMakeLists.txt also pulls in vendor/theoraplay, which the engine needs
#  - run_hybrid.bat looks for the engine at <repo-root>\build\bin\Release\rsdkv4.exe
#  - CMakePresets.json already targets ${sourceDir}/build
Write-Host "Building Hybrid RSDK engine..." -ForegroundColor Yellow

# Clean previous build
if (Test-Path "build") {
    Remove-Item -Recurse -Force "build"
}
New-Item -ItemType Directory -Path "build" -Force | Out-Null

# Configure and build
$vcpkgDir = Join-Path $PSScriptRoot "vcpkg"
$vcpkgScriptsDir = Join-Path $vcpkgDir "scripts"
$vcpkgBuildsystemsDir = Join-Path $vcpkgScriptsDir "buildsystems"
$vcpkgToolchain = Join-Path $vcpkgBuildsystemsDir "vcpkg.cmake"
Write-Host "Using vcpkg toolchain: $vcpkgToolchain" -ForegroundColor Cyan

# Set environment variable for vcpkg manifest mode
$env:VCPKG_ROOT = $vcpkgDir
$env:VCPKG_INSTALLED_DIR = Join-Path $PSScriptRoot "vcpkg_installed"

Write-Host "Configuring CMake..." -ForegroundColor Cyan
cmake -S . -B build `
    -DCMAKE_TOOLCHAIN_FILE="$vcpkgToolchain" `
    -DVCPKG_TARGET_TRIPLET=x64-windows `
    -DVCPKG_MANIFEST_MODE=ON `
    -DVCPKG_MANIFEST_DIR="$PSScriptRoot" `
    -DVCPKG_INSTALLED_DIR="$env:VCPKG_INSTALLED_DIR" `
    -DBUILD_SONIC3AIR=OFF

if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: CMake configuration failed" -ForegroundColor Red
    exit 1
}

cmake --build build --config Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Build failed" -ForegroundColor Red
    exit 1
}

# Generate the unified hybrid Data.rsdk (S1 + SCD + S2) if the source files are present.
Write-Host ""
Write-Host "Checking for hybrid data generation..." -ForegroundColor Yellow
$srcDir = Join-Path $PSScriptRoot "Hybrid-RSDK-Main/rsdk-source-data"
if ((Test-Path (Join-Path $srcDir "soniccd.rsdk")) -and
    (Test-Path (Join-Path $srcDir "sonic1.rsdk")) -and
    (Test-Path (Join-Path $srcDir "sonic2.rsdk"))) {

    Write-Host "Source .rsdk files found - generating unified Data.rsdk..." -ForegroundColor Cyan
    Push-Location (Join-Path $PSScriptRoot "Hybrid-RSDK-Main")
    try {
        dotnet run --project SonicHybridRsdk.Build/SonicHybridRsdk.Build.csproj -c Release
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Hybrid data generated: Hybrid-RSDK-Main/sonic-hybrid/Data.rsdk" -ForegroundColor Green
        } else {
            Write-Host "Error: hybrid data generation failed" -ForegroundColor Red
            exit 1
        }
    } finally {
        Pop-Location
    }
} else {
    Write-Host "Source .rsdk files not found in Hybrid-RSDK-Main/rsdk-source-data/ - skipping data generation." -ForegroundColor Yellow
    Write-Host "Place soniccd.rsdk, sonic1.rsdk and sonic2.rsdk there to enable hybrid mode." -ForegroundColor Yellow
}

# Build Custom Client
Write-Host "Building Custom Client..." -ForegroundColor Yellow
Set-Location "Custom-Client"
dotnet build --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Custom Client build failed" -ForegroundColor Red
    exit 1
}

Set-Location ".."

Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "Executables are located in:" -ForegroundColor Cyan
Write-Host "  - build/bin/Release/" -ForegroundColor White
Write-Host "  - Custom-Client/bin/" -ForegroundColor White
