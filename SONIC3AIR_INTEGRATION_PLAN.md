# Sonic 3 AIR Integration Plan

## Current Status

The Sonic 3 AIR (Oxygen Engine) is available as a git submodule at `vendor/sonic3air` but is not currently built or integrated into the Hybrid executable.

### What Exists
- ✅ Git submodule configured at `vendor/sonic3air`
- ✅ OxygenWrapper.cpp (thin wrapper that launches external executable)
- ✅ C# P/Invoke bindings in OxygenEngine.cs
- ❌ No CMake integration to build Sonic 3 AIR from source
- ❌ No static library linking

### Why Stub Mode Exists
The OxygenWrapper currently operates in two modes:
1. **External Mode**: Finds and launches `sonic3air.exe` as a separate process
2. **Stub Mode**: Falls back when executable not found, provides setup instructions

Stub mode is intentional - it allows the application to gracefully handle missing Sonic 3 AIR without crashing.

## Full Integration Requirements

To build Sonic 3 AIR into the final executable requires:

### 1. CMake Build System Integration

**Sonic 3 AIR Build Structure**:
```
vendor/sonic3air/
├── librmx/
│   ├── source/rmxbase/      → librmxbase (utility classes)
│   └── source/rmxmedia/     → librmxmedia (game framework on SDL2/OpenGL)
├── Oxygen/
│   ├── lemonscript/         → liblemonscript (scripting engine)
│   ├── oxygenengine/        → liboxygenengine (core game engine)
│   └── sonic3air/           → sonic3air executable (S3&K specific code)
└── framework/
    └── external/
        ├── sdl/             → SDL2 (bundled)
        ├── ogg-vorbis/      → Audio libraries
        ├── zlib/            → Compression
        ├── curl/            → Network library
        └── imgui/           → Debug UI
```

**Required CMake Changes**:
```cmake
# In Hybrid-RSDK-Main/CMakeLists.txt

# Option to build Sonic 3 AIR from source
option(BUILD_SONIC3AIR "Build Sonic 3 AIR (Oxygen Engine) from source" OFF)

if(BUILD_SONIC3AIR)
    # Check if submodule is initialized
    if(NOT EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/../vendor/sonic3air/CMakeLists.txt")
        message(STATUS "Initializing sonic3air submodule...")
        execute_process(
            COMMAND git submodule update --init vendor/sonic3air
            WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}/..
        )
    endif()
    
    # Set Sonic 3 AIR build options
    set(BUILD_SDL_STATIC ON CACHE BOOL "Build SDL as static library")
    set(USE_DISCORD OFF CACHE BOOL "Disable Discord integration")
    set(BUILD_OXYGEN_SERVER OFF CACHE BOOL "Don't build server")
    
    # Add Sonic 3 AIR as subdirectory
    add_subdirectory(
        ${CMAKE_CURRENT_SOURCE_DIR}/../vendor/sonic3air/Oxygen/sonic3air/build/_cmake
        ${CMAKE_BINARY_DIR}/sonic3air
    )
    
    # Link OxygenEngine wrapper to sonic3air libraries
    target_link_libraries(OxygenEngine PRIVATE
        sonic3air          # Main S3AIR library
        oxygenengine       # Oxygen engine core
        lemonscript        # Scripting engine
        rmxmedia           # Media framework
        rmxbase            # Base utilities
    )
    
    target_compile_definitions(OxygenEngine PRIVATE
        OXYGEN_EMBEDDED_MODE  # Tell wrapper to use embedded engine
    )
endif()
```

### 2. Dependency Management

**Conflict**: Both RSDK and Sonic 3 AIR use SDL2
- RSDK links to system SDL2 or vcpkg SDL2
- Sonic 3 AIR bundles its own SDL2 build

**Solutions**:
1. **Option A** (Recommended): Use Sonic 3 AIR's bundled SDL2 for both
   - Modify RSDK to link against sonic3air's SDL2
   - Ensures compatibility
   
2. **Option B**: Build Sonic 3 AIR with system SDL2
   - Modify sonic3air CMake to use external SDL2
   - Risk of version conflicts

3. **Option C**: Separate SDL2 contexts
   - Keep both SDL2 instances
   - Initialize separately (complex, not recommended)

### 3. OxygenWrapper Modifications

**Current (External Launch)**:
```cpp
// OxygenWrapper.cpp - launches external process
processId = fork();
if (processId == 0) {
    execl(exePath, exePath, romArg, (char*)NULL);
}
```

**After Integration (Embedded)**:
```cpp
// OxygenWrapper.cpp - calls engine directly
#ifdef OXYGEN_EMBEDDED_MODE
#include "oxygen/Application.h"

static oxygen::Application* s_oxygenApp = nullptr;

EXPORT int InitOxygenEngine(const char* scriptPath) {
    s_oxygenApp = new oxygen::Application();
    return s_oxygenApp->initialize(scriptPath) ? 1 : 0;
}

EXPORT void UpdateOxygenEngine() {
    if (s_oxygenApp) {
        s_oxygenApp->update();
    }
}
#endif
```

