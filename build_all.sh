#!/bin/bash

# Sonic Hybrid Ultimate Build Script
echo "Building Sonic Hybrid Ultimate..."

# Check if we're in the right directory
if [ ! -f "README.md" ]; then
    echo "Error: Please run this script from the project root directory"
    exit 1
fi

# Fetch RSDK decompilations
echo "Fetching RSDK decompilations..."
chmod +x fetch_rsdkv3.sh fetch_rsdkv4.sh fetch_rsdkv5.sh
./fetch_rsdkv4.sh
./fetch_rsdkv3.sh
./fetch_rsdkv5.sh

# Build Hybrid RSDK engine
echo "Building Hybrid RSDK engine..."
cd "Hybrid-RSDK Main"

# Clean previous build
rm -rf build
mkdir -p build
cd build

# Configure and build
cmake ..
if [ $? -ne 0 ]; then
    echo "Error: CMake configuration failed"
    exit 1
fi

cmake --build .
if [ $? -ne 0 ]; then
    echo "Error: Build failed"
    exit 1
fi

cd ../..

# Build Custom Client
echo "Building Custom Client..."
cd "Custom Client"
dotnet build
if [ $? -ne 0 ]; then
    echo "Error: Custom Client build failed"
    exit 1
fi

cd ..

echo "Build completed successfully!"
echo "Executables are located in:"
echo "  - Hybrid-RSDK Main/build/bin/"
echo "  - Custom Client/bin/"
