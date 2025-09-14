# Sonic Hybrid Ultimate Build Script (Windows)
Write-Host "Building Sonic Hybrid Ultimate..." -ForegroundColor Green

# Check if we're in the right directory
if (-not (Test-Path "README.md")) {
    Write-Host "Error: Please run this script from the project root directory" -ForegroundColor Red
    exit 1
}

# Fetch RSDK decompilations
Write-Host "Fetching RSDK decompilations..." -ForegroundColor Yellow

if (-not (Test-Path "Hybrid-RSDK Main/RSDKV4-Decompilation")) {
    Write-Host "Cloning RSDKv4 Decompilation..."
    git clone https://github.com/RSDKModding/RSDKv4-Decompilation.git "Hybrid-RSDK Main/RSDKV4-Decompilation"
}

if (-not (Test-Path "Hybrid-RSDK Main/RSDKV3")) {
    Write-Host "Cloning RSDKv3 Decompilation..."
    git clone https://github.com/RSDKModding/RSDKv3-Decompilation.git "Hybrid-RSDK Main/RSDKV3"
}

if (-not (Test-Path "Hybrid-RSDK Main/RSDKV5")) {
    Write-Host "Cloning RSDKv5 Decompilation..."
    git clone https://github.com/RSDKModding/RSDKv5-Decompilation.git "Hybrid-RSDK Main/RSDKV5"
}

# Build Hybrid RSDK engine
Write-Host "Building Hybrid RSDK engine..." -ForegroundColor Yellow
Set-Location "Hybrid-RSDK Main"

# Clean previous build
if (Test-Path "build") {
    Remove-Item -Recurse -Force "build"
}
New-Item -ItemType Directory -Path "build" -Force | Out-Null
Set-Location "build"

# Configure and build
cmake ..
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: CMake configuration failed" -ForegroundColor Red
    exit 1
}

cmake --build . --config Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Build failed" -ForegroundColor Red
    exit 1
}

Set-Location "../.."

# Build Custom Client
Write-Host "Building Custom Client..." -ForegroundColor Yellow
Set-Location "Custom Client"
dotnet build --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Custom Client build failed" -ForegroundColor Red
    exit 1
}

Set-Location ".."

Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "Executables are located in:" -ForegroundColor Cyan
Write-Host "  - Hybrid-RSDK Main/build/bin/" -ForegroundColor White
Write-Host "  - Custom Client/bin/" -ForegroundColor White
