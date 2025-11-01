#!/bin/bash

# Fetch RSDKv4 Decompilation for Sonic 1 and 2
echo "Fetching RSDKv4 Decompilation..."

if [ ! -d "Hybrid-RSDK-Main/RSDKV4-Decompilation" ]; then
    git clone https://github.com/RSDKModding/RSDKv4-Decompilation.git "Hybrid-RSDK-Main/RSDKV4-Decompilation"
    cd "Hybrid-RSDK-Main/RSDKV4-Decompilation"
    git checkout main
    cd ../..
    echo "RSDKv4 Decompilation fetched successfully"
else
    echo "RSDKv4 already exists, updating..."
    cd "Hybrid-RSDK-Main/RSDKV4-Decompilation" && git fetch && git reset --hard origin/main && cd ../..
fi
