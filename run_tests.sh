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
    bash -c '! git grep -q "RSDKV4/RSDKV4" -- "*.cpp" "*.hpp" "*.txt" "*.cs" "*.ps1" "*.bat" 2>/dev/null'

# The bytecode stores variables as an index into RSDKv3's ScrVariable enum. A
# hand-written copy of that enum once contained 15 members RSDKv3 does not have
# and drifted out of alignment at index 41, which mislabelled every variable
# from there on without any error. The table is now generated from the engine
# source, and this test fails if it is stale.
check "generated v3 variable table is in sync with the engine sources" \
    bash -c 'python scripts/gen_rsdkv3_variables.py | grep -q "^up to date:"'

check "v3 variable mapping names only variables RSDKv4 defines" \
    bash -c 'python scripts/gen_rsdkv3_variables.py >/dev/null'

# RSDKv4 only ever runs an object through eventObjectUpdate / eventObjectDraw /
# eventObjectStartup (Script.cpp:2848-2864) or through loaded bytecode
# (Script.cpp:3191-3209). An object script with none of those markers keeps the
# sentinel pointers set at Script.cpp:3318-3325 and is never called at all.
check "every object script declares an RSDKv4 entry point" \
    bash -c 'scripts/check_script_entrypoints.py'

# Data/Sprites used to be copied S1, CD, S2 in order with last-writer-wins, so
# Sonic 1 silently drew itself with Sonic 2's player sheets - identical paths,
# different layouts. Every clashing sheet now gets a per-game copy.
check "no game's spritesheet is overwritten by another's" \
    bash -c 'python scripts/check_sprite_collisions.py'

# RSDKv4 loads one GlobalCode.bin for the whole process, so only one game can
# have working object logic unless the two containers are merged. This checks the
# merge is sound: every shipped container round-trips, every merged pointer stays
# in range, and both sentinels survive. It also fails if the two pointer tables
# ever start sharing a sentinel, which would silently corrupt every merged file.
check "bytecode merger round-trips and merges consistently" \
    bash -c 'python scripts/test_bytecode_merger.py'


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

# The packer renumbers Act layout object types by name, so they must arrive at the
# engine as destination indices exactly once. An earlier build shifted them a second
# time and a third, which pushed Green Hill's types past the end of the object table:
# 18 of its 305 objects became entities whose type matched no script. The layout still
# parsed to its exact length and the stage still reached 600 frames, so only an
# explicit bound catches it.
if [ -f "$PACK" ] && command -v python3 >/dev/null 2>&1; then
    if python3 scripts/test_act_layouts.py; then
        ok "Act layouts carry destination object types exactly once"
    else
        bad "Act layouts have been renumbered more than once"
    fi
else
    printf '  SKIP  Act layout check (no pack, or python3 unavailable)\n'
fi

# ---------------------------------------------------------------------------
head "4. RSDKv3 bytecode reader"

# Sonic CD ships no text scripts - its gameplay is RSDKv3 VM bytecode, and
# nothing in it can run on RSDKv4 until that is decompiled. The reader must parse
# every shipped file exactly, or the decompiler cannot be trusted.
BC="Hybrid-RSDK-Main/rsdk-source-data/soniccd/Data/Scripts/ByteCode"
if [ -d "$BC" ]; then
    # The generator resolves its paths from Hybrid-RSDK-Main, so run it from there.
    bc_out=$(cd Hybrid-RSDK-Main && dotnet run \
        --project SonicHybridRsdk.Build/SonicHybridRsdk.Build.csproj \
        -c Release --no-build 2>&1)
    # The generator now throws if any shipped script fails to parse, and the
    # report asserts each container is consumed to its exact byte length.
    if printf '%s' "$bc_out" | grep -qE "^  [0-9]+ files, [1-9][0-9]* instructions"; then
        ok "RSDKv3 bytecode reader parsed every Sonic CD script"
        printf '%s' "$bc_out" | grep -E "^  [0-9]+ files," | sed 's/^/      /'
    else
        bad "RSDKv3 bytecode reader failed on Sonic CD bytecode"
        printf '%s' "$bc_out" | tail -5 | sed 's/^/      /'
    fi

    # Guard the exact-consumption assertion itself: a wrong per-script pointer
    # count still "parses" but leaves bytes behind, which is how a 5-pointer bug
    # silently corrupted the tail of nine scripts.
    check "bytecode report asserts exact container consumption" \
        grep -q "reader.BytesConsumed != length" \
            Hybrid-RSDK-Main/SonicHybridRsdk.Generator/RsdkV3BytecodeReport.cs
else
    printf '  SKIP  no Sonic CD bytecode present\n'
fi

# ---------------------------------------------------------------------------
head "5. Unified stage list reachability"

