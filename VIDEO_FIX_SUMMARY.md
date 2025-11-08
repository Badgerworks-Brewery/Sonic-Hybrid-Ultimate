# Video.cpp Build Fix Summary

## Issue
The build was failing when compiling `Video.cpp` from the Team Forever RSDKv4 patch with the following errors:
- Missing THEORAPLAY type definitions (`THEORAPLAY_Decoder`, `THEORAPLAY_VideoFrame`, etc.)
- Incorrect number of parameters for `THEORAPLAY_startDecode` and `THEORAPLAY_startDecodeFile` functions

## Root Cause
The Team Forever patch added Video.cpp and Video.hpp to support OGG Theora video playback, but the code was written for an older version of the theoraplay library API. The current theoraplay library (from icculus/theoraplay) uses an updated API that requires additional parameters:
- Old API: `THEORAPLAY_startDecodeFile(filepath, maxframes, vidfmt)`
- New API: `THEORAPLAY_startDecodeFile(filepath, maxframes, vidfmt, allocator, multithreaded)`

## Solution
Updated all calls to THEORAPLAY functions in Video.cpp to include the new required parameters:
- `allocator`: Set to `NULL` to use default memory allocation
- `multithreaded`: Set to `1` to enable multithreading for better performance

### Changes Made
```cpp
// Before:
videoDecoder = THEORAPLAY_startDecodeFile(filepath, 30, THEORAPLAY_VIDFMT_RGBA);

// After:
videoDecoder = THEORAPLAY_startDecodeFile(filepath, 30, THEORAPLAY_VIDFMT_RGBA, NULL, 1);
```

## Build Process Updates
1. **Updated build_all.sh** to automatically apply the Team Forever patch
2. **Updated BUILD_INSTRUCTIONS.md** to include:
   - OGG and Theora dependencies (`libogg-dev`, `libtheora-dev`)
   - Team Forever patch application step
   - Clear documentation of the build process

## Test Results
✅ CMake configuration succeeds
✅ Build completes successfully
✅ All libraries and executables are generated:
   - `librsdk_core.a`
   - `librsdkv3_core.a`
   - `libHybridRSDK.so`
   - `rsdkv4` executable

## Known Issues
The Custom Client DLL loading error is a separate issue:
- The Custom Client is looking for a shared library named `RSDKv4` with exported functions (`InitRSDKv4`, `UpdateRSDKv4`, `CleanupRSDKv4`)
- These wrapper functions don't exist yet - the Custom Client integration is at 0% according to the README
- The priority is debugging Hybrid-RSDK-Main (now complete), not the Custom Client

## Next Steps
For future development:
1. Create a C wrapper library that exports the required functions for P/Invoke
2. Integrate the rsdk_core library with the Custom Client via the wrapper
3. Test end-to-end functionality with actual game data files
