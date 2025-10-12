$baseUrl = "https://raw.githubusercontent.com/RSDKModding/RSDKv4-Decompilation/main/RSDKv4"
$files = @(
    # Core Engine Files
    "RetroEngine.hpp",
    "RetroEngine.cpp",
    # Scene Management
    "Scene.hpp",
    "Scene.cpp",
    "Scene3D.hpp",
    "Scene3D.cpp",
    # Graphics and Animation
    "Animation.hpp",
    "Animation.cpp",
    "Drawing.hpp",
    "Drawing.cpp",
    "Palette.hpp",
    "Palette.cpp",
    "Renderer.hpp",
    "Renderer.cpp",
    "Sprite.hpp",
    "Sprite.cpp",
    # Audio
    "Audio.hpp",
    "Audio.cpp",
    # Input and Control
    "Input.hpp",
    "Input.cpp",
    # Scripting and Logic
    "Script.hpp",
    "Script.cpp",
    # Networking
    "Networking.hpp",
    "Networking.cpp",
    # Debug and Testing
    "Debug.hpp",
    "Debug.cpp",
    # Math and Collision
    "Math.hpp",
    "Math.cpp",
    "Collision.hpp",
    "Collision.cpp",
    # Object Management
    "Object.hpp",
    "Object.cpp",
    # Resource Management
    "Reader.hpp",
    "Reader.cpp",
    "String.hpp",
    "String.cpp",
    # Mod Support
    "ModAPI.hpp",
    "ModAPI.cpp",
    # Testing
    "Test.hpp",
    "Test.cpp",
    # User Data
    "Userdata.hpp",
    "Userdata.cpp",
    # Resource Files
    "resource.h",
    "RSDKv4.rc",
    # Filters and vcproj files for reference
    "RSDKv4.vcxproj",
    "RSDKv4.vcxproj.filters"
)

$targetDir = "Hybrid-RSDK-Main/RSDKV4/RSDKV4"
if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force
}

foreach ($file in $files) {
    $url = "$baseUrl/$file"
    $output = Join-Path $targetDir $file
    Write-Host "Downloading $file..."
    try {
        Invoke-WebRequest -Uri $url -OutFile $output
        Write-Host "Successfully downloaded $file"
    }
    catch {
        Write-Host "Failed to download $file: $($_.Exception.Message)"
    }
}
