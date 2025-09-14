#!/bin/bash

# Setup script for Sonic Hybrid Ultimate

set -e

echo "=== Sonic Hybrid Ultimate Setup ==="

# Make all scripts executable
echo "Making scripts executable..."
chmod +x build_clean.sh
chmod +x fetch_rsdkv3.sh
chmod +x fetch_rsdkv5.sh

# Initialize git submodules
echo "Initializing git submodules..."
git submodule update --init --recursive

# Check if Sonic 3 AIR directory exists and is empty (indicating it should be a submodule)
if [ -d "Sonic 3 AIR Main" ] && [ ! -d "Sonic 3 AIR Main/.git" ]; then
    echo "Converting Sonic 3 AIR to submodule..."
    rm -rf "Sonic 3 AIR Main"
    git submodule add https://github.com/Eukaryot/sonic3air.git "Sonic 3 AIR Main"
fi

# Create necessary directories
echo "Creating necessary directories..."
mkdir -p "Hybrid-RSDK Main/rsdk-source-data"
mkdir -p "Hybrid-RSDK Main/build"

# Check for .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo "Warning: .NET SDK not found. Please install .NET 6.0 SDK."
    echo "Download from: https://dotnet.microsoft.com/download/dotnet/6.0"
fi

# Check for CMake
if ! command -v cmake &> /dev/null; then
    echo "Warning: CMake not found. Please install CMake."
fi

echo ""
echo "Setup complete!"
echo "Next steps:"
echo "1. Place your RSDK data files in 'Hybrid-RSDK Main/rsdk-source-data/'"
echo "2. Run './build_clean.sh' to build the project"
