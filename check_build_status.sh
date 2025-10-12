#!/bin/bash

# Sonic Hybrid Ultimate Build Status Check
echo "Checking Sonic Hybrid Ultimate build status..."
echo "=============================================="

# Check if we're in the right directory
if [ ! -f "README.md" ]; then
    echo "❌ Error: Please run this script from the project root directory"
    exit 1
fi

# Check for required tools
echo "Checking required tools..."

if command -v cmake &> /dev/null; then
    echo "✅ CMake found: $(cmake --version | head -n1)"
else
    echo "❌ CMake not found"
fi

if command -v dotnet &> /dev/null; then
    echo "✅ .NET found: $(dotnet --version)"
else
    echo "❌ .NET not found"
fi

if command -v git &> /dev/null; then
    echo "✅ Git found: $(git --version)"
else
    echo "❌ Git not found"
fi

# Check for dependencies (Linux)
if command -v pkg-config &> /dev/null; then
    echo "✅ pkg-config found"

    if pkg-config --exists sdl2; then
        echo "✅ SDL2 found: $(pkg-config --modversion sdl2)"
    else
        echo "❌ SDL2 not found"
    fi

    if pkg-config --exists gl; then
        echo "✅ OpenGL found"
    else
        echo "❌ OpenGL not found"
    fi

    if pkg-config --exists glew; then
        echo "✅ GLEW found: $(pkg-config --modversion glew)"
    else
        echo "❌ GLEW not found"
    fi

    if pkg-config --exists vorbis; then
        echo "✅ Vorbis found: $(pkg-config --modversion vorbis)"
    else
        echo "❌ Vorbis not found"
    fi

    if pkg-config --exists tinyxml2; then
        echo "✅ TinyXML2 found: $(pkg-config --modversion tinyxml2)"
    else
        echo "❌ TinyXML2 not found"
    fi
else
    echo "❌ pkg-config not found (Linux dependencies cannot be checked)"
fi

# Check submodules
echo ""
echo "Checking submodules..."

if [ -d "Sonic 3 AIR Main/.git" ]; then
    echo "✅ Sonic 3 AIR submodule initialized"
else
    echo "❌ Sonic 3 AIR submodule not initialized"
fi

# Check RSDK decompilations
echo ""
echo "Checking RSDK decompilations..."

if [ -d "Hybrid-RSDK-Main/RSDKV4-Decompilation" ]; then
    echo "✅ RSDKv4 Decompilation found"
else
    echo "❌ RSDKv4 Decompilation not found"
fi

if [ -d "Hybrid-RSDK-Main/RSDKV3" ]; then
    echo "✅ RSDKv3 Decompilation found"
else
    echo "❌ RSDKv3 Decompilation not found"
fi

if [ -d "Hybrid-RSDK-Main/RSDKV5" ]; then
    echo "✅ RSDKv5 Decompilation found"
else
    echo "❌ RSDKv5 Decompilation not found"
fi

# Check project files
echo ""
echo "Checking project files..."

if [ -f "Hybrid-RSDK-Main/CMakeLists.txt" ]; then
    echo "✅ CMakeLists.txt found"
else
    echo "❌ CMakeLists.txt not found"
fi

if [ -f "Custom-Client/CustomClient.csproj" ]; then
    echo "✅ CustomClient.csproj found"
else
    echo "❌ CustomClient.csproj not found"
fi

# Check build directories
echo ""
echo "Checking build status..."

if [ -d "Hybrid-RSDK-Main/build" ]; then
    echo "✅ Build directory exists"
    if [ -f "Hybrid-RSDK-Main/build/bin/rsdkv4" ] || [ -f "Hybrid-RSDK-Main/build/bin/rsdkv4.exe" ]; then
        echo "✅ RSDK executable built"
    else
        echo "❌ RSDK executable not built"
    fi
else
    echo "❌ Build directory not found"
fi

if [ -d "Custom-Client/bin" ]; then
    echo "✅ Custom Client build directory exists"
else
    echo "❌ Custom Client not built"
fi

echo ""
echo "Build status check complete!"
