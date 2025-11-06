#!/bin/bash
set -e

# Test script to verify compilation fixes
echo "Testing compilation fixes..."

# Test 1: Try to compile the test file
echo "Test 1: Compiling test_compile_fixes.cpp..."
if g++ -c test_compile_fixes.cpp -I. -std=c++11 2>/dev/null; then
    echo "✅ Test file compiles successfully"
    rm -f test_compile_fixes.o
else
    echo "❌ Test file compilation failed"
    echo "Attempting compilation with verbose output:"
    g++ -c test_compile_fixes.cpp -I. -std=c++11
    exit 1
fi

# Test 2: Check that all required symbols are declared
echo "Test 2: Checking symbol declarations..."
if grep -q "textureList\[TEXTURE_COUNT\]" Hybrid-RSDK-Main/RSDKV4/RSDKV4/Renderer.hpp; then
    echo "✅ textureList symbol found"
else
    echo "❌ textureList symbol missing"
    exit 1
fi

if grep -q "ResetRenderStates" Hybrid-RSDK-Main/RSDKV4/RSDKV4/Renderer.hpp; then
    echo "✅ ResetRenderStates function found"
else
    echo "❌ ResetRenderStates function missing"
    exit 1
fi

# Test 3: Check TextMenu struct has new members
echo "Test 3: Checking TextMenu struct..."
if grep -q "entryStart\[0x100\]" Hybrid-RSDK-Main/RSDKV4/RSDKV4/Text.hpp; then
    echo "✅ TextMenu.entryStart member found"
else
    echo "❌ TextMenu.entryStart member missing"
    exit 1
fi

if grep -q "textData\[0x2000\]" Hybrid-RSDK-Main/RSDKV4/RSDKV4/Text.hpp; then
    echo "✅ TextMenu.textData member found"
else
    echo "❌ TextMenu.textData member missing"
    exit 1
fi

# Test 4: Check Debug.hpp uses safe string functions
echo "Test 4: Checking Debug.hpp safety..."
if grep -q "vsnprintf" Hybrid-RSDK-Main/RSDKV4/RSDKV4/Debug.hpp; then
    echo "✅ Safe vsnprintf function found"
else
    echo "❌ Safe vsnprintf function missing"
    exit 1
fi

if ! grep -q "sprintf(buffer, \"%s" Hybrid-RSDK-Main/RSDKV4/RSDKV4/Debug.hpp; then
    echo "✅ Unsafe sprintf calls removed"
else
    echo "❌ Unsafe sprintf calls still present"
    exit 1
fi

# Test 5: Check Renderer.hpp is included in RetroEngine.hpp
echo "Test 5: Checking include chain..."
if grep -A1 -B1 "Drawing.hpp" Hybrid-RSDK-Main/RSDKV4/RSDKV4/RetroEngine.hpp | grep -q "Renderer.hpp"; then
    echo "✅ Renderer.hpp included after Drawing.hpp"
else
    echo "❌ Renderer.hpp include missing or in wrong position"
    exit 1
fi

echo ""
echo "🎉 All compilation fixes verified successfully!"
echo "The build should now pass in the CI pipeline."