#!/bin/bash

# Fetch RSDKv5 Decompilation (Universal)
echo "Fetching RSDKv5 Decompilation..."

if [ ! -d "Hybrid-RSDK Main/RSDKV5" ]; then
    git clone https://github.com/RSDKModding/RSDKv5-Decompilation.git "Hybrid-RSDK Main/RSDKV5"
    cd "Hybrid-RSDK Main/RSDKV5"
    git checkout main
    cd ../..
    echo "RSDKv5 Decompilation fetched successfully"
else
    echo "RSDKv5 already exists, updating..."
    cd "Hybrid-RSDK Main/RSDKV5" && git pull && cd ../..
fi
