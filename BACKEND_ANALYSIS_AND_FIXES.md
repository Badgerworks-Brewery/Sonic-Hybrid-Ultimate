# Sonic Hybrid Ultimate - Backend Analysis and Fixes

## Executive Summary

After analyzing the repository structure, I've identified the key issues and provide concrete solutions below. The C# build tools exist but are **not integrated into the build process**. The RSDK engine and Custom Client are looking for individual .rsdk files instead of a unified hybrid Data.rsdk.

---

## Current State Analysis

### What Exists ✅

1. **C# Build Tools** (Hybrid-RSDK-Main/)
   - ✅ `SonicHybridRsdk.UnpackScd/` - Unpacks Sonic CD (RSDKv3 encrypted)
   - ✅ `SonicHybridRsdk.UnpackS12/` - Unpacks Sonic 1 & 2 (RSDKv4)
   - ✅ `SonicHybridRsdk.Generator/` - Converts and merges games
   - ✅ `SonicHybridRsdk.Build/` - Orchestrator (Build.cs)
   - ✅ `SonicHybridRsdk.sln` - Complete solution file

2. **Directory Structure**
   - ✅ `rsdk-source-data/` - For source .rsdk files (currently empty except README)
   - ✅ `sonic-hybrid/` - Target for unified Data.rsdk (currently has only C++ code)

3. **RSDK Engines**
   - ✅ RSDKv4 core library and wrapper
   - ✅ RSDKv3 core library (optional)
   - ✅ CMake build system for native code

4. **Custom Client**
   - ✅ C# .NET 6.0 application
   - ✅ P/Invoke integration with RSDKv4.dll
   - ✅ Multi-engine support framework

### What's Missing ❌

1. **Build Integration**
   - ❌ C# tools are never compiled or executed
   - ❌ No CMake step to build/run SonicHybridRsdk.Build
   - ❌ No generation of unified Data.rsdk
   - ❌ No automation of the hybrid build process

2. **Data Files**
   - ❌ Source .rsdk files not in repository (expected - user must provide)
   - ❌ Generated Data.rsdk not being created
   - ❌ No validation that source files exist before building

3. **Engine Configuration**
   - ❌ RSDKv4Wrapper.cpp expects individual .rsdk paths
   - ❌ Custom Client searches for sonic1.rsdk, sonic2.rsdk, soniccd.rsdk separately
   - ❌ No configuration to use unified sonic-hybrid/Data.rsdk

---

## Problem Analysis

### 1. C# Build Tools Not Integrated

**Current**: The `SonicHybridRsdk.Build` project exists but is never compiled or run during the build process.

**Impact**: 
- Unified Data.rsdk is never generated
- Users can't run the hybrid experience
- The entire purpose of the project is unfulfilled

### 2. Engine Looking in Wrong Places

**Current**: 
- `RSDKv4Wrapper.cpp` accepts any data path via `InitRSDKv4(const char* dataPath)`
- `Custom-Client/Program.cs` searches for individual game files:
  - `GamePaths.Sonic1SearchPaths`
  - `GamePaths.Sonic2SearchPaths`
  - `GamePaths.SonicCDSearchPaths`

**Impact**:
- Custom Client never looks for unified Data.rsdk
- Each game loads separately instead of hybrid experience

### 3. Directory Structure Confusion

**Current Layout**:
```
Sonic-Hybrid-Ultimate/
├── Hybrid-RSDK-Main/          # Mixed: RSDK engines + C# build tools
│   ├── RSDKV3/                # RSDKv3 engine source
│   ├── RSDKV4-Decompilation/  # RSDKv4 engine source
│   ├── RSDKV5/                # RSDKv5 engine source
│   ├── SonicHybridRsdk.*/     # C# build tools (4 projects)
│   ├── sonic-hybrid/          # Should contain Data.rsdk but only has C++ code
│   └── rsdk-source-data/      # Should contain source .rsdk files
├── Custom-Client/             # C# game launcher
└── Sonic 3 AIR Main/          # Separate Sonic 3 engine
```

**Issues**:
- Mixing engine source code with build tools
- Unclear separation of concerns
- Hard to understand what goes where

---

## Recommended Solutions

### Solution 1: Integrate C# Build Tools into CMake

**Add a new CMake step** that:
1. Checks for source .rsdk files
2. Builds the SonicHybridRsdk.Build project
3. Runs it to generate unified Data.rsdk
4. Validates the output

**Implementation**: Add to `Hybrid-RSDK-Main/CMakeLists.txt`

