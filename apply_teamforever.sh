#!/bin/bash
# Script to apply Team Forever RSDKv4 enhancements
# This applies local modifications to the RSDKv4 submodule

set -e

echo "Applying Team Forever RSDKv4 enhancements..."

cd "$(dirname "$0")/Hybrid-RSDK-Main/RSDKV4-Decompilation"

# Check if patch has already been applied
if [ -f "RSDKv4/Video.cpp" ] && [ -f "RSDKv4/Video.hpp" ]; then
    echo "Team Forever enhancements already applied, skipping..."
else
    # Apply the patch
    if [ -f "../../teamforever-rsdkv4.patch" ]; then
        git apply ../../teamforever-rsdkv4.patch
        echo "Team Forever enhancements applied successfully!"
    else
        echo "Error: teamforever-rsdkv4.patch not found"
        exit 1
    fi
fi

# Clone theoraplay if not present
if [ ! -d "dependencies/all/theoraplay/.git" ]; then
    echo "Cloning theoraplay dependency..."
    rm -rf dependencies/all/theoraplay
    git clone --depth=1 https://github.com/icculus/theoraplay.git dependencies/all/theoraplay
fi

echo "Done! Team Forever enhancements are ready."
