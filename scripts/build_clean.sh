#!/bin/bash

# Clean build script for Sonic Hybrid Ultimate

set -e  # Exit on any error

echo "=== Sonic Hybrid Ultimate Build Script ==="

# Function to check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Function to install dependencies on Ubuntu/Debian
install_ubuntu_deps() {
    echo "Installing Ubuntu/Debian dependencies..."
    sudo apt-get update
    sudo apt-get install -y \
        cmake \
        build-essential \
        pkg-config \
        libsdl2-dev \
        libgl1-mesa-dev \
        libglew-dev \
        libvorbis-dev \
        git \
        wget \
        curl
}

# Function to install dependencies on macOS
install_macos_deps() {
    echo "Installing macOS dependencies..."
    if command_exists brew; then
        brew install cmake sdl2 glew libvorbis pkg-config
    else
        echo "Homebrew not found. Please install Homebrew first."
        exit 1
    fi
}

# Detect OS and install dependencies
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    if command_exists apt-get; then
        install_ubuntu_deps
    else
        echo "Unsupported Linux distribution. Please install dependencies manually."
        exit 1
    fi
elif [[ "$OSTYPE" == "darwin"* ]]; then
    install_macos_deps
else
    echo "Unsupported OS: $OSTYPE"
    exit 1
fi

# Check for required tools
echo "Checking for required tools..."
for tool in cmake git dotnet; do
    if ! command_exists $tool; then
        echo "Error: $tool is not installed or not in PATH"
        exit 1
    fi
done

echo "All required tools found."

# Make fetch scripts executable
chmod +x fetch_rsdkv3.sh fetch_rsdkv5.sh

# Fetch RSDK decompilations if they don't exist
echo "Fetching RSDK decompilations..."

if [ ! -d "Hybrid-RSDK-Main/RSDKV3" ]; then
    echo "Fetching RSDKv3..."
    ./fetch_rsdkv3.sh
fi

if [ ! -d "Hybrid-RSDK-Main/RSDKV5" ]; then
    echo "Fetching RSDKv5..."
    ./fetch_rsdkv5.sh
fi

# Clean previous build artifacts
echo "Cleaning previous build artifacts..."

if [ -d "Hybrid-RSDK-Main/build" ]; then
    rm -rf "Hybrid-RSDK-Main/build"
fi

# Create fresh build directory
mkdir -p "Hybrid-RSDK-Main/build"

echo "Building Hybrid-RSDK..."
cd "Hybrid-RSDK-Main/build"

# Configure CMake
cmake .. -DCMAKE_BUILD_TYPE=Release

# Build the project
cmake --build . --config Release

echo "Building Custom Client..."
cd "../../Custom-Client"

# Restore NuGet packages and build
dotnet restore
dotnet build --configuration Release

echo "=== Build Complete ==="
echo ""
echo "Built components:"
echo "  - Hybrid-RSDK-Main/build/bin/rsdkv4"
echo "  - Hybrid-RSDK-Main/build/lib/libHybridRSDK.so (or .dll/.dylib)"
echo "  - Custom-Client/bin/Release/net6.0-windows/SonicHybrid.exe"
echo ""
echo "To run the project:"
echo "  1. Place your RSDK data files in 'Hybrid-RSDK-Main/rsdk-source-data/'"
echo "  2. Run the Custom Client executable"
echo ""
echo "Required data files:"
echo "  - sonic1.rsdk (Sonic 1 data)"
echo "  - sonic2.rsdk (Sonic 2 data)"
echo "  - soniccd.rsdk (Sonic CD data)"
echo "  - sonic3.bin (Sonic 3 & Knuckles ROM)"
