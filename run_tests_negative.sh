#!/usr/bin/env bash
# Negative test: reintroduce each bug we fixed and confirm run_tests.sh catches it.
# A test suite that cannot fail is no better than the fake ones it replaced.
set -uo pipefail

cd "$(dirname "$0")"
PACKER="Hybrid-RSDK-Main/SonicHybridRsdk.Generator/RsdkPacker.cs"
BAK="$(mktemp)"
cp "$PACKER" "$BAK"

restore() { cp "$BAK" "$PACKER"; rm -f "$BAK"; }
trap restore EXIT

expect_fail() { # expect_fail <label>
    if KEEP_BUILD=1 bash run_tests.sh >/dev/null 2>&1; then
        printf '  \033[31mNOT CAUGHT\033[0m  %s  (test suite passed on broken source!)\n' "$1"
        return 1
    else
        printf '  \033[32mcaught\033[0m      %s\n' "$1"
        return 0
    fi
}

rc=0

# 1. raw (non-swapped) MD5 words - the original packer bug
sed -i 's/stored\[w \* 4 + b\] = digest\[w \* 4 + (3 - b)\];/stored[w * 4 + b] = digest[w * 4 + b];/' "$PACKER"
expect_fail "packer writes unswapped MD5 words" || rc=1
cp "$BAK" "$PACKER"

# 2. EOL target framework
sed -i 's#<TargetFramework>net8.0</TargetFramework>#<TargetFramework>net6.0</TargetFramework>#' \
    Hybrid-RSDK-Main/SonicHybridRsdk.Generator/SonicHybridRsdk.Generator.csproj
expect_fail "generator retargeted to net6.0" || rc=1
git checkout -- Hybrid-RSDK-Main/SonicHybridRsdk.Generator/SonicHybridRsdk.Generator.csproj 2>/dev/null
cp "$BAK" "$PACKER"

# 3. stale native-DLL staging path
sed -i 's/net8.0-windows/net6.0-windows/g' Hybrid-RSDK-Main/CMakeLists.txt
expect_fail "CMake stages DLLs into net6.0-windows" || rc=1
git checkout -- Hybrid-RSDK-Main/CMakeLists.txt 2>/dev/null

# 4. unescaped parens in the launcher
sed -i 's/(build_all.ps1 or cmake --build\^)/(build_all.ps1 or cmake --build)/' \
    Hybrid-RSDK-Main/sonic-hybrid/run_hybrid.bat
expect_fail "run_hybrid.bat unescaped parens restored" || rc=1
git checkout -- Hybrid-RSDK-Main/sonic-hybrid/run_hybrid.bat 2>/dev/null

# 5. case-colliding CMake targets
sed -i 's/^add_executable(rsdkv4$/add_executable(RSDKv4/' Hybrid-RSDK-Main/CMakeLists.txt
expect_fail "executable target renamed to collide with the library" || rc=1
git checkout -- Hybrid-RSDK-Main/CMakeLists.txt 2>/dev/null

printf '\n'
if [ "$rc" -eq 0 ]; then
    printf '\033[32mAll injected faults were detected.\033[0m\n'
else
    printf '\033[31mAt least one injected fault slipped through.\033[0m\n'
fi
exit "$rc"