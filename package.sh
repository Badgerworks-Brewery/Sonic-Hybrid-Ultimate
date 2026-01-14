#!/bin/bash
# Package script for Sonic Hybrid Ultimate - Creates single-folder distribution
set -e

echo "========================================="
echo "Sonic Hybrid Ultimate - Package Builder"
echo "========================================="
echo ""

# Detect platform
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    PLATFORM="linux"
    RID="linux-x64"
    LIB_EXT="so"
    EXE_EXT=""
elif [[ "$OSTYPE" == "darwin"* ]]; then
    PLATFORM="osx"
    RID="osx-x64"
    LIB_EXT="dylib"
    EXE_EXT=""
elif [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "cygwin" ]] || [[ "$OSTYPE" == "win32" ]]; then
    PLATFORM="windows"
    RID="win-x64"
    LIB_EXT="dll"
    EXE_EXT=".exe"
else
    echo "ERROR: Unsupported platform: $OSTYPE"
    exit 1
fi

echo "Platform: $PLATFORM ($RID)"
echo ""

# Configuration
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT_DIR="$PROJECT_ROOT/dist/SonicHybridUltimate"
BUILD_DIR="$PROJECT_ROOT/Hybrid-RSDK-Main/build"
CLIENT_DIR="$PROJECT_ROOT/Custom-Client"

echo "Step 1: Building native libraries..."
echo "-----------------------------------"
cd "$PROJECT_ROOT/Hybrid-RSDK-Main"
if [ ! -d "build" ]; then
    mkdir build
fi
cd build
cmake .. -DCMAKE_BUILD_TYPE=Release
cmake --build . --config Release -j$(nproc 2>/dev/null || sysctl -n hw.ncpu 2>/dev/null || echo 4)
echo "✓ Native libraries built"
echo ""

echo "Step 2: Building C# build tools..."
echo "-----------------------------------"
cd "$PROJECT_ROOT/Hybrid-RSDK-Main"
dotnet build SonicHybridRsdk.sln -c Release
echo "✓ C# build tools compiled"
echo ""

echo "Step 3: Building Custom Client..."
echo "-----------------------------------"
cd "$CLIENT_DIR"
dotnet publish -c Release -r $RID --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true
echo "✓ Custom Client built"
echo ""

echo "Step 4: Creating distribution directory..."
echo "-----------------------------------"
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR/GameData"
echo "✓ Distribution directory created: $OUTPUT_DIR"
echo ""

echo "Step 5: Copying executable..."
echo "-----------------------------------"
PUBLISH_DIR="$CLIENT_DIR/bin/Release/net6.0-windows/$RID/publish"
if [ ! -d "$PUBLISH_DIR" ]; then
    # Try alternative path without RID
    PUBLISH_DIR="$CLIENT_DIR/bin/Release/net6.0-windows/publish"
fi

if [ -f "$PUBLISH_DIR/SonicHybrid$EXE_EXT" ]; then
    cp "$PUBLISH_DIR/SonicHybrid$EXE_EXT" "$OUTPUT_DIR/"
    echo "✓ Copied SonicHybrid$EXE_EXT"
else
    echo "ERROR: SonicHybrid$EXE_EXT not found in $PUBLISH_DIR"
    exit 1
fi