```cmake
# Check if .NET SDK is available
find_program(DOTNET_EXECUTABLE dotnet)
if(DOTNET_EXECUTABLE)
    message(STATUS "Found .NET SDK: ${DOTNET_EXECUTABLE}")
    
    # Check for source .rsdk files
    set(SOURCE_RSDK_DIR "${CMAKE_CURRENT_SOURCE_DIR}/rsdk-source-data")
    set(HYBRID_OUTPUT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/sonic-hybrid")
    
    if(EXISTS "${SOURCE_RSDK_DIR}/soniccd.rsdk" AND 
       EXISTS "${SOURCE_RSDK_DIR}/sonic1.rsdk" AND 
       EXISTS "${SOURCE_RSDK_DIR}/sonic2.rsdk")
        
        message(STATUS "Found source .rsdk files, will generate hybrid data")
        
        # Build and run the hybrid generator
        add_custom_target(hybrid_data_generator ALL
            COMMAND ${DOTNET_EXECUTABLE} build SonicHybridRsdk.Build/SonicHybridRsdk.Build.csproj -c Release
            COMMAND ${DOTNET_EXECUTABLE} run --project SonicHybridRsdk.Build/SonicHybridRsdk.Build.csproj -c Release
            WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}
            COMMENT "Generating unified Sonic Hybrid data..."
        )
        
        # Ensure this runs before building the engines
        add_dependencies(rsdk_core hybrid_data_generator)
        
    else()
        message(WARNING "Source .rsdk files not found in ${SOURCE_RSDK_DIR}")
        message(WARNING "Please place soniccd.rsdk, sonic1.rsdk, and sonic2.rsdk there")
        message(WARNING "Hybrid data generation will be skipped")
    endif()
else()
    message(WARNING ".NET SDK not found - hybrid data generation will be skipped")
endif()
```

### Solution 2: Configure Engines to Use Hybrid Data

**Option A: Modify Custom Client to prioritize hybrid data**

Update `Custom-Client/Program.cs`:

```csharp
internal static class GamePaths
{
    // Primary: Unified hybrid data
    public static readonly string[] HybridSearchPaths = new[]
    {
        Path.Combine("Hybrid-RSDK-Main", "sonic-hybrid", "Data.rsdk"),
        Path.Combine("sonic-hybrid", "Data.rsdk"),
        Path.Combine("..", "Hybrid-RSDK-Main", "sonic-hybrid", "Data.rsdk"),
        Path.Combine("..", "sonic-hybrid", "Data.rsdk"),
    };
    
    // Fallback: Individual game files
    public static readonly string[] Sonic1SearchPaths = new[]
    {
        Path.Combine("Hybrid-RSDK-Main", "rsdk-source-data", "sonic1.rsdk"),
        // ... existing paths
    };
    // ... etc
}
```

**Option B: Create a standalone hybrid launcher**

Create `Hybrid-RSDK-Main/sonic-hybrid/run_hybrid.sh`:

```bash
#!/bin/bash
# Sonic Hybrid Ultimate Launcher
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DATA_FILE="$SCRIPT_DIR/Data.rsdk"

if [ ! -f "$DATA_FILE" ]; then
    echo "Error: Hybrid data file not found: $DATA_FILE"
    echo "Please run the build process to generate it from source .rsdk files"
    exit 1
fi

# Run RSDKv4 engine with hybrid data
cd "$SCRIPT_DIR"
../../build/bin/rsdkv4
```

### Solution 3: Restructure Backend for Clarity

**Proposed Structure**:

```
Sonic-Hybrid-Ultimate/
├── engines/                        # All engine implementations
│   ├── rsdkv3/                    # RSDKv3 for Sonic CD (cloned)
│   ├── rsdkv4/                    # RSDKv4 for Sonic 1/2 (cloned)
│   ├── rsdkv5/                    # RSDKv5 for Mania (cloned)
│   ├── sonic3air/                 # Sonic 3 AIR (Oxygen engine)
│   └── CMakeLists.txt             # Builds all engines
│
├── tools/                         # Build and data generation tools
│   ├── hybrid-generator/          # C# tools for hybrid data
│   │   ├── UnpackScd/
│   │   ├── UnpackS12/
│   │   ├── Generator/
│   │   ├── Build/
│   │   └── SonicHybridRsdk.sln
│   └── CMakeLists.txt             # Builds/runs tools
│
├── games/                         # Generated game data
│   ├── source-data/               # User provides original .rsdk files here
│   │   ├── README.md              # Instructions
│   │   ├── soniccd.rsdk           # User provided
│   │   ├── sonic1.rsdk            # User provided
│   │   └── sonic2.rsdk            # User provided
│   └── hybrid/                    # Generated unified game
│       ├── Data.rsdk              # Generated by tools
│       └── run.sh                 # Launcher script
│
├── launcher/                      # Custom Client (optional)
│   ├── CustomClient.csproj
│   └── ...
│
├── CMakeLists.txt                 # Root build orchestrator
├── build_all.sh                   # Main build script
└── README.md                      # User documentation
```

