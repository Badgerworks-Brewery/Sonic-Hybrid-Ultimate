# Sonic Hybrid Ultimate - Compilation Error Fixes

## Issues Addressed

The build was failing with multiple compilation errors in `Drawing.cpp` due to missing declarations and incomplete struct definitions.

### Original Error Log Summary
```
/home/runner/work/Sonic-Hybrid-Ultimate/Sonic-Hybrid-Ultimate/Hybrid-RSDK-Main/RSDKV4/RSDKV4/Drawing.cpp:241:5: error: 'textureList' was not declared in this scope
/home/runner/work/Sonic-Hybrid-Ultimate/Sonic-Hybrid-Ultimate/Hybrid-RSDK-Main/RSDKV4/RSDKV4/Drawing.cpp:244:5: error: 'ResetRenderStates' was not declared in this scope
/home/runner/work/Sonic-Hybrid-Ultimate/Sonic-Hybrid-Ultimate/Hybrid-RSDK-Main/RSDKV4/RSDKV4/Drawing.cpp:4407:30: error: 'struct TextMenu' has no member named 'entryStart'
```

## Root Cause Analysis

1. **Missing Renderer.hpp Include**: The `Drawing.cpp` file needed OpenGL/rendering declarations from `Renderer.hpp`, but this header was not included in the include chain.

2. **Incomplete TextMenu Struct**: The `TextMenu` struct in `Text.hpp` was missing several members that `Drawing.cpp` was trying to access.

3. **Buffer Overflow Warning**: The `Debug.hpp` file had an unsafe `sprintf` call that could cause buffer overflow.

## Fixes Applied

### 1. Added Renderer.hpp Include to RetroEngine.hpp

**File**: `/Hybrid-RSDK-Main/RSDKV4/RSDKV4/RetroEngine.hpp`
**Lines**: 340 (added after Drawing.hpp)

```cpp
#include "Drawing.hpp"
#include "Renderer.hpp"  // <- Added this line
#include "Scene3D.hpp"
```

**Resolves**:
- `textureList` was not declared
- `retroVertexList` was not declared  
- `screenBufferVertexList` was not declared
- `ResetRenderStates` was not declared
- `SetupDrawIndexList` was not declared
- `ClearMeshData` was not declared
- `ClearTextures` was not declared
- `SetPerspectiveMatrix` was not declared
- `TransferRetroBuffer` was not declared
- `TEXFMT_RETROBUFFER` was not declared

### 2. Extended TextMenu Struct in Text.hpp

**File**: `/Hybrid-RSDK-Main/RSDKV4/RSDKV4/Text.hpp`
**Lines**: 19-22 (added new members)

```cpp
struct TextMenu {
    char text[0x100][0x20];
    int selection1;
    int selection2;
    int rowCount;
    int alignment;
    int visibleRowCount;
    int visibleRowOffset;
    int timer;
    int selectionCount;
    int entryStart[0x100];      // <- Added
    int entrySize[0x100];       // <- Added
    byte textData[0x2000];      // <- Added
    bool entryHighlight[0x100]; // <- Added
};
```

**Resolves**:
- `'struct TextMenu' has no member named 'entryStart'`
- `'struct TextMenu' has no member named 'entrySize'`
- `'struct TextMenu' has no member named 'textData'`
- `'struct TextMenu' has no member named 'entryHighlight'`

### 3. Fixed Buffer Overflow in Debug.hpp

**File**: `/Hybrid-RSDK-Main/RSDKV4/RSDKV4/Debug.hpp`
**Lines**: 9-53 (replaced entire PrintLog function)

**Before** (unsafe):
```cpp
vsprintf(buffer, msg, args);
if (endLine) {
    printf("%s\n", buffer);
    sprintf(buffer, "%s\n", buffer);  // <- Buffer overflow risk
}
```

**After** (safe):
```cpp
vsnprintf(buffer, sizeof(buffer) - 2, msg, args);  // Leave space for newline
va_end(args);

if (endLine) {
    printf("%s\n", buffer);
    // Safely append newline for file writing
    size_t len = strlen(buffer);
    if (len < sizeof(buffer) - 2) {
        buffer[len] = '\n';
        buffer[len + 1] = '\0';
    }
}
```

**Resolves**:
- `warning: 'sprintf' may write a terminating nul past the end of the destination`

## Technical Details

### Include Chain Resolution
The issue was that `Drawing.cpp` includes `RetroEngine.hpp`, which includes `Drawing.hpp`, but `Renderer.hpp` was not in the include chain. Since `Drawing.cpp` uses OpenGL rendering functions and variables declared in `Renderer.hpp`, adding it to the include chain resolves all the missing symbol errors.

### TextMenu Structure Design
The added members follow the existing pattern in the struct:
- Arrays sized to `0x100` (256 entries) to match the existing `text` array
- `textData` sized to `0x2000` (8192 bytes) for larger text storage
- Types chosen based on usage patterns in `Drawing.cpp`

### Buffer Safety Approach
The fix uses `vsnprintf` with size limits and manual bounds-checked newline appending instead of the dangerous self-referential `sprintf` calls.

## Files Modified

1. **RetroEngine.hpp**: Added `#include "Renderer.hpp"`
2. **Text.hpp**: Extended `TextMenu` struct with missing members
3. **Debug.hpp**: Replaced unsafe `PrintLog` function with buffer-safe version

## Validation

Created test file `test_compile_fixes.cpp` that verifies:
- All missing symbols are now declared
- TextMenu struct has required members
- Include chain works correctly

## Build Impact

These changes should resolve all compilation errors in `Drawing.cpp` without affecting:
- Runtime behavior (new struct members are just storage)
- API compatibility (no function signatures changed)
- Build system configuration (only source code changes)

The build should now complete successfully through the CMake compilation phase.