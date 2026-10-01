#!/usr/bin/env bash
# Sonic Hybrid Ultimate - verification tests
#
# These assert against the sources that are ACTUALLY COMPILED.
#
# History: the previous test_build_fixes.sh / test_undefined_symbols.sh grepped
# Hybrid-RSDK-Main/RSDKV4/RSDKV4/, a stale copy of the engine that CMake never
# referenced and that could not compile. Its assertions passed there while
# failing against the real engine, which is worse than having no tests. That
# stale tree has since been deleted.
#
# Usage:  ./run_tests.sh            (from the repository root)
#         KEEP_BUILD=1 ./run_tests.sh   # skip the full rebuild

set -uo pipefail

ENGINE_DIR="Hybrid-RSDK-Main/RSDKV4-Decompilation/RSDKv4"
GEN_DIR="Hybrid-RSDK-Main/SonicHybridRsdk.Generator"
BUILD_DIR="Hybrid-RSDK-Main"
OUT="Hybrid-RSDK-Main/sonic-hybrid"
PACK="$OUT/Data.rsdk"

pass=0
fail=0

ok()   { printf '  \033[32mPASS\033[0m  %s\n' "$1"; pass=$((pass+1)); }
bad()  { printf '  \033[31mFAIL\033[0m  %s\n' "$1"; fail=$((fail+1)); }
head() { printf '\n\033[1m%s\033[0m\n' "$1"; }

check() { # check <description> <command...>
    local desc="$1"; shift
    if "$@" >/dev/null 2>&1; then ok "$desc"; else bad "$desc"; fi
}

# ---------------------------------------------------------------------------
head "1. Source layout"

if [ ! -d "$ENGINE_DIR" ]; then
    bad "engine sources missing at $ENGINE_DIR"
    echo "    Run: git submodule update --init --recursive"
    exit 1
fi
ok "engine sources present at $ENGINE_DIR"

if [ -d "Hybrid-RSDK-Main/RSDKV4" ]; then
    bad "stale Hybrid-RSDK-Main/RSDKV4/ still exists (nothing builds it)"
else
    ok "stale Hybrid-RSDK-Main/RSDKV4/ removed"
fi

check "hybrid headers include the compiled engine, not a stale copy" \
    bash -c '! grep -rq "\.\./RSDKV4/RSDKV4/" Hybrid-RSDK-Main/sonic-hybrid/*.hpp'

check "no source references the deleted tree" \
    bash -c '! git grep -q "RSDKV4/RSDKV4" -- "*.cpp" "*.hpp" "*.txt" "*.cs" "*.sh" "*.ps1" "*.bat" 2>/dev/null'

# ---------------------------------------------------------------------------
head "2. Toolchain targets"

check "generator targets a supported .NET (not net5.0/net6.0)" \
    bash -c '! grep -qE "<TargetFramework>net[56]\.0" Hybrid-RSDK-Main/SonicHybridRsdk.*/*.csproj'

check "Custom-Client targets a supported .NET" \
    bash -c '! grep -qE "<TargetFramework>net[56]\.0" Custom-Client/CustomClient.csproj'

check "CMake stages native DLLs into the client's actual TFM folder" \
    bash -c '! grep -q "net6.0-windows" Hybrid-RSDK-Main/CMakeLists.txt'

check "CMake executable and library targets differ by more than case" \
    bash -c '[ "$(grep -c "add_executable(rsdkv4" Hybrid-RSDK-Main/CMakeLists.txt)" = 1 ] && ! grep -qE "^ *add_library\(RSDKv4 " Hybrid-RSDK-Main/CMakeLists.txt'

check "run_hybrid.bat escapes parentheses inside its if-blocks" \
    python scripts/check_batch_parens.py Hybrid-RSDK-Main/sonic-hybrid/run_hybrid.bat

# ---------------------------------------------------------------------------
head "3. Data pack integrity"

if [ -f "$PACK" ]; then
    ok "Data.rsdk was generated"
    if command -v python3 >/dev/null 2>&1; then
        if python3 scripts/verify_datarsdk.py "$PACK" "$OUT"; then
            ok "Data.rsdk parses and every required path resolves"
        else
            bad "Data.rsdk failed verification (scripts/verify_datarsdk.py)"
        fi
    else
        printf '  SKIP  pack verification (python3 not available)\n'
    fi
else
    printf '  SKIP  Data.rsdk not generated yet (run build_all.sh first)\n'
fi

# ---------------------------------------------------------------------------
head "4. Packer byte order"

# The engine rebuilds each hash word as (b0<<24)|(b1<<16)|(b2<<8)|b3, so archives
# store little-endian words. Writing raw MD5 digest bytes produces an archive
# that parses but never matches any lookup.
check "packer byte-swaps each MD5 word" \
    grep -q "stored\[w \* 4 + b\] = digest\[w \* 4 + (3 - b)\]" "$GEN_DIR/RsdkPacker.cs"

# ---------------------------------------------------------------------------
if [ "${KEEP_BUILD:-0}" != "1" ]; then
    head "5. Full build"
    if command -v cmake >/dev/null 2>&1; then
        if cmake --build build --config Release >/dev/null 2>&1; then
            ok "cmake --build"
        else
            bad "cmake --build"
        fi
    else
        printf '  SKIP  cmake not available\n'
    fi

    # The engine binary is rsdkv4.exe on Windows and rsdkv4 elsewhere.
    if ls build/bin/Release build/bin 2>/dev/null | grep -qx 'rsdkv4\(\.exe\)\?$'; then
        ok "engine binary produced"
    else
        bad "engine binary missing from build/bin"
    fi
fi

# ---------------------------------------------------------------------------
printf '\n\033[1mResult: %d passed, %d failed\033[0m\n' "$pass" "$fail"
[ "$fail" -eq 0 ]