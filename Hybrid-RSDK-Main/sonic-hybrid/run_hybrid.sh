#!/bin/bash
# Sonic Hybrid Ultimate Launcher
# This script runs the unified Sonic 1 + CD + 2 hybrid experience

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DATA_FILE="$SCRIPT_DIR/Data.rsdk"
ENGINE_BINARY="$SCRIPT_DIR/../../build/bin/rsdkv4"

echo "==================================================================="
echo "  Sonic Hybrid Ultimate Launcher"
echo "==================================================================="
echo ""

# Check if hybrid data exists
if [ ! -f "$DATA_FILE" ]; then
    echo "❌ Error: Hybrid data file not found!"
    echo ""
    echo "Expected: $DATA_FILE"
    echo ""
    echo "To generate the hybrid data:"
    echo "  1. Place soniccd.rsdk, sonic1.rsdk, sonic2.rsdk in ../rsdk-source-data/"
    echo "  2. Run the build process (build_all.sh or cmake --build)"
    echo ""
    echo "See ../rsdk-source-data/README.md for detailed instructions."
    exit 1
fi

echo "✅ Found hybrid data: $DATA_FILE"

# Check if engine binary exists
if [ ! -f "$ENGINE_BINARY" ]; then
    echo "❌ Error: RSDKv4 engine not found!"
    echo ""
    echo "Expected: $ENGINE_BINARY"
    echo ""
    echo "Please build the project first using build_all.sh"
    exit 1
fi

echo "✅ Found RSDKv4 engine: $ENGINE_BINARY"
echo ""
echo "Starting Sonic Hybrid Ultimate..."
echo "  (Sonic 1 → Sonic CD → Sonic 2)"
echo ""

# Change to the data directory so the engine can find Data.rsdk
cd "$SCRIPT_DIR"

# Run the engine
"$ENGINE_BINARY"

echo ""
echo "Game exited."
