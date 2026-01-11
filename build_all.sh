#!/bin/bash

# Sonic Hybrid Ultimate Build Script
echo "Building Sonic Hybrid Ultimate..."

# Check if we're in the right directory
if [ ! -f "README.md" ]; then
    echo "Error: Please run this script from the project root directory"
    exit 1
fi

# Check for required tools
if ! command -v cmake &> /dev/null; then
    echo "Error: CMake is required but not installed"
    exit 1
fi

if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET 6.0 SDK is required but not installed"
    exit 1
fi

# Initialize submodules
echo "Initializing submodules..."
git submodule update --init --recursive

# Fetch RSDK decompilations
echo "Fetching RSDK decompilations..."

# Get the script directory to ensure we can find the fetch scripts
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Make fetch scripts executable
chmod +x "${SCRIPT_DIR}/fetch_rsdkv3.sh" "${SCRIPT_DIR}/fetch_rsdkv4.sh" "${SCRIPT_DIR}/fetch_rsdkv5.sh"

# Fetch RSDKv4 (required)
echo "Fetching RSDKv4 Decompilation..."
"${SCRIPT_DIR}/fetch_rsdkv4.sh"
if [ $? -ne 0 ]; then
    echo "Error: Failed to fetch RSDKv4 Decompilation (required)"
    exit 1
fi

# Fetch RSDKv3 (optional - for Sonic CD support)
echo "Fetching RSDKv3 Decompilation (optional)..."
"${SCRIPT_DIR}/fetch_rsdkv3.sh"
if [ $? -ne 0 ]; then
    echo "Warning: Failed to fetch RSDKv3 Decompilation (Sonic CD support will be unavailable)"
fi

# Fetch RSDKv5 (optional - for newer games support)
echo "Fetching RSDKv5 Decompilation (optional)..."
"${SCRIPT_DIR}/fetch_rsdkv5.sh"
if [ $? -ne 0 ]; then
    echo "Warning: Failed to fetch RSDKv5 Decompilation (newer games support will be unavailable)"
fi

# Apply Team Forever enhancements (optional - provides video playback and mod support)
echo "Applying Team Forever RSDKv4 enhancements..."
chmod +x "${SCRIPT_DIR}/apply_teamforever.sh"
"${SCRIPT_DIR}/apply_teamforever.sh"
# Script always exits 0 now - patch application is optional

# Build Hybrid-RSDK-Main engine
echo "Building Hybrid-RSDK-Main engine..."
cd "Hybrid-RSDK-Main"

# Clean previous build
rm -rf build
mkdir -p build
cd build

# Configure and build
echo "Configuring CMake..."
cmake ..
if [ $? -ne 0 ]; then
    echo "Error: CMake configuration failed"
    echo "Please check that all dependencies are installed:"
    echo "  sudo apt-get install -y cmake build-essential pkg-config libsdl2-dev libgl1-mesa-dev libglew-dev libvorbis-dev libtinyxml2-dev libogg-dev libtheora-dev"
    exit 1
fi

echo "Building native components..."
cmake --build . --config Release
if [ $? -ne 0 ]; then
    echo "Error: Build failed"
    exit 1
fi

cd ../..

# Generate hybrid data if source files are available
echo ""
echo "Checking for hybrid data generation..."
if [ -f "Hybrid-RSDK-Main/rsdk-source-data/soniccd.rsdk" ] && \
   [ -f "Hybrid-RSDK-Main/rsdk-source-data/sonic1.rsdk" ] && \
   [ -f "Hybrid-RSDK-Main/rsdk-source-data/sonic2.rsdk" ]; then
    echo "Source .rsdk files found - generating Sonic Hybrid Ultimate data..."
    cd "Hybrid-RSDK-Main"
    
    # Build the generator
    dotnet build SonicHybridRsdk.Build/SonicHybridRsdk.Build.csproj -c Release
    if [ $? -ne 0 ]; then
        echo "⚠️  Failed to build hybrid data generator"
    else
        # Run the generator
        dotnet run --project SonicHybridRsdk.Build/SonicHybridRsdk.Build.csproj -c Release --no-build
        if [ $? -eq 0 ]; then
            echo "✅ Hybrid data generated successfully at sonic-hybrid/Data.rsdk"
        else
            echo "⚠️  Hybrid data generation failed - check for errors above"
        fi
    fi
    
    cd ..
else
    echo "ℹ️  Source .rsdk files not found in Hybrid-RSDK-Main/rsdk-source-data/"
    echo "   To enable hybrid mode, place the following files there:"
    echo "     - soniccd.rsdk (from Sonic CD)"
    echo "     - sonic1.rsdk (from Sonic 1)"
    echo "     - sonic2.rsdk (from Sonic 2)"
    echo "   See Hybrid-RSDK-Main/rsdk-source-data/README.md for details"
fi
echo ""

# Build Custom Client
echo "Building Custom Client..."
cd "Custom-Client"
dotnet build --configuration Release
if [ $? -ne 0 ]; then
    echo "Error: Custom Client build failed"
    exit 1
fi

cd ..

echo ""
echo "🎉 Build completed successfully!"
echo ""
echo "Executables are located in:"
echo "  - Hybrid-RSDK-Main/build/bin/"
echo "  - Custom-Client/bin/"
echo ""
echo "📋 IMPORTANT: To run the games, you need to provide the following files:"
echo ""
echo "For Sonic 1 & 2:"
echo "  - Place Data.rsdk files in the executable directory"
echo "  - Obtain these from your legally owned copies of Sonic 1 & 2"
echo ""
echo "For Sonic CD (if RSDKv3 was built):"
echo "  - Place Data.rsdk file from Sonic CD in the executable directory"
echo ""
echo "For newer Sonic games (if RSDKv5 was built):"
echo "  - Place appropriate Data.rsdk files in the executable directory"
echo ""
echo "The build system intentionally does not include these files."
echo "You must provide them from your legally purchased copies of the games."
