# Single Executable Build Instructions

## Overview

Sonic Hybrid Ultimate now builds as a **single self-contained executable** that includes:
- Custom Client frontend
- RSDKv4 library and all dependencies (SDL2, GLEW, OGG, Vorbis, Theora)
- RSDKv3 library (for Sonic CD)
- Hybrid-RSDK integration
- All game engines embedded

## Building the Single Executable

### Prerequisites
- .NET 6.0 SDK
- CMake and C++ compiler
- All dependencies listed in BUILD_INSTRUCTIONS.md

### Build Steps

#### Option 1: Using the publish script (Recommended)
```bash
chmod +x publish.sh
./publish.sh
```

#### Option 2: Manual build
```bash
# Build native libraries first
cd Hybrid-RSDK-Main
./build_all.sh

# Publish as single executable
cd ../Custom-Client
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:IncludeAllContentForSelfExtract=true
```

### Output Location
After building, the single executable will be located at:
- **Windows**: `Custom-Client/bin/Release/net6.0-windows/win-x64/publish/SonicHybrid.exe`
- **Linux**: `Custom-Client/bin/Release/net6.0-windows/linux-x64/publish/SonicHybrid`

## What's Included

The single executable bundles:
1. **.NET Runtime** - Self-contained, no installation required
2. **RSDKv4.dll** - Native RSDK library for Sonic 1, 2
3. **All Dependencies** - SDL2, GLEW, OpenGL, OGG, Vorbis, Theora libraries
4. **RSDKv3 Core** - For Sonic CD support
5. **Hybrid-RSDK** - Game transition management
6. **Custom Client** - Frontend UI

## Configuration

The executable extracts native libraries to a temporary directory on first run. This is handled automatically by .NET's single-file publishing.

### Runtime Extraction
- Native DLLs are extracted to: `%TEMP%\.net\SonicHybrid\<random-id>\`
- Extraction happens once per version
- No manual setup required

## File Size
The single executable will be approximately 100-150MB due to bundled dependencies and self-contained runtime.

## Troubleshooting

### "Missing native library" errors
- Ensure you built the native libraries first (`./build_all.sh` in Hybrid-RSDK-Main)
- Check that `PublishSingleFile=true` and `IncludeNativeLibrariesForSelfExtract=true` are set

### Game files not loading
- Place your game `.rsdk` files in the same directory as the executable, or
- Configure the path in the Custom Client UI

## Platform-Specific Builds

### Windows (x64)
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

### Linux (x64)
```bash
dotnet publish -c Release -r linux-x64 --self-contained
```

### macOS (x64)
```bash
dotnet publish -c Release -r osx-x64 --self-contained
```

## Distribution

The single executable can be distributed standalone:
1. Copy `SonicHybrid.exe` from the publish directory
2. No additional files needed (runtime and dependencies are embedded)
3. Users can run it directly - no installation required

## Benefits

✅ **Single file** - Easy to distribute and run  
✅ **No dependencies** - .NET runtime and native libraries bundled  
✅ **No installation** - Just download and run  
✅ **Self-contained** - Works on systems without .NET installed  
✅ **Portable** - Can run from USB drive or any location  

## Technical Details

### Publishing Configuration (CustomClient.csproj)
```xml
<PublishSingleFile>true</PublishSingleFile>
<SelfContained>true</SelfContained>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<IncludeAllContentForSelfExtract>true</IncludeAllContentForSelfExtract>
```

These settings:
- Bundle everything into a single .exe file
- Include the .NET runtime (no installation needed)
- Extract native DLLs at runtime automatically
- Include all content files in the bundle
