# Sonic Hybrid Ultimate

A comprehensive Sonic the Hedgehog experience that combines multiple games into one seamless adventure.

![Sonic 1 in Sonic 2](docs/preview.png)

## Overview

This project combines the RSDK (Retro Engine) decompilations with Sonic 3 AIR's Oxygen Engine to create a unified Sonic experience. Instead of mixing engines directly, the project uses a custom frontend that manages transitions between different game engines.

## Architecture

The project consists of three main components:

1. **Hybrid-RSDK Engine**: Enhanced RSDK engine supporting multiple versions (v3, v4, v5)
2. **Sonic 3 AIR Integration**: Oxygen Engine for Sonic 3 & Knuckles
3. **Custom Client**: Frontend that manages game transitions and provides a unified interface

## Build Process

### Prerequisites

- **Linux/macOS**: CMake, SDL2, OpenGL, GLEW, Vorbis libraries
- **Windows**: Visual Studio 2019+ or Build Tools
- **.NET 6.0 SDK** for the Custom Client
- **Git** for fetching submodules and dependencies

### Quick Build

```bash
# Make the build script executable
chmod +x build_clean.sh

# Run the build script
./build_clean.sh
```

### Manual Build

1. **Install Dependencies**:
   ```bash
   # Ubuntu/Debian
   sudo apt-get install cmake build-essential pkg-config libsdl2-dev libgl1-mesa-dev libglew-dev libvorbis-dev

   # macOS (with Homebrew)
   brew install cmake sdl2 glew libvorbis pkg-config
   ```

2. **Fetch RSDK Decompilations**:
   ```bash
   ./fetch_rsdkv3.sh  # For Sonic CD
   ./fetch_rsdkv5.sh  # Universal RSDK engine
   ```

3. **Build Hybrid-RSDK**:
   ```bash
   cd "Hybrid-RSDK Main"
   mkdir build && cd build
   cmake .. -DCMAKE_BUILD_TYPE=Release
   cmake --build . --config Release
   ```

4. **Build Custom Client**:
   ```bash
   cd "Custom Client"
   dotnet restore
   dotnet build --configuration Release
   ```

## Setup

1. **Obtain Game Data Files**:
   - `sonic1.rsdk` from Sonic 1 (2013 version)
   - `sonic2.rsdk` from Sonic 2 (2013 version)
   - `soniccd.rsdk` from Sonic CD
   - `sonic3.bin` from Sonic 3 & Knuckles ROM

2. **Place Data Files**:
   ```
   Hybrid-RSDK Main/rsdk-source-data/
   ├── sonic1.rsdk
   ├── sonic2.rsdk
   ├── soniccd.rsdk
   └── sonic3.bin
   ```

3. **Run the Custom Client**:
   ```bash
   cd "Custom Client/bin/Release/net6.0-windows"
   ./SonicHybrid.exe
   ```

## Features

* Play Sonic 1, Sonic CD, Sonic 2 and Sonic 3&K as a single big game.
* Star Posts in Sonic the Hedgehog 1 and CD will bring you to the Sonic the Hedgehog 2 special stages.
* Completing Sonic the Hedgehog 1's Final Zone will bring you to Palmtree Panic Zone.
* Completing Sonic the Hedgehog CD's Metallic Madness Act 3 will bring you to Emerald Hill Zone.
* Completing Death Egg Zone in Sonic the Hedgehog 2 will bring you to Angel Island Zone.
* The Stage Select in the debug menu will report all the implemented level names.
* Sonic CD stages correctly transitions as the original game.
* Metal Sonic is now a playable character.
* Sonic 3 will be included.

## Known Issues

* **Building requires proper RSDK data files**
* No Sonic 3 yet (in progress).
* Sonic 1 Special Stages are working from the Stage Select, but the graphics may be corrupted.
* The main menu of RSDK will report the wrong stage names.
* The Giant Ring from Sonic the Hedgehog 1 will teleport to the Sonic the Hedgehog 2 special stages.
* Collision Chaos and Stardust Speedway are half-implemented.
* Tidal Tempest, Quartz Quadrant, Wacky Workbench and Metallic Madness are barely implemented.
* In Palmtree Panic Zone, the spinner will softlock the player.
* Some Sonic CD's enemies and gimmicks might have the wrong palette.
* Playable Metal Sonic has a "rolling" bugging collision.

## Resources

Xeeynamo has written [some notes](rsdkv3-to-rsdkv4.md) on how to convert RSDKv3 scripts to RSDKv4 scripts without modifying the RSDKv4 engine.

Everything contained in `rsdk/Scripts` is a modified version of [Rubberduckycooly's Sonic 1/2 script decompilation](https://github.com/Rubberduckycooly/Sonic-1-Sonic-2-2013-Script-Decompilation). This project would not exist without it.

The function `SonicHybridRsdk.Unpack12/DecryptData` was written by Giuseppe Gatta (nextvolume) from its [Retrun](http://unhaut.epizy.com/retrun/).

## Credits

* Decompilation by Rubberduckycooly.
* Hybrid-RSDK by Xeeynamo.
* Sonic 3 AIR by Eukaryot.
* Main Development By FGSOFTWARE1.
* RSDK Decompilations by RSDKModding team.