# Copy all DLLs from publish directory
cp "$PUBLISH_DIR"/*.dll "$OUTPUT_DIR/" 2>/dev/null || true
echo "✓ Copied .NET runtime libraries"
echo ""

echo "Step 6: Copying native libraries..."
echo "-----------------------------------"
# Copy RSDK libraries
if [ -f "$BUILD_DIR/lib/libRSDKv4.$LIB_EXT" ]; then
    cp "$BUILD_DIR/lib/libRSDKv4.$LIB_EXT" "$OUTPUT_DIR/"
    echo "✓ Copied libRSDKv4.$LIB_EXT"
elif [ -f "$BUILD_DIR/lib/RSDKv4.$LIB_EXT" ]; then
    cp "$BUILD_DIR/lib/RSDKv4.$LIB_EXT" "$OUTPUT_DIR/"
    echo "✓ Copied RSDKv4.$LIB_EXT"
fi

# Copy OxygenEngine library
if [ -f "$BUILD_DIR/lib/libOxygenEngine.$LIB_EXT" ]; then
    cp "$BUILD_DIR/lib/libOxygenEngine.$LIB_EXT" "$OUTPUT_DIR/"
    echo "✓ Copied libOxygenEngine.$LIB_EXT"
elif [ -f "$BUILD_DIR/lib/OxygenEngine.$LIB_EXT" ]; then
    cp "$BUILD_DIR/lib/OxygenEngine.$LIB_EXT" "$OUTPUT_DIR/"
    echo "✓ Copied OxygenEngine.$LIB_EXT"
fi

# Copy all dependency libraries from build/bin
if [ -d "$BUILD_DIR/bin/Release" ]; then
    cp "$BUILD_DIR/bin/Release"/*.$LIB_EXT "$OUTPUT_DIR/" 2>/dev/null || true
    echo "✓ Copied dependency libraries from bin/Release"
elif [ -d "$BUILD_DIR/bin" ]; then
    cp "$BUILD_DIR/bin"/*.$LIB_EXT "$OUTPUT_DIR/" 2>/dev/null || true
    echo "✓ Copied dependency libraries from bin"
fi

# Copy system dependencies if on Linux
if [ "$PLATFORM" == "linux" ]; then
    # List required libraries
    echo "Checking for system dependencies..."
    for lib in libSDL2 libGLEW libGL libogg libvorbis libtheora; do
        LIB_FILE=$(ldconfig -p | grep "$lib" | head -1 | awk '{print $NF}')
        if [ -n "$LIB_FILE" ]; then
            cp "$LIB_FILE" "$OUTPUT_DIR/" 2>/dev/null || echo "  Note: Could not copy $lib (may need sudo)"
        fi
    done
fi
echo ""

echo "Step 6.5: Copying Sonic 3 AIR files..."
echo "-----------------------------------"

# Copy Sonic 3 AIR executable from build
SONIC3AIR_EXE="$BUILD_DIR/bin/sonic3air$EXE_EXT"
if [ -f "$SONIC3AIR_EXE" ]; then
    cp "$SONIC3AIR_EXE" "$OUTPUT_DIR/"
    echo "✓ Copied sonic3air$EXE_EXT"
else
    # Try alternative location
    SONIC3AIR_EXE="$BUILD_DIR/sonic3air/sonic3air_linux"
    if [ -f "$SONIC3AIR_EXE" ]; then
        cp "$SONIC3AIR_EXE" "$OUTPUT_DIR/sonic3air$EXE_EXT"
        chmod +x "$OUTPUT_DIR/sonic3air$EXE_EXT"
        echo "✓ Copied sonic3air (from alternate location)"
    else
        echo "⚠ sonic3air executable not found - Sonic 3 will run in stub mode"
    fi
fi

# Copy Sonic 3 AIR data files
S3AIR_SOURCE="$PROJECT_ROOT/vendor/sonic3air/Oxygen/sonic3air"
S3AIR_DATA="$S3AIR_SOURCE/data"
S3AIR_SCRIPTS="$S3AIR_SOURCE/scripts"
S3AIR_INTERNAL="$S3AIR_SOURCE/___internal"

if [ -d "$S3AIR_DATA" ]; then
    mkdir -p "$OUTPUT_DIR/data"
    cp -r "$S3AIR_DATA"/* "$OUTPUT_DIR/data/" 2>/dev/null || true
    echo "✓ Copied Sonic 3 AIR data files"
fi

if [ -d "$S3AIR_SCRIPTS" ]; then
    mkdir -p "$OUTPUT_DIR/scripts"
    cp -r "$S3AIR_SCRIPTS"/* "$OUTPUT_DIR/scripts/" 2>/dev/null || true
    echo "✓ Copied Sonic 3 AIR scripts"
fi

if [ -d "$S3AIR_INTERNAL" ]; then
    mkdir -p "$OUTPUT_DIR/___internal"
    cp -r "$S3AIR_INTERNAL"/* "$OUTPUT_DIR/___internal/" 2>/dev/null || true
    echo "✓ Copied Sonic 3 AIR internal files"
fi

echo ""

echo "Step 7: Creating GameData readme..."
echo "-----------------------------------"
cat > "$OUTPUT_DIR/GameData/README.txt" << 'EOF'
Sonic Hybrid Ultimate - Game Data Folder
=========================================

Place your legally obtained game files here:

Required Files:
  - sonic1.rsdk   (from Sonic 1 mobile/remaster)
  - sonic2.rsdk   (from Sonic 2 mobile/remaster)
  - soniccd.rsdk  (from Sonic CD mobile/remaster)
  - sonic3.bin    (Sonic 3 & Knuckles ROM - your legally obtained copy)

How to obtain these files:
  1. Purchase the games from Steam, Google Play, or App Store
  2. Extract the .rsdk files from your purchased games
  3. For Sonic 3, you need a Sonic 3 & Knuckles ROM file

Once you have placed these files here, run SonicHybrid to start playing!

Note: Due to copyright, we cannot provide these files. You must obtain them
legally from your own purchased copies of the games.
EOF
echo "✓ Created GameData/README.txt"
echo ""

echo "Step 8: Creating launcher script..."
echo "-----------------------------------"
cat > "$OUTPUT_DIR/run.sh" << 'EOF'
#!/bin/bash
# Sonic Hybrid Ultimate launcher for Linux/macOS

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Set library path for native dependencies
export LD_LIBRARY_PATH="$SCRIPT_DIR:$LD_LIBRARY_PATH"
export DYLD_LIBRARY_PATH="$SCRIPT_DIR:$DYLD_LIBRARY_PATH"

# Run the game
./SonicHybrid
EOF
chmod +x "$OUTPUT_DIR/run.sh"
echo "✓ Created run.sh"

cat > "$OUTPUT_DIR/run.bat" << 'EOF'
@echo off
REM Sonic Hybrid Ultimate launcher for Windows

cd /d "%~dp0"
start SonicHybrid.exe
EOF
echo "✓ Created run.bat"
echo ""

echo "Step 9: Creating usage documentation..."
echo "-----------------------------------"
cat > "$OUTPUT_DIR/README.txt" << 'EOF'
========================================
   Sonic Hybrid Ultimate
========================================

Welcome to Sonic Hybrid Ultimate - A unified Sonic experience!

QUICK START:
------------
1. Place your game files in the GameData folder:
   - sonic1.rsdk, sonic2.rsdk, soniccd.rsdk, sonic3.bin

2. Run the game:
   - Windows: Double-click run.bat or SonicHybrid.exe
   - Linux/macOS: Run ./run.sh from terminal

GAME FILES:
-----------
You need legally obtained copies of:
  • Sonic the Hedgehog 1 (Data.rsdk → sonic1.rsdk)
  • Sonic the Hedgehog 2 (Data.rsdk → sonic2.rsdk)
  • Sonic CD (Data.rsdk → soniccd.rsdk)
  • Sonic 3 & Knuckles ROM (sonic3.bin)

These files are NOT included due to copyright.

CONTROLS:
---------
Use the in-game menu to configure controls.
Default keyboard controls are typically:
  Arrow Keys - Movement
  A/Z - Jump
  S/X - Action
  Enter - Pause

TROUBLESHOOTING:
---------------
If the game doesn't start:
  1. Check that all game files are in GameData folder
  2. Make sure you have required system libraries:
     - SDL2
     - OpenGL
     - GLEW
     - Ogg/Vorbis/Theora (for video playback)

For more information, visit:
https://github.com/Badgerworks-Brewery/Sonic-Hybrid-Ultimate

LEGAL:
------
Sonic Hybrid Ultimate is a fan project. Sonic and related characters
are property of SEGA. You must own legal copies of all games to use
this software.
EOF
echo "✓ Created README.txt"
echo ""

echo "========================================="
echo "✓ Package build complete!"
echo "========================================="
echo ""
echo "Distribution created at:"
echo "  $OUTPUT_DIR"
echo ""
echo "Contents:"
ls -lh "$OUTPUT_DIR"
echo ""
echo "To distribute:"
echo "  1. Add your game files to GameData folder (for testing)"
echo "  2. Zip the SonicHybridUltimate folder"
echo "  3. Share with users (without game files)"
echo ""
