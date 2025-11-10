# build_native_libs.ps1 - Build native libraries required by Custom-Client (Windows)

param(
    [string]$Configuration = "Release"
)

Write-Host "Building native libraries for Sonic Hybrid Ultimate..." -ForegroundColor Green

# Check if we're in the right directory
if (-not (Test-Path "CMakeLists.txt")) {
    Write-Error "CMakeLists.txt not found. Please run this script from the project root."
    exit 1
}

# Create build directory
$BuildDir = "build"
if (-not (Test-Path $BuildDir)) {
    Write-Host "Creating build directory..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $BuildDir | Out-Null
}

Set-Location $BuildDir

try {
    # Configure with CMake
    Write-Host "Configuring build with CMake..." -ForegroundColor Yellow
    & cmake .. -DCMAKE_BUILD_TYPE=$Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "CMake configuration failed"
    }

    # Build the project
    Write-Host "Building native libraries..." -ForegroundColor Yellow
    & cmake --build . --config $Configuration --target OxygenEngine --target RSDKv4
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed"
    }

    # Check if libraries were built successfully
    $OxygenLib = $null
    $RsdkLib = $null

    $PossibleOxygenPaths = @(
        "lib\OxygenEngine.dll",
        "bin\OxygenEngine.dll",
        "$Configuration\OxygenEngine.dll",
        "lib\$Configuration\OxygenEngine.dll",
        "bin\$Configuration\OxygenEngine.dll"
    )

    $PossibleRsdkPaths = @(
        "lib\RSDKv4.dll",
        "bin\RSDKv4.dll", 
        "$Configuration\RSDKv4.dll",
        "lib\$Configuration\RSDKv4.dll",
        "bin\$Configuration\RSDKv4.dll"
    )

    foreach ($path in $PossibleOxygenPaths) {
        if (Test-Path $path) {
            $OxygenLib = $path
            break
        }
    }

    foreach ($path in $PossibleRsdkPaths) {
        if (Test-Path $path) {
            $RsdkLib = $path
            break
        }
    }

    if (-not $OxygenLib) {
        Write-Warning "OxygenEngine library not found after build"
        Write-Host "Searched paths:" -ForegroundColor Yellow
        foreach ($path in $PossibleOxygenPaths) {
            Write-Host "  - $path" -ForegroundColor Gray
        }
    } else {
        Write-Host "✓ OxygenEngine library built: $OxygenLib" -ForegroundColor Green
    }

    if (-not $RsdkLib) {
        Write-Warning "RSDKv4 library not found after build"
        Write-Host "Searched paths:" -ForegroundColor Yellow
        foreach ($path in $PossibleRsdkPaths) {
            Write-Host "  - $path" -ForegroundColor Gray
        }
    } else {
        Write-Host "✓ RSDKv4 library built: $RsdkLib" -ForegroundColor Green
    }

    # Copy libraries to Custom-Client directory if they exist
    $CustomClientDir = "..\Custom-Client"
    
    if ($OxygenLib -and (Test-Path $OxygenLib)) {
        Write-Host "Copying OxygenEngine library to Custom-Client..." -ForegroundColor Yellow
        
        $TargetDirs = @(
            "$CustomClientDir\bin\Release\net6.0-windows",
            "$CustomClientDir\bin\Debug\net6.0-windows",
            "$CustomClientDir\bin\Release\net6.0-windows\win-x64",
            "$CustomClientDir\bin\Debug\net6.0-windows\win-x64"
        )
        
        foreach ($dir in $TargetDirs) {
            if (-not (Test-Path $dir)) {
                New-Item -ItemType Directory -Path $dir -Force | Out-Null
            }
            Copy-Item $OxygenLib $dir -Force
            Write-Host "  Copied to: $dir" -ForegroundColor Gray
        }
    }

    if ($RsdkLib -and (Test-Path $RsdkLib)) {
        Write-Host "Copying RSDKv4 library to Custom-Client..." -ForegroundColor Yellow
        
        $TargetDirs = @(
            "$CustomClientDir\bin\Release\net6.0-windows",
            "$CustomClientDir\bin\Debug\net6.0-windows",
            "$CustomClientDir\bin\Release\net6.0-windows\win-x64",
            "$CustomClientDir\bin\Debug\net6.0-windows\win-x64"
        )
        
        foreach ($dir in $TargetDirs) {
            if (-not (Test-Path $dir)) {
                New-Item -ItemType Directory -Path $dir -Force | Out-Null
            }
            Copy-Item $RsdkLib $dir -Force
            Write-Host "  Copied to: $dir" -ForegroundColor Gray
        }
    }

    Write-Host "Native library build complete!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "1. Build the Custom-Client C# application" -ForegroundColor White
    Write-Host "2. Ensure Sonic 3 AIR executable is available if you want to use Sonic 3 & Knuckles" -ForegroundColor White
    Write-Host "3. Place .rsdk files in the expected locations or select them when prompted" -ForegroundColor White

} catch {
    Write-Error "Build failed: $_"
    exit 1
} finally {
    Set-Location ..
}