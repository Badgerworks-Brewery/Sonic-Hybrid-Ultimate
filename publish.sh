#!/bin/bash
set -e

echo "Building Sonic Hybrid Ultimate as a single executable..."

# Step 1: Build native libraries
echo "Step 1: Building native RSDK libraries..."
cd Hybrid-RSDK-Main
./build_all.sh || true  # Continue even if build has warnings

# Step 2: Build and publish Custom Client as single executable
echo "Step 2: Publishing Custom Client as single executable..."
cd ../Custom-Client

# Publish for Windows
echo "Publishing for Windows (x64)..."
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true

# Publish for Linux (optional)
# echo "Publishing for Linux (x64)..."
# dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

echo ""
echo "Build complete!"
echo "Single executable output:"
echo "  Windows: Custom-Client/bin/Release/net6.0-windows/win-x64/publish/SonicHybrid.exe"
echo ""
echo "Run the game:"
echo "  cd Custom-Client/bin/Release/net6.0-windows/win-x64/publish"
echo "  ./SonicHybrid.exe"