**Challenges**:
- Oxygen expects to control the main loop
- Need to integrate with RSDK's game loop
- Window management conflicts

### 4. Data Files

Sonic 3 AIR requires ~100MB of data files:
```
Oxygen/sonic3air/
├── data/
│   ├── shader/              → ~500KB of shaders
│   ├── font/                → Font files
│   └── ...                  → Various game data
└── scripts/
    └── *.lemon              → ~50MB of game scripts
```

**Packaging Options**:
1. **Bundle as resources** (Complex)
   - Embed files in executable
   - Extract on first run
   - Requires resource manager

2. **Copy to distribution** (Simple)
   - Include data/ and scripts/ folders
   - Keep separate from executable
   - Current approach for development

### 5. Build Time Impact

**Current Build Times**:
- RSDK libraries: ~2 minutes
- Custom Client: ~30 seconds
- **Total**: ~2.5 minutes

**With Sonic 3 AIR**:
- Sonic 3 AIR libraries: ~8-10 minutes (first build)
- Subsequent builds: ~2 minutes (with ccache)
- **New Total**: ~12 minutes (first), ~4.5 minutes (incremental)

### 6. Binary Size Impact

**Current**:
- SonicHybrid.exe: ~5MB (with .NET runtime)
- RSDKv4.dll: ~2MB
- OxygenEngine.dll: ~50KB (stub wrapper)
- Total: ~7MB + dependencies

**With Integrated Sonic 3 AIR**:
- SonicHybrid.exe: ~5MB
- RSDKv4.dll: ~2MB
- OxygenEngine.dll: ~15MB (includes sonic3air, oxygenengine, lemonscript, etc.)
- Total: ~22MB + dependencies

**Plus data files**: ~100MB of scripts/shaders/assets

## Implementation Steps

### Phase 1: Build Integration (Week 1)
1. ✅ Initialize sonic3air submodule
2. Add CMake option `BUILD_SONIC3AIR`
3. Integrate sonic3air CMakeLists as subdirectory
4. Resolve SDL2 dependency conflicts
5. Test that sonic3air builds standalone

### Phase 2: Wrapper Integration (Week 1-2)
1. Add `OXYGEN_EMBEDDED_MODE` preprocessor flag
2. Modify OxygenWrapper.cpp to call engine directly
3. Create proper C API for oxygen::Application
4. Handle initialization/shutdown lifecycle
5. Test basic ROM loading

### Phase 3: Runtime Integration (Week 2)
1. Resolve window management conflicts
2. Integrate game loops (RSDK vs Oxygen)
3. Handle input properly
4. Test transitions between games
5. Handle cleanup/restart scenarios

### Phase 4: Data Packaging (Week 2-3)
1. Bundle data files in distribution
2. Update packaging scripts
3. Handle resource loading
4. Test on clean systems
5. Documentation

### Phase 5: Polish (Week 3)
1. Performance optimization
2. Memory leak fixes
3. Cross-platform testing
4. User documentation
5. Release

## Estimated Effort

**Total Time**: 2-3 weeks of focused development

**Skills Required**:
- CMake build system expertise
- C++ library integration
- SDL2/OpenGL knowledge
- Game loop architecture
- Cross-platform development

**Risk Factors**:
- High: Window management conflicts
- High: SDL2 instance conflicts
- Medium: Memory management
- Medium: Build system complexity
- Low: Data file handling

## Alternative Approaches

### Approach A: Hybrid Mode (Recommended for Now)
**Status Quo**: Keep external launch mode
- Users download prebuilt Sonic 3 AIR
- Place in expected location
- OxygenWrapper launches it
- **Pros**: Works today, no build changes needed
- **Cons**: Not "single exe", requires separate download

### Approach B: Full Integration (Future)
**As Described Above**
- Build Sonic 3 AIR from source
- Link statically into OxygenEngine.dll
- **Pros**: True single-exe distribution
- **Cons**: 2-3 weeks of work, complex integration

### Approach C: Bundled Executable (Middle Ground)
**Compromise**: Include prebuilt sonic3air.exe
- Package sonic3air executable in distribution
- OxygenWrapper launches bundled copy
- **Pros**: Single folder distribution, minimal work
- **Cons**: Still separate process, larger download

## Recommendation

**Short Term** (Current PR):
- Keep stub mode as-is
- Document workaround (download Sonic 3 AIR)
- Focus on single-folder build (✅ already fixed)

**Medium Term** (Next PR):
- Implement Approach C: Bundle prebuilt executable
- Download sonic3air release from sonic3air.org
- Include in dist/ folder
- Update OxygenWrapper search paths

**Long Term** (Future Major Feature):
- Implement Approach B: Full integration
- Create dedicated PR for this work
- Requires 2-3 weeks of development
- Significant testing required

## Current Status

- ✅ Submodule initialized
- ✅ Build integration plan documented
- ⏳ Awaiting decision on approach
- ❌ Full integration not yet implemented

For the current PR focusing on backend setup and single-folder distribution, the stub mode is acceptable. Full Sonic 3 AIR integration should be a separate, dedicated effort.
