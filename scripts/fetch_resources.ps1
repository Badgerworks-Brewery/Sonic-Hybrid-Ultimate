$baseUrl = "https://raw.githubusercontent.com/RSDKModding/RSDKv4-Decompilation/main/RSDKv4"
$files = @(
    "resource.h",
    "RSDKv4.rc",
    "RSDKv4.vcxproj",
    "RSDKv4.vcxproj.filters",
    "Scene3D.hpp",
    "Scene3D.cpp",
    "Reader.hpp",
    "Reader.cpp",
    "Test.hpp",
    "Test.cpp",
    "Userdata.hpp",
    "Userdata.cpp"
)

$targetDir = "Hybrid-RSDK Main/RSDKV4/RSDKV4"
if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force
}

foreach ($file in $files) {
    $url = "$baseUrl/$file"
    $output = Join-Path $targetDir $file
    Write-Host "Downloading $file from $url"
    try {
        Invoke-WebRequest -Uri $url -OutFile $output -ErrorAction Stop
        Write-Host "Successfully downloaded $file"
    }
    catch {
        Write-Host "Failed to download $file"
        Write-Host $_.Exception.Message
    }
    Start-Sleep -Milliseconds 100
}
