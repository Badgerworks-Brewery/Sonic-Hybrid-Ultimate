#!/bin/bash
# Script to apply Team Forever RSDKv4 enhancements
# This applies local modifications to the RSDKv4 submodule
# NOTE: Video support patch is optional - build will continue without it

echo "Applying Team Forever RSDKv4 enhancements..."

cd "$(dirname "$0")/Hybrid-RSDK-Main/RSDKV4-Decompilation"

# Check if patch has already been applied
if [ -f "RSDKv4/Video.cpp" ] && [ -f "RSDKv4/Video.hpp" ]; then
    echo "Team Forever enhancements already applied, skipping..."
else
    # Try to apply the patch (non-fatal)
    if [ -f "../../teamforever-rsdkv4.patch" ]; then
        echo "Attempting to apply teamforever-rsdkv4.patch..."
        if git apply ../../teamforever-rsdkv4.patch 2>&1; then
            echo "Team Forever enhancements applied successfully!"
            
            # Apply video fix patch
            if [ -f "../../video-fix.patch" ]; then
                if git apply ../../video-fix.patch 2>&1; then
                    echo "Video.cpp fixes applied successfully!"
                else
                    echo "Warning: Could not apply video-fix.patch, continuing without video support"
                fi
            fi
        else
            echo "Warning: Could not apply teamforever-rsdkv4.patch"
            echo "Continuing build without video playback support"
            echo "The game will still build and run, but video cutscenes will be unavailable"
        fi
    else
        echo "Warning: teamforever-rsdkv4.patch not found, continuing without video support"
    fi
fi

# Clone theoraplay if not present (optional dependency)
if [ ! -d "dependencies/all/theoraplay/.git" ]; then
    echo "Cloning theoraplay dependency..."
    rm -rf dependencies/all/theoraplay
    git clone --depth=1 https://github.com/icculus/theoraplay.git dependencies/all/theoraplay 2>/dev/null || echo "Warning: Could not clone theoraplay (video support unavailable)"
fi

echo "Done! Build configuration complete."
exit 0
