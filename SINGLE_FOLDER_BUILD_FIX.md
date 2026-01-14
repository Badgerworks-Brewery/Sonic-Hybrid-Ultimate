# Single-Folder Build Fix - Implementation Notes

## Issues Fixed

### 1. Multiple Output Folders ✅ FIXED
**Problem**: Build was creating two separate folders:
- `Custom-Client/bin/Release/net6.0-windows/win-x64/publish/`
- `Hybrid-RSDK-Main/build/`

Additionally, PostBuild targets were creating subfolders within the output:
- `Hybrid-RSDK-Main/` subfolder in output
- `Sonic 3 AIR Main/` subfolder in output

**Solution Implemented**:
1. **Removed PostBuild Targets**: Deleted the PostBuild targets from `CustomClient.csproj` that were creating the `Hybrid-RSDK-Main` and `Sonic 3 AIR Main` subfolders
2. **Removed Folder Copying**: Removed ItemGroup entries that copied entire folder hierarchies to output
3. **Fixed Packaging Scripts**: Updated both `package.ps1` and `package.sh` to use `PublishSingleFile=true` instead of `false`
4. **Proper Single-File Publishing**: Enabled `IncludeNativeLibrariesForSelfExtract=true` and `IncludeAllContentForSelfExtract=true`

**Result**: 
- Build now outputs to a SINGLE `dist/SonicHybridUltimate/` folder
- Contains only: `SonicHybrid.exe` + native DLLs + `GameData/` folder
- NO extra subdirectories for Hybrid-RSDK-Main or Sonic 3 AIR Main

### 2. Sonic 3 AIR Stub Mode - ARCHITECTURAL LIMITATION

**Current Architecture**:
The OxygenWrapper.cpp shows that Sonic 3 AIR integration currently works in 3 modes:
1. **Embedded Mode**: (TODO - not yet implemented) - Would compile entire Oxygen engine into the exe
2. **External Mode**: Launches external Sonic 3 AIR executable found at:
   - `Sonic 3 AIR Main/sonic3air.exe`
   - Environment variable `SONIC3AIR_PATH`
   - Common installation paths
3. **Stub Mode**: Provides helpful error messages when neither embedded nor external is available

**Why Stub Mode Activates**:
When the packaged build doesn't include the `Sonic 3 AIR Main/sonic3air.exe` file, the OxygenEngine can't find an executable to launch, so it enters stub mode.

**Why Full Embedding Is Not Trivial**:
Sonic 3 AIR (Oxygen Engine) is a complex game engine with:
- Its own rendering pipeline (OpenGL/DirectX)
- Script system with hundreds of game scripts
- Audio/video playback systems
- ROM loading and interpretation
- Save game management
- Mod support infrastructure
- Extensive data files (sprites, sounds, music, scripts)

Fully embedding this requires:
1. Compiling the entire Oxygen engine as a static library
2. Embedding all data files (sprites, sounds, scripts) as resources
3. Implementing resource extraction at runtime
4. Managing conflicts between SDL2 instances (RSDK and Oxygen both use SDL2)
5. Handling window management for seamless transitions
6. Managing memory properly across both engines

**Current Workaround**:
For now, users need to:
1. Download Sonic 3 AIR from https://sonic3air.org/
2. Extract to `Sonic 3 AIR Main/` folder in the repository
3. The packaging script will then bundle it into the distribution

**Future Implementation Path**:
To fully embed Sonic 3 AIR:
1. Add Sonic 3 AIR (Oxygen) as a git submodule or vendor dependency
2. Modify its CMakeLists.txt to build as a static library
3. Create a proper C wrapper for its C++ API
4. Embed data files as resources in the .exe
5. Implement resource extraction on first run
6. Update OxygenWrapper.cpp to use embedded mode instead of launching external process

This is a significant engineering effort that would require:
- Several weeks of development
- Deep understanding of the Oxygen engine internals
- Testing across Windows/Linux/macOS
- Handling of licensing for bundled Sonic 3 AIR

## Changes Made

### File: `package.ps1`
**Before**:
```powershell
dotnet publish -c Release -r $RID --self-contained true -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=false
```

**After**:
```powershell
dotnet publish -c Release -r $RID --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true
```

### File: `package.sh`
**Before**:
```bash
dotnet publish -c Release -r $RID --self-contained true -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=false
```

**After**:
```bash
dotnet publish -c Release -r $RID --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true
```

### File: `CustomClient.csproj`
**Removed**:
- ItemGroup with `<None Update="Hybrid-RSDK-Main\**">` and `<None Update="Sonic 3 AIR Main\**">`
- PostBuild Target for Windows (creating subfolders, copying DLLs)
- PostBuild Target for Linux/macOS (creating subfolders, copying .so files)

**Why**: These were causing extra folders to be created in the build output and preventing true single-folder distribution.

## Testing the Fix

### Build and Package:
```powershell
# Windows
.\package.ps1
```

```bash
# Linux/macOS
./package.sh
```

### Expected Output:
```
dist/SonicHybridUltimate/
├── SonicHybrid.exe          # Single executable
├── RSDKv4.dll               # Native RSDK engine
├── OxygenEngine.dll         # Native Oxygen wrapper
├── SDL2.dll                 # Dependencies
├── GLEW.dll
├── ogg.dll
├── vorbis.dll
├── theora.dll
├── [other runtime DLLs]
├── GameData/                # Single subfolder for game files
│   ├── sonic1.rsdk
│   ├── sonic2.rsdk
│   ├── soniccd.rsdk
│   └── sonic3.bin
└── README.txt
```

**No `Hybrid-RSDK-Main/` folder**
**No `Sonic 3 AIR Main/` folder** 

## For Sonic 3 AIR Support

### Temporary Solution (Until Full Embedding):
1. Download Sonic 3 AIR: https://sonic3air.org/
2. Extract to `Sonic 3 AIR Main/` in the project root
3. Run the packaging script - it will bundle the executable
4. The OxygenEngine will find and launch it

### Long-term Solution:
Requires implementation of embedded Oxygen engine mode (see "Future Implementation Path" above).

## Status

✅ **Single-folder build**: FIXED
⚠️ **Sonic 3 AIR embedding**: Requires architectural changes (documented above)

The build now produces a clean single-folder distribution. The Sonic 3 AIR stub mode is a separate architectural issue that requires significant refactoring to fully resolve.
