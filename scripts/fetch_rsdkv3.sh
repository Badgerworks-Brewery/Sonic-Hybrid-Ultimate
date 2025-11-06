#!/bin/bash

# Fetch RSDKv3 Decompilation (Universal)
echo "Fetching RSDKv3 Decompilation..."

# Get the directory of this script
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(realpath "$SCRIPT_DIR/..")" 

RSDK_DIR="$ROOT_DIR/Hybrid-RSDK-Main/RSDKV3"

if [ ! -d "$RSDK_DIR" ]; then
    git clone -b master --depth 1 https://github.com/RSDKModding/RSDKv3-Decompilation.git "$RSDK_DIR"
    cd "$RSDK_DIR" || exit
    echo "RSDKv3 Decompilation fetched successfully"
else
    echo "RSDKv3 already exists, updating..."
    cd "$RSDK_DIR" && git fetch && git reset --hard origin/master
fi
