#!/bin/bash
# build_native_libs.sh - Build native libraries required by Custom-Client

set -e  # Exit on any error

echo "Building native libraries for Sonic Hybrid Ultimate..."

# Check if we're in the right directory
if [ ! -f "CMakeLists.txt" ]; then
    echo "Error: CMakeLists.txt not found. Please run this script from the project root."
    exit 1
fi

# Create build directory
BUILD_DIR="build"
if [ ! -d "$BUILD_DIR" ]; then
    echo "Creating build directory..."
    mkdir -p "$BUILD_DIR"
fi

cd "$BUILD_DIR"

# Configure with CMake
echo "Configuring build with CMake..."
cmake .. -DCMAKE_BUILD_TYPE=Release

# Build the project
echo "Building native libraries..."
cmake --build . --config Release --target OxygenEngine --target RSDKv4

# Check if libraries were built successfully
OXYGEN_LIB=""
RSDK_LIB=""

if [ -f "lib/libOxygenEngine.so" ]; then
    OXYGEN_LIB="lib/libOxygenEngine.so"
elif [ -f "lib/OxygenEngine.dll" ]; then
    OXYGEN_LIB="lib/OxygenEngine.dll"
elif [ -f "bin/OxygenEngine.dll" ]; then
    OXYGEN_LIB="bin/OxygenEngine.dll"
fi

if [ -f "lib/libRSDKv4.so" ]; then
    RSDK_LIB="lib/libRSDKv4.so"
elif [ -f "lib/RSDKv4.dll" ]; then
    RSDK_LIB="lib/RSDKv4.dll"
elif [ -f "bin/RSDKv4.dll" ]; then
    RSDK_LIB="bin/RSDKv4.dll"
fi

if [ -z "$OXYGEN_LIB" ]; then
    echo "Warning: OxygenEngine library not found after build"
else
    echo "✓ OxygenEngine library built: $OXYGEN_LIB"
fi

if [ -z "$RSDK_LIB" ]; then
    echo "Warning: RSDKv4 library not found after build"
else
    echo "✓ RSDKv4 library built: $RSDK_LIB"
fi

# Copy libraries to Custom-Client directory if they exist
CUSTOM_CLIENT_DIR="../Custom-Client"
if [ -n "$OXYGEN_LIB" ] && [ -f "$OXYGEN_LIB" ]; then
    echo "Copying OxygenEngine library to Custom-Client..."
    mkdir -p "$CUSTOM_CLIENT_DIR/bin/Release/net6.0-windows/"
    cp "$OXYGEN_LIB" "$CUSTOM_CLIENT_DIR/bin/Release/net6.0-windows/"
    
    # Also copy to Debug directory
    mkdir -p "$CUSTOM_CLIENT_DIR/bin/Debug/net6.0-windows/"
    cp "$OXYGEN_LIB" "$CUSTOM_CLIENT_DIR/bin/Debug/net6.0-windows/"
fi

if [ -n "$RSDK_LIB" ] && [ -f "$RSDK_LIB" ]; then
    echo "Copying RSDKv4 library to Custom-Client..."
    mkdir -p "$CUSTOM_CLIENT_DIR/bin/Release/net6.0-windows/"
    cp "$RSDK_LIB" "$CUSTOM_CLIENT_DIR/bin/Release/net6.0-windows/"
    
    # Also copy to Debug directory
    mkdir -p "$CUSTOM_CLIENT_DIR/bin/Debug/net6.0-windows/"
    cp "$RSDK_LIB" "$CUSTOM_CLIENT_DIR/bin/Debug/net6.0-windows/"
fi

cd ..

echo "Native library build complete!"
echo ""
echo "Next steps:"
echo "1. Build the Custom-Client C# application"
echo "2. Set up Sonic 3 AIR for Sonic 3 & Knuckles support:"
echo "   - Download Sonic 3 AIR from: https://sonic3air.org/"
echo "   - Extract it to a 'Sonic 3 AIR Main' folder in your project directory"
echo "   - Ensure sonic3air.exe is present and executable"
echo "   - See SONIC3_AIR_SETUP.md for detailed instructions"
echo "3. Place .rsdk files in the expected locations or select them when prompted"
echo "4. Obtain legally acquired ROM files for the games you want to play"