# The whole premise of the project is one continuous game. Boot the engine
# directly into stages from all three games and confirm each has working object
# logic - not merely that it loads. A stage whose bytecode is missing still
# draws its background and still reports success, so "loaded" on its own would
# let this pass while nothing in the stage actually runs.
if [ -f "build/bin/Release/rsdkv4.exe" ] || [ -f "build/bin/rsdkv4" ]; then
    if python scripts/probe_stages.py; then
        ok "probed stages from all three games have working object logic"
    else
        bad "at least one game has no probed stage with working object logic"
    fi
else
    printf '  SKIP  engine binary not built\n'
fi

# ProcessStartupObjects runs the eventStartup of all 256 object types. If it stops early,
# every type after that point silently never starts up, while the stage still loads, still
# places its objects and still turns frames - so a probe cannot see it. The engine now
# reports the outcome itself, and this asserts on that rather than trusting the frames.
#
# Worth its own check: this exact failure survived two rounds of investigation here, and
# the intermediate readings were confidently wrong twice before the real cause turned out
# to be the measurement itself.
if [ -f "$OUT/log.txt" ] && command -v python3 >/dev/null 2>&1; then
    # Probe one stage that has bytecode and require its verdict to include the startup
    # loop completing. Not a grep over "$OUT/log.txt": that file is whichever stage ran
    # last, and a stage with no bytecode never reports a startup at all - which is why
    # the first version of this check failed on a perfectly healthy build.
    # `set -o pipefail` is on, so piping this would take probe_stages.py's exit status
    # rather than grep's - and it exits non-zero whenever any game lacks a working stage,
    # which is a separate test. Capture the output, then match it.
    probe_out=$(python3 scripts/probe_stages.py --scene 0 2>&1 || true)
    if printf '%s\n' "$probe_out" | grep -q "startup loop complete"; then
        ok "the engine reports the startup loop reached every object type"
    else
        bad "startup loop did not complete; some object types are silently inert"
        printf '%s\n' "$probe_out" | sed 's/^/    /' | head -8
    fi
else
    printf '  SKIP  engine binary not built, or python3 unavailable\n'
fi

# The oracle compares the bytecode walker's operand decoding against what the engine
# actually executed. Two failure modes here have each produced a confident wrong answer,
# and this asserts against both rather than against a percentage.
#
#   1. The checker used to `continue` past anything it could not place - no container
#      covers the word, the two read different opcodes, the walker threw - so it could
#      only ever report success. It now counts those paths, and they must be zero for
#      the agreement figure to mean anything.
#   2. The trace was once so slow that a traced run never got past the globals, and
#      coverage sat at exactly 190 distinct sites for stage after stage - which reads
#      as "verified" while measuring a run that never happened. Uniform per-stage
#      counts are the tell, so assert they are not all the same.
#
# One stage with a short budget: this is a regression guard, not the coverage report.
# docs/STATUS.md carries the real numbers from a full sweep.
if [ -f "build/bin/Release/rsdkv4.exe" ] && command -v python3 >/dev/null 2>&1; then
    oracle_out=$(python3 scripts/oracle_check.py 2>&1 || true)
    if printf '%s\n' "$oracle_out" | grep -q "not placed, so not compared: 0"; then
        ok "oracle compared every site it traced - nothing silently dropped"
    else
        bad "oracle silently dropped sites it could not place"
        printf '%s\n' "$oracle_out" | grep -E "not placed|opcode differs" | sed 's/^/    /'
    fi

    distinct=$(printf '%s\n' "$oracle_out" | grep -oE 'placed +[0-9]+' | \
              grep -oE '[0-9]+' | sort -u | wc -l)
    total=$(printf '%s\n' "$oracle_out" | grep -c 'placed ')
    if [ "$total" -lt 2 ]; then
        printf '  SKIP  oracle coverage-uniformity check (only %d stage traced)\n' "$total"
    elif [ "$distinct" -lt "$total" ]; then
        ok "oracle coverage varies per stage ($distinct of $total distinct)"
    else
        bad "oracle coverage identical across all $total stages - the traced run is
    probably not getting past the globals, whatever the agreement figure says"
    fi
else
    printf '  SKIP  engine binary not built, or python3 unavailable\n'
fi

# ---------------------------------------------------------------------------
head "6. Packer byte order"

# The engine rebuilds each hash word as (b0<<24)|(b1<<16)|(b2<<8)|b3, so archives
# store little-endian words. Writing raw MD5 digest bytes produces an archive
# that parses but never matches any lookup.
check "packer byte-swaps each MD5 word" \
    grep -q "stored\[w \* 4 + b\] = digest\[w \* 4 + (3 - b)\]" "$GEN_DIR/RsdkPacker.cs"

# ---------------------------------------------------------------------------
if [ "${KEEP_BUILD:-0}" != "1" ]; then
    head "7. Full build"
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