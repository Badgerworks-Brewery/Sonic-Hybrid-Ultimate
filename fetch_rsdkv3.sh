#!/bin/bash

# Fetch RSDKv3 Decompilation for Sonic CD
echo "Fetching RSDKv3 Decompilation..."

if [ ! -d "Hybrid-RSDK-Main/RSDKV3" ]; then
    git clone https://github.com/RSDKModding/RSDKv3-Decompilation.git "Hybrid-RSDK-Main/RSDKV3"
    cd "Hybrid-RSDK-Main/RSDKV3"
    git checkout main
    cd ../..
    echo "RSDKv3 Decompilation fetched successfully"
else
    echo "RSDKv3 already exists, updating..."
    cd "Hybrid-RSDK-Main/RSDKV3" && git pull && cd ../..
fi