**Benefits**:
- Clear separation: engines, tools, games, launcher
- Easy to understand what goes where
- Better matches the conceptual architecture
- Easier to maintain and extend

---

## Immediate Action Items

### Priority 1: Get Hybrid Data Generation Working

**File**: `Hybrid-RSDK-Main/CMakeLists.txt`

Add after line 68 (after RSDKV5 check):

```cmake
# Hybrid data generation using C# build tools
find_program(DOTNET_EXECUTABLE dotnet)
if(DOTNET_EXECUTABLE)
    message(STATUS "Found .NET SDK for hybrid data generation")
    
    set(SOURCE_RSDK_DIR "${CMAKE_CURRENT_SOURCE_DIR}/rsdk-source-data")
    set(HYBRID_OUTPUT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/sonic-hybrid")
    
    # Only generate if source files exist
    if(EXISTS "${SOURCE_RSDK_DIR}/soniccd.rsdk" AND 
       EXISTS "${SOURCE_RSDK_DIR}/sonic1.rsdk" AND 
       EXISTS "${SOURCE_RSDK_DIR}/sonic2.rsdk")
        
        message(STATUS "Source .rsdk files found - will generate hybrid data")
        
        add_custom_target(hybrid_data
            COMMAND ${DOTNET_EXECUTABLE} build -c Release
            COMMAND ${DOTNET_EXECUTABLE} run --project SonicHybridRsdk.Build/SonicHybridRsdk.Build.csproj -c Release
            WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}
            COMMENT "Generating unified Sonic Hybrid Ultimate data..."
            VERBATIM
        )
        
        # Make engines depend on hybrid data if available
        add_dependencies(rsdk_core hybrid_data)
        
    else()
        message(STATUS "Source .rsdk files not found in ${SOURCE_RSDK_DIR}")
        message(STATUS "Place soniccd.rsdk, sonic1.rsdk, sonic2.rsdk there to enable hybrid mode")
    endif()
else()
    message(STATUS ".NET SDK not found - hybrid data generation disabled")
endif()
```

### Priority 2: Update Custom Client to Use Hybrid Data

**File**: `Custom-Client/Program.cs`

Add after line 16 (before Sonic1SearchPaths):

```csharp
// Priority: Use unified hybrid data if available
public static readonly string[] HybridSearchPaths = new[]
{
    Path.Combine("Hybrid-RSDK-Main", "sonic-hybrid", "Data.rsdk"),
    Path.Combine("..", "Hybrid-RSDK-Main", "sonic-hybrid", "Data.rsdk"),
    Path.Combine("sonic-hybrid", "Data.rsdk"),
    Path.Combine("..", "sonic-hybrid", "Data.rsdk"),
};
```

Then update the game loading logic to check HybridSearchPaths first.

### Priority 3: Update Build Scripts

**File**: `build_all.sh`

Add after line 89 (after RSDKv4 build):

```bash
# Generate hybrid data if source files are available
if [ -f "rsdk-source-data/soniccd.rsdk" ] && \
   [ -f "rsdk-source-data/sonic1.rsdk" ] && \
   [ -f "rsdk-source-data/sonic2.rsdk" ]; then
    echo "Generating Sonic Hybrid Ultimate data..."
    dotnet run --project SonicHybridRsdk.Build/SonicHybridRsdk.Build.csproj -c Release
    if [ $? -eq 0 ]; then
        echo "✅ Hybrid data generated successfully"
    else
        echo "⚠️  Hybrid data generation failed"
    fi
else
    echo "ℹ️  Source .rsdk files not found - skipping hybrid data generation"
    echo "   Place soniccd.rsdk, sonic1.rsdk, sonic2.rsdk in rsdk-source-data/"
fi
```

### Priority 4: Add User Documentation

**File**: `Hybrid-RSDK-Main/rsdk-source-data/README.md`

Update with clearer instructions:

