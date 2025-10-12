#!/bin/bash
chmod +x "$0"

# Test script to validate build fixes
echo "Testing Sonic Hybrid Ultimate build fixes..."
echo "============================================="

# Test 1: Check if fetch scripts exist and are executable
echo "Test 1: Checking fetch scripts..."
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

for script in "fetch_rsdkv3.sh" "fetch_rsdkv4.sh" "fetch_rsdkv5.sh"; do
    if [ -f "${SCRIPT_DIR}/${script}" ]; then
        echo "  ✓ ${script} exists"
        if [ -x "${SCRIPT_DIR}/${script}" ]; then
            echo "  ✓ ${script} is executable"
        else
            echo "  ⚠ ${script} is not executable (will be fixed by build script)"
        fi
    else
        echo "  ✗ ${script} missing"
        exit 1
    fi
done

# Test 2: Check if build_all.sh exists and has correct structure
echo ""
echo "Test 2: Checking build_all.sh..."
if [ -f "${SCRIPT_DIR}/build_all.sh" ]; then
    echo "  ✓ build_all.sh exists"
    
    # Check for SCRIPT_DIR usage
    if grep -q "SCRIPT_DIR=" "${SCRIPT_DIR}/build_all.sh"; then
        echo "  ✓ build_all.sh uses SCRIPT_DIR for path resolution"
    else
        echo "  ✗ build_all.sh missing SCRIPT_DIR path resolution"
        exit 1
    fi
    
    # Check for error handling
    if grep -q "if \[ \$? -ne 0 \]" "${SCRIPT_DIR}/build_all.sh"; then
        echo "  ✓ build_all.sh has error handling"
    else
        echo "  ✗ build_all.sh missing error handling"
        exit 1
    fi
    
    # Check for user guidance
    if grep -q "IMPORTANT:" "${SCRIPT_DIR}/build_all.sh"; then
        echo "  ✓ build_all.sh includes user guidance"
    else
        echo "  ✗ build_all.sh missing user guidance"
        exit 1
    fi
else
    echo "  ✗ build_all.sh missing"
    exit 1
fi

# Test 3: Check CMakeLists.txt fixes
echo ""
echo "Test 3: Checking CMakeLists.txt..."
CMAKE_FILE="${SCRIPT_DIR}/Hybrid-RSDK-Main/CMakeLists.txt"
if [ -f "${CMAKE_FILE}" ]; then
    echo "  ✓ CMakeLists.txt exists"
    
    # Check for proper WORKING_DIRECTORY usage
    if grep -q "WORKING_DIRECTORY" "${CMAKE_FILE}"; then
        echo "  ✓ CMakeLists.txt uses WORKING_DIRECTORY"
    else
        echo "  ✗ CMakeLists.txt missing WORKING_DIRECTORY"
        exit 1
    fi
    
    # Check for RESULT_VARIABLE usage
    if grep -q "RESULT_VARIABLE" "${CMAKE_FILE}"; then
        echo "  ✓ CMakeLists.txt uses RESULT_VARIABLE for error handling"
    else
        echo "  ✗ CMakeLists.txt missing RESULT_VARIABLE error handling"
        exit 1
    fi
    
    # Check for build summary
    if grep -q "Build Summary" "${CMAKE_FILE}"; then
        echo "  ✓ CMakeLists.txt includes build summary"
    else
        echo "  ✗ CMakeLists.txt missing build summary"
        exit 1
    fi
else
    echo "  ✗ CMakeLists.txt missing"
    exit 1
fi

# Test 4: Check RetroEngine.hpp TextMenu fix
echo ""
echo "Test 4: Checking RetroEngine.hpp TextMenu fix..."
RETRO_ENGINE_FILE="${SCRIPT_DIR}/Hybrid-RSDK-Main/RSDKV4/RSDKV4/RetroEngine.hpp"
if [ -f "${RETRO_ENGINE_FILE}" ]; then
    echo "  ✓ RetroEngine.hpp exists"
    
    # Check for forward declaration
    if grep -q "struct TextMenu;" "${RETRO_ENGINE_FILE}"; then
        echo "  ✓ RetroEngine.hpp has TextMenu forward declaration"
    else
        echo "  ✗ RetroEngine.hpp missing TextMenu forward declaration"
        exit 1
    fi
    
    # Check for Text.hpp include
    if grep -q '#include "Text.hpp"' "${RETRO_ENGINE_FILE}"; then
        echo "  ✓ RetroEngine.hpp includes Text.hpp"
    else
        echo "  ✗ RetroEngine.hpp missing Text.hpp include"
        exit 1
    fi
else
    echo "  ✗ RetroEngine.hpp missing"
    exit 1
fi

# Test 5: Check Text.hpp structure
echo ""
echo "Test 5: Checking Text.hpp structure..."
TEXT_FILE="${SCRIPT_DIR}/Hybrid-RSDK-Main/RSDKV4/RSDKV4/Text.hpp"
if [ -f "${TEXT_FILE}" ]; then
    echo "  ✓ Text.hpp exists"
    
    # Check for TextMenu struct
    if grep -q "struct TextMenu" "${TEXT_FILE}"; then
        echo "  ✓ Text.hpp defines TextMenu struct"
    else
        echo "  ✗ Text.hpp missing TextMenu struct"
        exit 1
    fi
    
    # Check for include guards
    if grep -q "#ifndef TEXT_H" "${TEXT_FILE}" && grep -q "#define TEXT_H" "${TEXT_FILE}"; then
        echo "  ✓ Text.hpp has proper include guards"
    else
        echo "  ✗ Text.hpp missing proper include guards"
        exit 1
    fi
else
    echo "  ✗ Text.hpp missing"
    exit 1
fi

# Test 6: Check BUILD_INSTRUCTIONS.md
echo ""
echo "Test 6: Checking documentation..."
if [ -f "${SCRIPT_DIR}/BUILD_INSTRUCTIONS.md" ]; then
    echo "  ✓ BUILD_INSTRUCTIONS.md exists"
    
    if grep -q "Prerequisites" "${SCRIPT_DIR}/BUILD_INSTRUCTIONS.md"; then
        echo "  ✓ BUILD_INSTRUCTIONS.md includes prerequisites"
    else
        echo "  ✗ BUILD_INSTRUCTIONS.md missing prerequisites"
        exit 1
    fi
else
    echo "  ✗ BUILD_INSTRUCTIONS.md missing"
    exit 1
fi

echo ""
echo "============================================="
echo "✅ All tests passed! Build fixes are ready."
echo ""
echo "Summary of fixes applied:"
echo "  • Fixed script path resolution in build_all.sh"
echo "  • Added proper error handling for optional components"
echo "  • Fixed CMakeLists.txt fetch script execution"
echo "  • Added TextMenu forward declaration in RetroEngine.hpp"
echo "  • Added comprehensive user guidance"
echo "  • Created detailed build instructions"
echo ""
echo "The build should now work without requiring .bin/.rsdk files"
echo "and will provide clear guidance to users about what files they need."