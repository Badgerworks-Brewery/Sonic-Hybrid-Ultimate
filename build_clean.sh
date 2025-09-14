#!/bin/bash

# Clean build script for Sonic Hybrid Ultimate

echo "Cleaning previous build artifacts..."

# Clean Hybrid-RSDK build directory
if [ -d "Hybrid-RSDK Main/build" ]; then
    rm -rf "Hybrid-RSDK Main/build"
fi

# Create fresh build directory
mkdir -p "Hybrid-RSDK Main/build"

echo "Installing dependencies..."
sudo apt-get update
sudo apt-get install -y cmake build-essential pkg-config libsdl2-dev libgl1-mesa-dev libglew-dev libvorbis-dev

echo "Building Hybrid-RSDK..."
cd "Hybrid-RSDK Main/build"
cmake ..
cmake --build .

echo "Building Custom Client..."
cd "../../Custom Client"
dotnet build
