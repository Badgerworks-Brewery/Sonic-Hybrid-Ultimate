#!/bin/bash

# Fetch RSDKv5 Decompilation (Universal)
echo "Fetching RSDKv5 Decompilation..."

# Get the directory of this script
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(realpath "$SCRIPT_DIR/..")"  

RSDK_DIR="$ROOT_DIR/Hybrid-RSDK-Main/RSDKV5"

if [ ! -d "$RSDK_DIR" ]; then
    git clone -b master --depth 1 https://github.com/RSDKModding/RSDKv5-Decompilation.git "$RSDK_DIR"
    cd "$RSDK_DIR" || exit
    echo "RSDKv5 Decompilation fetched successfully"
else
    echo "RSDKv5 already exists, updating..."
    cd "$RSDK_DIR" && git fetch && git reset --hard origin/master
fi
