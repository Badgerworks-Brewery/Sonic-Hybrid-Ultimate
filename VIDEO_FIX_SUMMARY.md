# Video.cpp Build Fix Summary

## Issue
The build was failing when compiling `Video.cpp` from the Team Forever RSDKv4 patch with the following errors:
- Missing THEORAPLAY type definitions (`THEORAPLAY_Decoder`, `THEORAPLAY_VideoFrame`, etc.)
- Incorrect number of parameters for `THEORAPLAY_startDecode` and `THEORAPLAY_startDecodeFile` functions
- Patch re-application error when running `apply_teamforever.sh` multiple times

## Root Cause
1. The Team Forever patch added Video.cpp and Video.hpp to support OGG Theora video playback, but the code was written for an older version of the theoraplay library API
2. The current theoraplay library (from icculus/theoraplay) uses an updated API that requires additional parameters:
   - Old API: `THEORAPLAY_startDecodeFile(filepath, maxframes, vidfmt)`
   - New API: `THEORAPLAY_startDecodeFile(filepath, maxframes, vidfmt, allocator, multithreaded)`
3. The `apply_teamforever.sh` script didn't check if the patch was already applied, causing errors on subsequent runs

## Solution
1. **Updated the patch file** (`teamforever-rsdkv4.patch`) to include the correct THEORAPLAY API calls with new parameters:
   - `allocator`: Set to `NULL` to use default memory allocation
   - `multithreaded`: Set to `1` to enable multithreading for better performance

2. **Fixed `apply_teamforever.sh`** to check if Video.cpp and Video.hpp already exist before applying the patch, preventing re-application errors

3. **Created RSDKv4 shared library** (`libRSDKv4.so` / `RSDKv4.dll`) for Custom Client P/Invoke integration:
   - Added `RSDKv4Wrapper.cpp` with C exports for `InitRSDKv4`, `UpdateRSDKv4`, and `CleanupRSDKv4`
   - Updated CMakeLists.txt to build the shared library
   - Proper symbol visibility for cross-platform compatibility

### Changes Made
```cpp
// Before (in patch file):
videoDecoder = THEORAPLAY_startDecodeFile(filepath, 30, THEORAPLAY_VIDFMT_RGBA);

// After (in patch file):
videoDecoder = THEORAPLAY_startDecodeFile(filepath, 30, THEORAPLAY_VIDFMT_RGBA, NULL, 1);
```

## Build Process Updates
1. **Updated build_all.sh** to automatically apply the Team Forever patch
2. **Updated BUILD_INSTRUCTIONS.md** to include:
   - OGG and Theora dependencies (`libogg-dev`, `libtheora-dev`)
   - Team Forever patch application step
   - Clear documentation of the build process
3. **Fixed patch re-application** to be idempotent (can run multiple times safely)

## Test Results
✅ CMake configuration succeeds
✅ Build completes successfully
✅ All libraries and executables are generated:
   - `librsdk_core.a`
   - `librsdkv3_core.a`
   - `libHybridRSDK.so`
   - `libRSDKv4.so` (NEW - for Custom Client)
   - `rsdkv4` executable

✅ RSDKv4 shared library exports required functions:
   - `InitRSDKv4`
   - `UpdateRSDKv4`
   - `CleanupRSDKv4`

## Custom Client DLL Loading

The Custom Client requires the RSDKv4 shared library to function. The build system now automatically handles this:

### Automatic DLL Copying (Recommended)
When you build with CMake, the RSDKv4 library is automatically copied to:
1. The Custom Client output directory (`Custom-Client/bin/Release/net6.0-windows/`)
2. The bin directory (`Hybrid-RSDK-Main/build/bin/`)

### Manual DLL Copying (If Needed)
If the automatic copy doesn't work:

**On Windows:**
1. After building, locate `RSDKv4.dll` in `Hybrid-RSDK-Main/build/lib/` or `Hybrid-RSDK-Main/build/bin/Release/`
2. Copy it to the same directory as `SonicHybrid.exe` (typically `Custom-Client/bin/Release/net6.0-windows/`)

**On Linux:**
1. After building, locate `libRSDKv4.so` in `Hybrid-RSDK-Main/build/lib/`
2. Copy it to the same directory as the Custom Client executable

### Verifying the DLL
The Custom Client will look for:
- **Windows:** `RSDKv4.dll` 
- **Linux:** `libRSDKv4.so` (without the `lib` prefix in the P/Invoke call, Linux handles this automatically)

If you still get DLL loading errors, ensure:
1. The DLL is in the same directory as the executable
2. All dependencies (SDL2, OpenGL, GLEW, Vorbis, Theora, OGG) are installed
3. On Windows, the Visual C++ Redistributable is installed

## Remaining Work

- Custom Client file selector needs to support all game files (Sonic 1, CD, 2, S3&K)
- Build output consolidation into single executable
