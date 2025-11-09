# Sonic Hybrid Ultimate - Architecture Proposal

## Current Status

The current implementation has successfully fixed:
- ✅ Video.cpp build errors (THEORAPLAY API compatibility)
- ✅ Patch re-application system (idempotent)
- ✅ Team Forever enhancements integration
- ✅ RSDKv4 shared library with P/Invoke exports
- ✅ Custom Client with multi-engine support (theoretical)

However, we've encountered persistent DLL loading issues on Windows due to:
- Missing dependencies (SDL2, OpenGL, GLEW, Vorbis, Theora, OGG)
- Complex P/Invoke integration between C# and C++
- Multiple engine management (RSDKv3, RSDKv4, Oxygen)

## Recommended Architecture (Based on Xeeynamo's Implementation)

After reviewing [Xeeynamo/sonic-hybrid-rsdk](https://github.com/Xeeynamo/sonic-hybrid-rsdk), we should adopt a **much simpler approach**:

### The Xeeynamo Approach

```
┌─────────────────────────────────────────────────────────────┐
│ Build Process (C# Tools)                                     │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  1. Unpack soniccd.rsdk (RSDKv3) → Extract files/scripts     │
│  2. Unpack sonic1.rsdk (RSDKv4) → Extract files/scripts      │
│  3. Unpack sonic2.rsdk (RSDKv4) → Extract files/scripts      │
│                                                               │
│  4. Convert RSDKv3 scripts to RSDKv4 format                  │
│  5. Merge all assets into unified structure                  │
│  6. Modify scripts for stage transitions                     │
│  7. Generate single Data.rsdk containing all games           │
│                                                               │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ Runtime (Single RSDKv4 Executable)                           │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  • Load unified Data.rsdk                                    │
│  • Play Sonic 1 → Sonic CD → Sonic 2 seamlessly            │
│  • All transitions handled by modified scripts               │
│  • No engine switching required                              │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

### Key Benefits

1. **Single Executable**: Only RSDKv4 engine needed
2. **No DLL Issues**: Everything statically linked
3. **No P/Invoke**: Pure C++ executable
4. **Simpler**: No Custom Client management layer
5. **Proven**: Xeeynamo's implementation works successfully

## Implementation Steps

### Phase 1: Build Tools (C# - Similar to Xeeynamo)

Create the following tools under `Tools/` directory:

1. **UnpackSCD** - Decrypt and extract Sonic CD .rsdk files (RSDKv3 format)
   - Decrypt using Sonic CD encryption keys
   - Extract directory structure and files
   - Based on Xeeynamo's `SonicHybridRsdk.UnpackScd`

2. **UnpackS12** - Extract Sonic 1 and Sonic 2 .rsdk files (RSDKv4 format)
   - Extract directory structure and files
   - Based on Xeeynamo's `SonicHybridRsdk.UnpackS12`

3. **Generator** - Convert and merge all games into single RSDKv4 data file
   - Convert RSDKv3 entities to RSDKv4 format
   - Merge GameConfig.bin from all three games
   - Modify scripts for stage transitions (e.g., Final Zone → Palmtree Panic)
   - Generate unified Data.rsdk
   - Based on Xeeynamo's `SonicHybridRsdk.Generator`

4. **Build** - Orchestrate the entire build process
   - Run UnpackSCD on soniccd.rsdk
   - Run UnpackS12 on sonic1.rsdk and sonic2.rsdk
   - Run Generator to create unified Data.rsdk
   - Based on Xeeynamo's `SonicHybridRsdk.Build`

### Phase 2: Single RSDKv4 Executable

**Use Hybrid-RSDK-Main (RSDKv4) as the only runtime:**
- Already built and working (with Team Forever enhancements)
- No need for RSDKv3 engine
- No need for Oxygen/Sonic 3 AIR (separate project)
- Just load the unified Data.rsdk

### Phase 3: Deprecate Custom Client

**Remove the Custom Client entirely:**
- No longer needed with unified Data.rsdk
- Eliminates all DLL loading issues
- Simplifies project structure dramatically

## Comparison

### Current Approach (Complex)
```
User runs CustomClient.exe
  ↓
CustomClient.exe loads RSDKv4.dll (Windows DLL hell)
  ↓
RSDKv4.dll needs: SDL2.dll, OpenGL, GLEW, Vorbis, Theora, OGG
  ↓
CustomClient switches between engines based on game
  ↓
Complexity and DLL issues
```

### Proposed Approach (Simple)
```
User runs rsdkv4.exe
  ↓
Loads unified Data.rsdk
  ↓
Plays Sonic 1 → CD → 2 seamlessly
  ↓
Everything works
```

## Migration Path

1. **Keep current fixes** - Video.cpp fixes, patch system, etc. are still valuable
2. **Add C# build tools** - Port/adapt Xeeynamo's unpacking and generation tools
3. **Create unified Data.rsdk** - Merge all three games using the tools
4. **Test single RSDKv4 exe** - Verify it works with unified data
5. **Deprecate Custom Client** - Remove once unified approach works
6. **Update documentation** - Reflect new simpler architecture

## File Structure (Proposed)

```
Sonic-Hybrid-Ultimate/
├── Hybrid-RSDK-Main/          # RSDKv4 engine (already working)
│   ├── RSDKV4-Decompilation/
│   ├── build/
│   └── rsdkv4.exe             # Single executable
├── Tools/                     # C# build tools (NEW)
│   ├── UnpackSCD/
│   ├── UnpackS12/
│   ├── Generator/
│   └── Build/
├── rsdk-source-data/          # Input files
│   ├── soniccd.rsdk
│   ├── sonic1.rsdk
│   └── sonic2.rsdk
└── sonic-hybrid/              # Generated unified data (NEW)
    ├── Data.rsdk              # Unified game data
    ├── Data-Custom/           # Modified scripts
    └── Scripts/               # Converted scripts
```

## Next Steps

1. Review and approve this architecture change
2. Create the C# build tools based on Xeeynamo's implementation
3. Test unpacking and conversion process
4. Generate unified Data.rsdk
5. Test with single RSDKv4 executable
6. Remove Custom Client if successful

## References

- [Xeeynamo's Sonic Hybrid RSDK](https://github.com/Xeeynamo/sonic-hybrid-rsdk)
- [RSDKv3 to RSDKv4 Conversion Notes](https://github.com/Xeeynamo/sonic-hybrid-rsdk/blob/main/rsdkv3-to-rsdkv4.md)
- [Rubberduckycooly's RSDK Reverse Engineering](https://github.com/Rubberduckycooly/RSDK-Reverse)
