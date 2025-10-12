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
chmod +x fetch_rsdkv3.sh fetch_rsdkv4.sh fetch_rsdkv5.sh
./fetch_rsdkv4.sh
./fetch_rsdkv3.sh
./fetch_rsdkv5.sh

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
    echo "  sudo apt-get install -y cmake build-essential pkg-config libsdl2-dev libgl1-mesa-dev libglew-dev libvorbis-dev libtinyxml2-dev"
    exit 1
fi

echo "Building native components..."
cmake --build . --config Release
if [ $? -ne 0 ]; then
    echo "Error: Build failed"
    exit 1
fi

cd ../..

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
