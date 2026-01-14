# Sonic Hybrid Ultimate - Build Instructions

## Overview

Sonic Hybrid Ultimate is a hybrid engine that combines multiple RSDK (Retro Software Development Kit) versions to run various Sonic games. This build system automatically fetches the required decompilations and builds the engine without requiring game files to be present during compilation.

## Prerequisites

### Linux/Ubuntu
```bash
sudo apt-get install -y cmake build-essential pkg-config libsdl2-dev libgl1-mesa-dev libglew-dev libvorbis-dev libtinyxml2-dev libogg-dev libtheora-dev git dotnet-sdk-6.0
```

### Windows
- Visual Studio 2019 or later with C++ support
- CMake 3.15 or later
- .NET 6.0 SDK
- vcpkg (recommended for dependency management)

### macOS
```bash
brew install cmake sdl2 glew libvorbis tinyxml2 dotnet
```

## Building

### Quick Build
```bash
chmod +x build_all.sh
./build_all.sh
```

### Manual Build Steps

1. **Initialize submodules:**
   ```bash
   git submodule update --init --recursive
   ```

2. **Fetch RSDK decompilations:**
   ```bash
   ./fetch_rsdkv4.sh  # Required for Sonic 1 & 2
   ./fetch_rsdkv3.sh  # Optional for Sonic CD
   ./fetch_rsdkv5.sh  # Optional for newer games
   ```

3. **Apply Team Forever enhancements (required for video playback):**
   ```bash
   ./apply_teamforever.sh
   ```
   
   **Note**: The Team Forever patch may fail to apply due to version mismatches with RSDKv4 v1.3.3. This is expected and non-critical - the build will continue without video cutscene support, but core gameplay will work normally.

4. **Build the engine:**
   ```bash
   cd Hybrid-RSDK-Main
   mkdir build && cd build
   cmake ..
   cmake --build . --config Release
   cd ../..
   ```

5. **Build the custom client:**
   ```bash
   cd Custom-Client
   dotnet build --configuration Release
   cd ..
   ```

## RSDK Components

The build system supports three RSDK versions:

- **RSDKv4** (Required): Supports Sonic 1, Sonic 2, and Sonic CD (2011)
- **RSDKv3** (Optional): Supports original Sonic CD
- **RSDKv5** (Optional): Supports Sonic Mania and newer games

Missing optional components will result in warnings but won't prevent the build from completing.

## Game Files

**IMPORTANT**: The build system does NOT include game files due to copyright.

### For Hybrid Mode (Recommended)

To enable the unified Sonic 1 + CD + 2 experience:

1. **Obtain legally** the following Data.rsdk files:
   - Sonic CD (2011 remaster)
   - Sonic 1 (2013 mobile remaster)
   - Sonic 2 (2013 mobile remaster)

2. **Rename and place** them in `Hybrid-RSDK-Main/rsdk-source-data/`:
   ```
   rsdk-source-data/
   ├── soniccd.rsdk  (from Sonic CD)
   ├── sonic1.rsdk   (from Sonic 1)
   └── sonic2.rsdk   (from Sonic 2)
   ```

3. **Run the build** - The system will automatically:
   - Unpack and convert the games
   - Merge them into a unified experience
   - Generate `Hybrid-RSDK-Main/sonic-hybrid/Data.rsdk`

4. **Play** using:
   ```bash
   cd Hybrid-RSDK-Main/sonic-hybrid
   ./run_hybrid.sh  # Linux/macOS
   run_hybrid.bat   # Windows
   ```

See `Hybrid-RSDK-Main/rsdk-source-data/README.md` for detailed instructions on obtaining game files.

### For Individual Games (Fallback)

If you don't have all three games, you can still play them individually:

1. Place individual `Data.rsdk` files in the executable directory
2. Run the Custom Client and select the game
3. Or run `rsdkv4` directly with the data file in the same directory

### File Locations After Build

- **RSDKv4 Engine**: `Hybrid-RSDK-Main/build/bin/rsdkv4`
- **Hybrid Data**: `Hybrid-RSDK-Main/sonic-hybrid/Data.rsdk` (if generated)
- **Custom Client**: `Custom-Client/bin/Release/net6.0-windows/`
- **Sonic 3 AIR**: `Sonic 3 AIR Main/` (if configured)

### Supported Games

**Via Hybrid Mode (RSDKv4):**
- ✅ Sonic the Hedgehog (2013 remaster)
- ✅ Sonic CD (2011 remaster)
- ✅ Sonic the Hedgehog 2 (2013 remaster)

**Via Individual Mode:**
- Sonic 1, CD, 2 (as above)
- Sonic Mania / Mania Plus (if RSDKv5 is built - experimental)

**Via Sonic 3 AIR (separate):**
- Sonic 3 & Knuckles (requires ROM file and AIR setup)

## Troubleshooting

### Common Issues

1. **Team Forever patch fails to apply**
   - **Symptom**: `error: patch failed: RSDKv4/Drawing.cpp:2772` and similar errors during `apply_teamforever.sh`
   - **Cause**: The patch was created for an older RSDKv4 version and conflicts with v1.3.3
   - **Resolution**: This is expected and non-critical. The build continues automatically without video playback support. Core gameplay works normally.

2. **"TextMenu has not been declared"**
   - This should be fixed in the current version
   - If it persists, ensure all includes are properly ordered

3. **"fetch_rsdkv3.sh: No such file or directory"**
   - This should be fixed in the current version
   - Ensure you're running the build script from the project root

4. **Missing dependencies**
   - Install the prerequisites listed above
   - On Ubuntu: `sudo apt-get install -y cmake build-essential pkg-config libsdl2-dev libgl1-mesa-dev libglew-dev libvorbis-dev libtinyxml2-dev`

5. **RSDKv4.dll not found on Windows**
   - **Symptom**: `Could not find RSDKv4 library in any of the search paths`
   - **Cause**: The DLL wasn't copied to the correct output directory
   - **Resolution**: Rebuild Hybrid-RSDK-Main. The build system now copies to both `bin/Release/net6.0-windows/` and `bin/Release/net6.0-windows/win-x64/` for compatibility with .NET 6.0 RuntimeIdentifier.

6. **Git clone failures**
   - Ensure you have internet connectivity
   - Check if GitHub is accessible from your network
   - Some corporate networks may block git:// URLs

### Build Logs

The build system provides detailed logging:
- Successful fetches are logged with confirmation messages
- Failed optional components show warnings but don't stop the build
- Failed required components (RSDKv4) will stop the build with an error

## Legal Notice

This project requires you to own legal copies of the Sonic games you wish to play. The build system only compiles the open-source decompilation engines - it does not provide any copyrighted game content.

## Contributing

When contributing to the build system:
1. Test on multiple platforms if possible
2. Ensure optional dependencies remain optional
3. Maintain clear error messages for users
4. Update this documentation for any new requirements