# Sonic Hybrid Ultimate - Diagnostic Script
param(
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Continue"

function Write-Status($message, $status) {
    $color = switch ($status) {
        "OK" { "Green" }
        "WARN" { "Yellow" }
        "ERROR" { "Red" }
        default { "White" }
    }
    Write-Host "[$status] $message" -ForegroundColor $color
}

function Test-Dependency($name, $command, $expectedPattern = $null) {
    try {
        $output = Invoke-Expression $command 2>&1
        if ($LASTEXITCODE -eq 0 -or $output) {
            if ($expectedPattern -and $output -notmatch $expectedPattern) {
                Write-Status "$name found but version may be incompatible: $output" "WARN"
            } else {
                Write-Status "$name is available" "OK"
                if ($Verbose) { Write-Host "  Output: $output" -ForegroundColor Gray }
            }
            return $true
        }
    }
    catch {
        Write-Status "$name is not available: $_" "ERROR"
        return $false
    }
    Write-Status "$name is not available" "ERROR"
    return $false
}

function Test-FileExists($path, $description) {
    if (Test-Path $path) {
        Write-Status "$description exists: $path" "OK"
        return $true
    } else {
        Write-Status "$description missing: $path" "ERROR"
        return $false
    }
}

Write-Host "=== Sonic Hybrid Ultimate Diagnostic ===" -ForegroundColor Cyan
Write-Host ""

# Check dependencies
Write-Host "Checking Dependencies:" -ForegroundColor Yellow
$dotnetOk = Test-Dependency ".NET SDK" "dotnet --version" "^[6-9]\."
$cmakeOk = Test-Dependency "CMake" "cmake --version" "version [3-9]\."
$vsOk = Test-Dependency "Visual Studio" "where msbuild"

Write-Host ""

# Check project structure
Write-Host "Checking Project Structure:" -ForegroundColor Yellow
$projectOk = $true
$projectOk = Test-FileExists "Custom Client/CustomClient.csproj" "Custom Client project" -and $projectOk
$projectOk = Test-FileExists "Hybrid-RSDK Main/CMakeLists.txt" "Hybrid-RSDK CMake" -and $projectOk
$projectOk = Test-FileExists "Sonic 3 AIR Main" "Sonic 3 AIR directory" -and $projectOk
$projectOk = Test-FileExists "build.ps1" "Build script" -and $projectOk

Write-Host ""

# Check game data
Write-Host "Checking Game Data:" -ForegroundColor Yellow
$dataDir = "Hybrid-RSDK Main/Data"
$gameDataOk = $true
$gameDataOk = Test-FileExists "$dataDir/sonic1.rsdk" "Sonic 1 data" -and $gameDataOk
$gameDataOk = Test-FileExists "$dataDir/sonic2.rsdk" "Sonic 2 data" -and $gameDataOk
$gameDataOk = Test-FileExists "$dataDir/soniccd.rsdk" "Sonic CD data" -and $gameDataOk
$gameDataOk = Test-FileExists "$dataDir/sonic3.bin" "Sonic 3&K ROM" -and $gameDataOk

Write-Host ""

# Summary
Write-Host "=== Summary ===" -ForegroundColor Cyan
if ($dotnetOk -and $projectOk) {
    Write-Status "Ready to build Custom Client" "OK"
} else {
    Write-Status "Cannot build Custom Client - missing dependencies" "ERROR"
}

if ($cmakeOk -and $vsOk -and $projectOk) {
    Write-Status "Ready to build native components" "OK"
} else {
    Write-Status "Cannot build native components - use -SkipNative flag" "WARN"
}

if ($gameDataOk) {
    Write-Status "Game data is ready" "OK"
} else {
    Write-Status "Game data missing - see GETTING_STARTED.md for instructions" "ERROR"
}

Write-Host ""
Write-Host "Run './build.ps1' to build the project" -ForegroundColor Green
Write-Host "Run './build.ps1 -SkipNative' if you don't have Visual Studio" -ForegroundColor Yellow