```markdown
# Source RSDK Files

## What Goes Here

Place your legally obtained Sonic game data files here:

- `soniccd.rsdk` - From Sonic CD (2011 remaster)
- `sonic1.rsdk` - From Sonic the Hedgehog (2013 mobile remaster)
- `sonic2.rsdk` - From Sonic the Hedgehog 2 (2013 mobile remaster)

## How to Obtain

These files come from the official mobile releases available on:
- iOS App Store
- Google Play Store  
- Steam (for Sonic CD)

You must own these games legally. The files are copyrighted and cannot be distributed.

## What Happens Next

When you run the build process with these files present:

1. **UnpackScd** decrypts and extracts Sonic CD data (RSDKv3 format)
2. **UnpackS12** extracts Sonic 1 and Sonic 2 data (RSDKv4 format)
3. **Generator** converts Sonic CD to RSDKv4 and merges all games
4. **Build** orchestrates the process and creates `../sonic-hybrid/Data.rsdk`

The resulting unified data file allows playing all three games seamlessly
in a single continuous experience.

## If You Don't Have These Files

The build will still complete, but you'll only be able to run games individually
if you provide their Data.rsdk files to the engine at runtime.
```

---

## Long-Term Recommendations

### 1. Consider the Xeeynamo Approach

The ARCHITECTURE_PROPOSAL.md correctly identifies that a **simpler approach** may be better:

**Current (Complex)**:
- Custom Client (.NET) → RSDKv4.dll → Native dependencies
- Multi-engine management
- Complex P/Invoke integration
- DLL loading issues on Windows

**Simpler (Xeeynamo style)**:
- Single RSDKv4 executable
- Load unified Data.rsdk directly
- No Custom Client needed
- No DLL issues

**Migration Path**:
1. Get hybrid data generation working (Priority 1 above)
2. Test that standalone rsdkv4 executable works with hybrid Data.rsdk
3. If successful, consider deprecating Custom Client
4. Simplify to single executable + unified data file

### 2. Restructure Backend (As Detailed in Solution 3)

Move from current mixed structure to clearer separation:
- `engines/` - All RSDK variants
- `tools/` - Build and generation tools  
- `games/` - Source and generated data
- `launcher/` - Optional custom client

### 3. Separate Sonic 3 AIR

Sonic 3 AIR (Oxygen engine) is fundamentally different from RSDK:
- Different engine architecture
- Different data format (.bin ROM hacks, not .rsdk)
- Different build system

**Recommendation**: 
- Keep in separate top-level directory
- Build separately
- Don't try to merge with RSDK hybrid
- Custom Client can still launch both, but they're independent

---

## Questions Answered

### 1. What's the correct way to integrate the C# build tools?

**Answer**: Add CMake custom targets (see Priority 1) that:
- Check for .NET SDK
- Verify source .rsdk files exist
- Build SonicHybridRsdk.Build project
- Run it to generate hybrid data
- Make engine builds depend on this step

### 2. How should the RSDK engine be configured to load unified data?

**Answer**: 
- The RSDKv4 wrapper already supports any data path
- Update Custom Client to prioritize `sonic-hybrid/Data.rsdk` 
- OR create a simple launcher script that runs rsdkv4 directly with the hybrid data
- The engine itself doesn't need changes - just point it to the right file

### 3. What changes are needed to make rsdks and roms work properly?

**Answer**:
- **RSDKs**: Build integration (Priority 1) generates unified Data.rsdk from source files
- **ROMs**: Sonic 3 AIR uses ROM files (.bin), not .rsdk - keep it separate
- The confusion comes from mixing RSDK (Sonic 1/2/CD) with ROM-based (Sonic 3)

### 4. How should the backend be restructured?

**Answer**: See Solution 3 for detailed proposal:
- `engines/` - RSDK engine variants (v3, v4, v5) + Sonic 3 AIR
- `tools/` - Hybrid generator C# tools
- `games/` - Source data (user provided) + generated hybrid data
- `launcher/` - Optional custom client (or deprecate)

This creates clear separation and makes the project easier to understand and maintain.

---

## Summary

The C# build tools **exist and are well-written**, but they're **not integrated** into the build process. The fixes are straightforward:

1. ✅ Add CMake steps to build and run hybrid generator
2. ✅ Update Custom Client to look for hybrid data
3. ✅ Update build scripts to generate hybrid data
4. ✅ Improve documentation for users

These changes will make the hybrid experience actually work as intended.

The long-term consideration is whether the Custom Client adds value, or if a simpler "just run rsdkv4 with unified data" approach would be better.
