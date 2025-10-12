# Sonic Hybrid Ultimate - Build Instructions

## Overview

Sonic Hybrid Ultimate is a hybrid engine that combines multiple RSDK (Retro Software Development Kit) versions to run various Sonic games. This build system automatically fetches the required decompilations and builds the engine without requiring game files to be present during compilation.

## Prerequisites

### Linux/Ubuntu
```bash
sudo apt-get install -y cmake build-essential pkg-config libsdl2-dev libgl1-mesa-dev libglew-dev libvorbis-dev libtinyxml2-dev git dotnet-sdk-6.0
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

3. **Build the engine:**
   ```bash
   cd Hybrid-RSDK-Main
   mkdir build && cd build
   cmake ..
   cmake --build . --config Release
   cd ../..
   ```

4. **Build the custom client:**
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

**IMPORTANT**: The build system does NOT include game files. After building, you need to:

1. Obtain `Data.rsdk` files from your legally purchased copies of the games
2. Place these files in the same directory as the built executables

### File Locations
- **Executables**: `Hybrid-RSDK-Main/build/bin/`
- **Custom Client**: `Custom-Client/bin/`

### Supported Games
- Sonic the Hedgehog (2013)
- Sonic the Hedgehog 2 (2013)
- Sonic CD (2011)
- Sonic Mania (if RSDKv5 is built)
- Sonic Mania Plus (if RSDKv5 is built)

## Troubleshooting

### Common Issues

1. **"TextMenu has not been declared"**
   - This should be fixed in the current version
   - If it persists, ensure all includes are properly ordered

2. **"fetch_rsdkv3.sh: No such file or directory"**
   - This should be fixed in the current version
   - Ensure you're running the build script from the project root

3. **Missing dependencies**
   - Install the prerequisites listed above
   - On Ubuntu: `sudo apt-get install -y cmake build-essential pkg-config libsdl2-dev libgl1-mesa-dev libglew-dev libvorbis-dev libtinyxml2-dev`

4. **Git clone failures**
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